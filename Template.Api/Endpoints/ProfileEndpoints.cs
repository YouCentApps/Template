using Microsoft.AspNetCore.Mvc;
using Template.Api.Middleware;
using Template.Shared.Helpers;
using Template.Shared.Services.Data;

namespace Template.Api.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        // Get current user's profile
        group.MapGet("/", async (
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAdminRepository adminRepository) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            var isAdmin = await adminRepository.IsActiveAdminAsync(userId).ConfigureAwait(false);

            return Results.Ok(new
            {
                user.UserId,
                user.Email,
                user.Username,
                user.PreferredAuthMethod,
                HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                HasEmail = !string.IsNullOrEmpty(user.Email),
                user.CreatedDate,
                user.LastLoginDate,
                IsAdmin = isAdmin
            });
        })
        .RequireAuth()
        .WithName("GetProfile");

        // Update username
        group.MapPost("/username", async (
            HttpContext context,
            [FromBody] UpdateUsernameRequest request,
            [FromServices] IUserRepository userRepository) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            if (string.IsNullOrWhiteSpace(request.NewUsername))
                return Results.BadRequest(new { error = "Username is required" });

            if (request.NewUsername.Length < Policy.MinimumUsernameLength ||
                request.NewUsername.Length > Policy.MaximumUsernameLength)
                return Results.BadRequest(new { error = $"Username must be {Policy.MinimumUsernameLength}-{Policy.MaximumUsernameLength} characters" });

            var existingUser = await userRepository.GetUserByUsernameAsync(request.NewUsername).ConfigureAwait(false);
            if (existingUser != null && existingUser.UserId != userId)
                return Results.BadRequest(new { error = "Username already taken" });

            user.Username = request.NewUsername.Trim();
            user.NormalizedUsername = InputValidator.NormalizeUsername(request.NewUsername);
            var success = await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update username" });

            return Results.Ok(new { message = "Username updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdateUsername");

        // Update password
        group.MapPost("/password", async (
            HttpContext context,
            [FromBody] UpdatePasswordRequest request,
            [FromServices] IUserRepository userRepository) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            if (string.IsNullOrWhiteSpace(request.NewPassword) ||
                request.NewPassword.Length < Policy.MinimumPasswordLength)
                return Results.BadRequest(new { error = $"Password must be at least {Policy.MinimumPasswordLength} characters" });

            var hasExistingPassword = !string.IsNullOrEmpty(user.PasswordHash);

            if (hasExistingPassword)
            {
                if (string.IsNullOrEmpty(request.OldPassword))
                    return Results.BadRequest(new { error = "Current password is required" });

                if (!PasswordHelper.VerifyPassword(request.OldPassword, user.PasswordHash, user.PasswordSalt))
                    return Results.BadRequest(new { error = "Current password is incorrect" });
            }

            var salt = PasswordHelper.GenerateSalt();
            var hash = PasswordHelper.HashPassword(request.NewPassword, salt);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;

            var success = await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update password" });

            return Results.Ok(new { message = "Password updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdatePassword");

        // Update preferred auth method
        group.MapPost("/auth-method", async (
            HttpContext context,
            [FromBody] UpdateAuthMethodRequest request,
            [FromServices] IUserRepository userRepository) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            var method = request.NewAuthMethod;
            if (method != "Email" && method != "Password" && method != "Both")
                return Results.BadRequest(new { error = "Invalid auth method. Use Email, Password, or Both." });

            if ((method == "Password" || method == "Both") && string.IsNullOrEmpty(user.PasswordHash))
                return Results.BadRequest(new { error = "Cannot use password authentication without a password set. Set a password first." });

            if ((method == "Email" || method == "Both") && string.IsNullOrEmpty(user.Email))
                return Results.BadRequest(new { error = "Cannot use email authentication without an email set." });

            user.PreferredAuthMethod = method;
            var success = await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update auth method" });

            return Results.Ok(new { message = "Auth method updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdateAuthMethod");
    }
}

// Request DTOs
public record UpdateUsernameRequest(string NewUsername);
public record UpdatePasswordRequest(string? OldPassword, string NewPassword);
public record UpdateAuthMethodRequest(string NewAuthMethod);
