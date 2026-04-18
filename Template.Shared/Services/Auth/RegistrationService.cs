using Azure;
using Azure.Data.Tables;
using Template.Shared.Services.Data;

namespace Template.Shared.Services.Auth;

/// <summary>
/// Interface for user registration operations
/// </summary>
public interface IRegistrationService
{
    Task<(bool Success, string TempUserId, string ErrorMessage)> StartRegistrationAsync(string email, string username, string password, string preferredAuthMethod);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> CompleteRegistrationAsync(string tempUserId, string otpCode);
    Task<(bool Success, string UserId, string ErrorMessage)> RegisterDirectAsync(string email, string username, string password, string preferredAuthMethod);
}

/// <summary>
/// Service for handling user registration with OTP verification
/// </summary>
public class RegistrationService(
    ITableClientFactory tableClientFactory,
    IUserRepository userRepository,
    Action<string, string>? sendEmailCallback = null) : IRegistrationService
{
    private readonly ITableClientFactory _tableClientFactory = tableClientFactory;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly Action<string, string>? _sendEmailCallback = sendEmailCallback;

    public async Task<(bool Success, string TempUserId, string ErrorMessage)> StartRegistrationAsync(
        string email, string username, string password, string preferredAuthMethod)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(username))
            return (false, string.Empty, "Username is required");

        if ((preferredAuthMethod == "Email" || preferredAuthMethod == "Both") && string.IsNullOrWhiteSpace(email))
            return (false, string.Empty, "Email is required for email authentication");

        if ((preferredAuthMethod == "Password" || preferredAuthMethod == "Both") && string.IsNullOrWhiteSpace(password))
            return (false, string.Empty, "Password is required for password authentication");

        // Check if user already exists
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(email).ConfigureAwait(false);
            if (existingUserByEmail != null)
                return (false, string.Empty, "Email already registered");
        }

        var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(username).ConfigureAwait(false);
        if (existingUserByUsername != null)
            return (false, string.Empty, "Username already taken");

        // Generate temp ID and hash password
        var tempUserId = InputValidator.GenerateUserId();
        var salt = PasswordHelper.GenerateSalt();
        var hash = PasswordHelper.HashPassword(password, salt);

        // Store pending registration
        var pendingRegClient = _tableClientFactory.GetTableClient("TemplatePendingReg");
        await pendingRegClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        var pendingEntity = new TableEntity("PendingReg", tempUserId)
        {
            ["Email"] = InputValidator.NormalizeEmail(email),
            ["Username"] = username.Trim(),
            ["NormalizedUsername"] = InputValidator.NormalizeUsername(username),
            ["PasswordHash"] = hash,
            ["PasswordSalt"] = salt,
            ["PreferredAuthMethod"] = preferredAuthMethod,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(30)
        };

        try
        {
            await pendingRegClient.AddEntityAsync(pendingEntity).ConfigureAwait(false);
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, "Registration failed");
        }

        // Generate and store OTP
        var otpCode = OTPHelper.GenerateOTP();
        var otpClient = _tableClientFactory.GetTableClient(TableNames.OTPCodes);
        await otpClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = InputValidator.NormalizeEmail(email),
            ["Code"] = otpCode,
            ["UserId"] = tempUserId,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10),
            ["IsUsed"] = false,
            ["IsRegistration"] = true
        };

        try
        {
            await otpClient.AddEntityAsync(otpEntity).ConfigureAwait(false);
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, "Failed to send verification code");
        }

        // Send email with OTP
        _sendEmailCallback?.Invoke(email, $"Your verification code is: {otpCode}");

        return (true, tempUserId, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> CompleteRegistrationAsync(
        string tempUserId, string otpCode)
    {
        var pendingRegClient = _tableClientFactory.GetTableClient("TemplatePendingReg");
        var otpClient = _tableClientFactory.GetTableClient(TableNames.OTPCodes);

        // Verify OTP
        var cutoffTime = DateTime.UtcNow;
        var safeTempUserId = tempUserId.Replace("'", "''", StringComparison.Ordinal);
        var filter = $"PartitionKey eq 'OTP' and UserId eq '{safeTempUserId}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}' and IsRegistration eq true";

        TableEntity? validOtp = null;
        await foreach (var entity in otpClient.QueryAsync<TableEntity>(filter: filter).ConfigureAwait(false))
        {
            if (entity.GetString("Code") == otpCode)
            {
                validOtp = entity;
                break;
            }
        }

        if (validOtp == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid or expired verification code");
        }

        // Get pending registration
        TableEntity pending;
        try
        {
            var pendingResponse = await pendingRegClient.GetEntityAsync<TableEntity>("PendingReg", tempUserId).ConfigureAwait(false);
            pending = pendingResponse.Value;
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Registration not found");
        }

        // Create user
        var userId = InputValidator.GenerateUserId();
        var user = new User
        {
            UserId = userId,
            Email = pending.GetString("Email") ?? string.Empty,
            Username = pending.GetString("Username") ?? string.Empty,
            NormalizedUsername = pending.GetString("NormalizedUsername") ?? string.Empty,
            PasswordHash = pending.GetString("PasswordHash") ?? string.Empty,
            PasswordSalt = pending.GetString("PasswordSalt") ?? string.Empty,
            PreferredAuthMethod = pending.GetString("PreferredAuthMethod") ?? "Both",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            LastLoginDate = DateTime.UtcNow
        };

        var createSuccess = await _userRepository.CreateUserAsync(user).ConfigureAwait(false);
        if (!createSuccess)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Failed to create user");
        }

        // Mark OTP as used
        validOtp["IsUsed"] = true;
        await otpClient.UpdateEntityAsync(validOtp, ETag.All, TableUpdateMode.Merge).ConfigureAwait(false);

        // Clean up pending registration
        try
        {
            await pendingRegClient.DeleteEntityAsync("PendingReg", tempUserId).ConfigureAwait(false);
        }
        #pragma warning disable CA1031
                catch { /* Ignore cleanup errors */ }
        #pragma warning restore CA1031

        // Create session
        var sessionId = Guid.NewGuid().ToString();
        var sessionClient = _tableClientFactory.GetTableClient(TableNames.AuthSessions);
        await sessionClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        var sessionEntity = new TableEntity("Session", sessionId)
        {
            ["UserId"] = userId,
            ["Email"] = user.Email,
            ["Username"] = user.Username,
            ["CreatedDate"] = DateTime.UtcNow,
            ["ExpiryDate"] = DateTime.UtcNow.AddDays(30),
            ["IsActive"] = true
        };

        await sessionClient.AddEntityAsync(sessionEntity).ConfigureAwait(false);

        return (true, userId, user.Username, sessionId, string.Empty);
    }

    public async Task<(bool Success, string UserId, string ErrorMessage)> RegisterDirectAsync(
        string email, string username, string password, string preferredAuthMethod)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(username))
            return (false, string.Empty, "Username is required");

        if ((preferredAuthMethod == "Password" || preferredAuthMethod == "Both") && string.IsNullOrWhiteSpace(password))
            return (false, string.Empty, "Password is required for password authentication");

        // Check existing users
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await _userRepository.GetUserByEmailAsync(email).ConfigureAwait(false);
            if (existingByEmail != null)
                return (false, string.Empty, "Email already registered");
        }

        var existingByUsername = await _userRepository.GetUserByUsernameAsync(username).ConfigureAwait(false);
        if (existingByUsername != null)
            return (false, string.Empty, "Username already taken");

        // Create user directly (no email verification)
        var userId = InputValidator.GenerateUserId();
        var salt = !string.IsNullOrWhiteSpace(password) ? PasswordHelper.GenerateSalt() : string.Empty;
        var hash = !string.IsNullOrWhiteSpace(password) ? PasswordHelper.HashPassword(password, salt) : string.Empty;

        var user = new User
        {
            UserId = userId,
            Email = !string.IsNullOrWhiteSpace(email) ? InputValidator.NormalizeEmail(email) : string.Empty,
            Username = username.Trim(),
            NormalizedUsername = InputValidator.NormalizeUsername(username),
            PasswordHash = hash,
            PasswordSalt = salt,
            PreferredAuthMethod = preferredAuthMethod,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var success = await _userRepository.CreateUserAsync(user).ConfigureAwait(false);
        if (!success)
            return (false, string.Empty, "Failed to create user");

        return (true, userId, string.Empty);
    }
}
