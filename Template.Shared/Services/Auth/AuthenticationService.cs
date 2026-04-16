using Azure;
using Azure.Data.Tables;
using Template.Shared.Models;
using Template.Shared.Services.Data;
using Template.Shared.Helpers;

namespace Template.Shared.Services.Auth;

/// <summary>
/// Interface for authentication operations
/// </summary>
public interface IAuthenticationService
{
    Task<(bool Success, string ActualEmail, string ErrorMessage)> SendOTPAsync(string emailOrUsername);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> SignInWithPasswordAsync(string emailOrUsername, string password);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> ValidateSessionAsync(string sessionId);
    Task SignOutAsync(string sessionId);
}

/// <summary>
/// Service for handling user authentication
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ITableClientFactory _tableClientFactory;
    private readonly IUserRepository _userRepository;
    private readonly Action<string, string>? _sendEmailCallback;

    public AuthenticationService(
        ITableClientFactory tableClientFactory,
        IUserRepository userRepository,
        Action<string, string>? sendEmailCallback = null)
    {
        _tableClientFactory = tableClientFactory;
        _userRepository = userRepository;
        _sendEmailCallback = sendEmailCallback;
    }

    public async Task<(bool Success, string ActualEmail, string ErrorMessage)> SendOTPAsync(string emailOrUsername)
    {
        // Find user by email or username
        User? user = null;

        if (emailOrUsername.Contains("@"))
        {
            user = await _userRepository.GetUserByEmailAsync(emailOrUsername);
        }
        else
        {
            user = await _userRepository.GetUserByUsernameAsync(emailOrUsername);
        }

        if (user == null)
        {
            return (false, string.Empty, "User not found");
        }

        if (!user.IsActive)
        {
            return (false, string.Empty, "Account is inactive");
        }

        // Generate and store OTP
        var otpCode = OTPHelper.GenerateOTP();
        var otpClient = _tableClientFactory.GetTableClient(TableNames.OTPCodes);
        await otpClient.CreateIfNotExistsAsync();

        var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = user.Email,
            ["Code"] = otpCode,
            ["UserId"] = user.UserId,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10),
            ["IsUsed"] = false,
            ["IsRegistration"] = false
        };

        try
        {
            await otpClient.AddEntityAsync(otpEntity);
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, "Failed to send OTP");
        }

        // Send email
        _sendEmailCallback?.Invoke(user.Email, $"Your login code is: {otpCode}");

        return (true, user.Email, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> VerifyOTPAsync(
        string email, string otpCode)
    {
        var otpClient = _tableClientFactory.GetTableClient(TableNames.OTPCodes);

        // Find valid OTP
        var normalizedEmail = InputValidator.NormalizeEmail(email);
        var cutoffTime = DateTime.UtcNow;
        var safeEmail = normalizedEmail.Replace("'", "''");
        var filter = $"PartitionKey eq 'OTP' and Email eq '{safeEmail}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";

        TableEntity? validOtp = null;
        await foreach (var entity in otpClient.QueryAsync<TableEntity>(filter: filter))
        {
            if (entity.GetString("Code") == otpCode)
            {
                validOtp = entity;
                break;
            }
        }

        if (validOtp == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid or expired code");
        }

        // Get user
        var userId = validOtp.GetString("UserId") ?? string.Empty;
        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null || !user.IsActive)
        {
            return (false, string.Empty, string.Empty, string.Empty, "User not found or inactive");
        }

        // Mark OTP as used
        validOtp["IsUsed"] = true;
        await otpClient.UpdateEntityAsync(validOtp, ETag.All, TableUpdateMode.Merge);

        // Update last login
        await _userRepository.UpdateLastLoginAsync(userId);

        // Create session
        var sessionId = await CreateSessionAsync(userId, user.Email, user.Username);

        return (true, userId, user.Username, sessionId, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> SignInWithPasswordAsync(
        string emailOrUsername, string password)
    {
        // Find user
        User? user = null;

        if (emailOrUsername.Contains("@"))
        {
            user = await _userRepository.GetUserByEmailAsync(emailOrUsername);
        }
        else
        {
            user = await _userRepository.GetUserByUsernameAsync(emailOrUsername);
        }

        if (user == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid credentials");
        }

        if (!user.IsActive)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Account is inactive");
        }

        // Verify password
        if (!PasswordHelper.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid credentials");
        }

        // Update last login
        await _userRepository.UpdateLastLoginAsync(user.UserId);

        // Create session
        var sessionId = await CreateSessionAsync(user.UserId, user.Email, user.Username);

        return (true, user.UserId, user.Username, sessionId, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> ValidateSessionAsync(
        string sessionId)
    {
        var sessionClient = _tableClientFactory.GetTableClient(TableNames.AuthSessions);

        try
        {
            var response = await sessionClient.GetEntityAsync<TableEntity>("Session", sessionId);
            var session = response.Value;

            var isActive = session.GetBoolean("IsActive") ?? false;
            var expiryDate = session.GetDateTimeOffset("ExpiryDate")?.DateTime ?? DateTime.MinValue;

            if (!isActive || expiryDate < DateTime.UtcNow)
            {
                return (false, string.Empty, string.Empty, string.Empty, "Session expired");
            }

            var userId = session.GetString("UserId") ?? string.Empty;
            var username = session.GetString("Username") ?? string.Empty;

            return (true, userId, username, sessionId, string.Empty);
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid session");
        }
    }

    public async Task SignOutAsync(string sessionId)
    {
        var sessionClient = _tableClientFactory.GetTableClient(TableNames.AuthSessions);

        try
        {
            var response = await sessionClient.GetEntityAsync<TableEntity>("Session", sessionId);
            var session = response.Value;
            session["IsActive"] = false;
            await sessionClient.UpdateEntityAsync(session, ETag.All, TableUpdateMode.Merge);
        }
        catch (RequestFailedException)
        {
            // Session not found or already inactive
        }
    }

    private async Task<string> CreateSessionAsync(string userId, string email, string username)
    {
        var sessionId = Guid.NewGuid().ToString();
        var sessionClient = _tableClientFactory.GetTableClient(TableNames.AuthSessions);
        await sessionClient.CreateIfNotExistsAsync();

        var sessionEntity = new TableEntity("Session", sessionId)
        {
            ["UserId"] = userId,
            ["Email"] = email,
            ["Username"] = username,
            ["CreatedDate"] = DateTime.UtcNow,
            ["ExpiryDate"] = DateTime.UtcNow.AddDays(30),
            ["IsActive"] = true
        };

        await sessionClient.AddEntityAsync(sessionEntity);
        return sessionId;
    }
}
