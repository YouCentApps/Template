using System.Net.Http.Json;
using System.Text.Json;

namespace Template.Shared.Services.Api;

public interface ITemplateApiClient
{
    // Authentication
    Task<ApiClientResponse<RegisterVerificationResponse>> SendRegistrationVerificationAsync(string email, string username, string password, string preferredAuthMethod);
    Task<ApiClientResponse<AuthResponse>> VerifyRegistrationAsync(string tempUserId, string otpCode);
    Task<ApiClientResponse<MessageResponse>> RegisterAsync(string email, string username, string password, string preferredAuthMethod);
    Task<ApiClientResponse<SendOtpResponse>> SendOtpAsync(string emailOrUsername);
    Task<ApiClientResponse<AuthResponse>> VerifyOtpAsync(string email, string otpCode);
    Task<ApiClientResponse<AuthResponse>> SignInAsync(string emailOrUsername, string password);
    Task<ApiClientResponse<AuthResponse>> ValidateSessionAsync(string sessionId);
    Task<ApiClientResponse<MessageResponse>> SignOutAsync(string sessionId);

    // Profile
    Task<ApiClientResponse<UserProfileResponse>> GetProfileAsync();
    Task<ApiClientResponse<MessageResponse>> UpdateUsernameAsync(string newUsername);
    Task<ApiClientResponse<MessageResponse>> UpdatePasswordAsync(string? oldPassword, string newPassword);
    Task<ApiClientResponse<MessageResponse>> UpdateAuthMethodAsync(string newAuthMethod);

    // Admin
    Task<ApiClientResponse<AdminStatusResponse>> GetMyAdminStatusAsync();
    Task<ApiClientResponse<List<AdminInfo>>> GetAllAdminsAsync();
    Task<ApiClientResponse<MessageResponse>> CreateAdminAsync(string userId, bool isActive, bool canManageAdmins);
    Task<ApiClientResponse<MessageResponse>> UpdateAdminAsync(string userId, bool isActive, bool canManageAdmins);
    Task<ApiClientResponse<MessageResponse>> RemoveAdminAsync(string userId);
    Task<ApiClientResponse<List<UserInfo>>> GetAllUsersAsync();
    Task<ApiClientResponse<List<UserInfo>>> SearchUsersAsync(string query);
    Task<ApiClientResponse<UserInfo>> GetUserAsync(string userId);
    Task<ApiClientResponse<MessageResponse>> AdminUpdateUserAsync(string userId, string username, string email, bool isActive);
    Task<ApiClientResponse<MessageResponse>> AdminDeleteUserAsync(string userId);
}

public class TemplateApiClient(HttpClient httpClient) : ITemplateApiClient
{
    private readonly HttpClient _httpClient = httpClient;

    // Authentication
    public async Task<ApiClientResponse<RegisterVerificationResponse>> SendRegistrationVerificationAsync(string email, string username, string password, string preferredAuthMethod)
    {
        return await PostAsync<RegisterVerificationResponse>("/api/auth/register/send-verification",
            new { email, username, password, preferredAuthMethod }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<AuthResponse>> VerifyRegistrationAsync(string tempUserId, string otpCode)
    {
        return await PostAsync<AuthResponse>("/api/auth/register/verify",
            new { tempUserId, otpCode }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> RegisterAsync(string email, string username, string password, string preferredAuthMethod)
    {
        return await PostAsync<MessageResponse>("/api/auth/register",
            new { email, username, password, preferredAuthMethod }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<SendOtpResponse>> SendOtpAsync(string emailOrUsername)
    {
        return await PostAsync<SendOtpResponse>("/api/auth/send-otp",
            new { emailOrUsername }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<AuthResponse>> VerifyOtpAsync(string email, string otpCode)
    {
        return await PostAsync<AuthResponse>("/api/auth/verify-otp",
            new { email, otpCode }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<AuthResponse>> SignInAsync(string emailOrUsername, string password)
    {
        return await PostAsync<AuthResponse>("/api/auth/signin",
            new { emailOrUsername, password }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<AuthResponse>> ValidateSessionAsync(string sessionId)
    {
        return await PostAsync<AuthResponse>("/api/auth/validate-session",
            new { sessionId }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> SignOutAsync(string sessionId)
    {
        return await PostAsync<MessageResponse>("/api/auth/signout",
            new { sessionId }).ConfigureAwait(false);
    }

    // Profile
    public async Task<ApiClientResponse<UserProfileResponse>> GetProfileAsync()
    {
        return await GetAsync<UserProfileResponse>("/api/profile").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> UpdateUsernameAsync(string newUsername)
    {
        return await PostAsync<MessageResponse>("/api/profile/username",
            new { newUsername }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> UpdatePasswordAsync(string? oldPassword, string newPassword)
    {
        return await PostAsync<MessageResponse>("/api/profile/password",
            new { oldPassword, newPassword }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> UpdateAuthMethodAsync(string newAuthMethod)
    {
        return await PostAsync<MessageResponse>("/api/profile/auth-method",
            new { newAuthMethod }).ConfigureAwait(false);
    }

    // Admin
    public async Task<ApiClientResponse<AdminStatusResponse>> GetMyAdminStatusAsync()
    {
        return await GetAsync<AdminStatusResponse>("/api/admin/me").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<List<AdminInfo>>> GetAllAdminsAsync()
    {
        return await GetAsync<List<AdminInfo>>("/api/admin/admins").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> CreateAdminAsync(string userId, bool isActive, bool canManageAdmins)
    {
        return await PostAsync<MessageResponse>("/api/admin/admins",
            new { userId, isActive, canManageAdmins }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> UpdateAdminAsync(string userId, bool isActive, bool canManageAdmins)
    {
        return await PutAsync<MessageResponse>($"/api/admin/admins/{userId}",
            new { isActive, canManageAdmins }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> RemoveAdminAsync(string userId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/admins/{userId}").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<List<UserInfo>>> GetAllUsersAsync()
    {
        return await GetAsync<List<UserInfo>>("/api/admin/users").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<UserInfo>> GetUserAsync(string userId)
    {
        return await GetAsync<UserInfo>($"/api/admin/users/{userId}").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<List<UserInfo>>> SearchUsersAsync(string query)
    {
        return await GetAsync<List<UserInfo>>($"/api/admin/users/search?q={Uri.EscapeDataString(query)}").ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> AdminUpdateUserAsync(string userId, string username, string email, bool isActive)
    {
        return await PutAsync<MessageResponse>($"/api/admin/users/{userId}",
            new { username, email, isActive }).ConfigureAwait(false);
    }

    public async Task<ApiClientResponse<MessageResponse>> AdminDeleteUserAsync(string userId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/users/{userId}").ConfigureAwait(false);
    }

    // Helper methods
    private async Task<ApiClientResponse<T>> GetAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                return new ApiClientResponse<T> { Success = true, Data = data };
            }
            var error = await ParseErrorResponseAsync(response).ConfigureAwait(false);
            return new ApiClientResponse<T> { Success = false, Error = error };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new ApiClientResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiClientResponse<T>> PostAsync<T>(string url, object data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, data).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                return new ApiClientResponse<T> { Success = true, Data = result };
            }
            var error = await ParseErrorResponseAsync(response).ConfigureAwait(false);
            return new ApiClientResponse<T> { Success = false, Error = error };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new ApiClientResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiClientResponse<T>> PutAsync<T>(string url, object data)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, data).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                return new ApiClientResponse<T> { Success = true, Data = result };
            }
            var error = await ParseErrorResponseAsync(response).ConfigureAwait(false);
            return new ApiClientResponse<T> { Success = false, Error = error };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new ApiClientResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiClientResponse<T>> DeleteAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
                return new ApiClientResponse<T> { Success = true, Data = result };
            }
            var error = await ParseErrorResponseAsync(response).ConfigureAwait(false);
            return new ApiClientResponse<T> { Success = false, Error = error };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return new ApiClientResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private static async Task<string> ParseErrorResponseAsync(HttpResponseMessage response)
    {
        try
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "Your session has expired. Please sign in again.";

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return "You don't have permission to access this resource.";

            var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(errorContent);
            if (doc.RootElement.TryGetProperty("error", out var errorProperty))
                return errorProperty.GetString() ?? errorContent;

            return errorContent;
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            return "An error occurred. Please try again.";
        }
    }
}

// Response DTOs
public class ApiClientResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public record RegisterVerificationResponse(string TempUserId, string Message);
public record AuthResponse(string UserId, string Username, string SessionId, string Message);
public record SendOtpResponse(string Message, string Email);
public record MessageResponse(string Message);
public record AdminStatusResponse(bool IsAdmin, bool CanManageAdmins);

public record UserProfileResponse(
    string UserId,
    string Email,
    string Username,
    string PreferredAuthMethod,
    bool HasPassword,
    bool HasEmail,
    DateTime CreatedDate,
    DateTime? LastLoginDate,
    bool IsAdmin);

public record AdminInfo(
    string UserId,
    bool IsActive,
    bool CanManageAdmins,
    DateTime CreatedDate,
    string CreatedBy);

public record UserInfo(
    string UserId,
    string Email,
    string Username,
    bool IsActive,
    DateTime CreatedDate,
    DateTime? LastLoginDate,
    bool IsAdmin = false);
