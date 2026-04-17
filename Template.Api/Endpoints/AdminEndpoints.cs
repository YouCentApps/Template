using Microsoft.AspNetCore.Mvc;
using Template.Api.Middleware;
using Template.Shared.Models;
using Template.Shared.Services.Data;

namespace Template.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        // Get current user's admin status (only requires authentication)
        group.MapGet("/me", async (
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var userId = httpContext.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var admin = await adminRepo.GetAdminAsync(userId).ConfigureAwait(false);
            if (admin == null)
                return Results.Ok(new { isAdmin = false, canManageAdmins = false });

            return Results.Ok(new { isAdmin = admin.IsActive, canManageAdmins = admin.CanManageAdmins });
        })
        .RequireAuth()
        .WithName("GetMyAdminStatus");

        // Get all admins
        group.MapGet("/admins", async (
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admins = await adminRepo.GetAllAdminsAsync().ConfigureAwait(false);
            return Results.Ok(admins);
        })
        .RequireAdmin()
        .WithName("GetAllAdmins");

        // Get specific admin
        group.MapGet("/admins/{userId}", async (
            string userId,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admin = await adminRepo.GetAdminAsync(userId).ConfigureAwait(false);
            if (admin == null)
                return Results.NotFound(new { error = "Admin not found" });

            return Results.Ok(admin);
        })
        .RequireAdmin()
        .WithName("GetAdmin");

        // Create new admin
        group.MapPost("/admins", async (
            [FromBody] CreateAdminRequest request,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo,
            [FromServices] IUserRepository userRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(request.UserId).ConfigureAwait(false);
            if (user == null)
                return Results.BadRequest(new { error = "User not found" });

            var existingAdmin = await adminRepo.GetAdminAsync(request.UserId).ConfigureAwait(false);
            if (existingAdmin != null)
                return Results.BadRequest(new { error = "User is already an admin" });

            var createdBy = httpContext.GetUserId() ?? "System";
            var admin = new Admin
            {
                UserId = request.UserId,
                IsActive = request.IsActive,
                CanManageAdmins = request.CanManageAdmins,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            var success = await adminRepo.CreateAdminAsync(admin).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to create admin" });

            return Results.Ok(new { message = "Admin created successfully" });
        })
        .RequireAdminManagement()
        .WithName("CreateAdmin");

        // Update admin
        group.MapPut("/admins/{userId}", async (
            string userId,
            [FromBody] UpdateAdminRequest request,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admin = await adminRepo.GetAdminAsync(userId).ConfigureAwait(false);
            if (admin == null)
                return Results.NotFound(new { error = "Admin not found" });

            admin.IsActive = request.IsActive;
            admin.CanManageAdmins = request.CanManageAdmins;
            admin.LastModifiedDate = DateTime.UtcNow;
            admin.LastModifiedBy = httpContext.GetUserId();

            var success = await adminRepo.UpdateAdminAsync(admin).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update admin" });

            return Results.Ok(new { message = "Admin updated successfully" });
        })
        .RequireAdminManagement()
        .WithName("UpdateAdmin");

        // Delete admin (prevent self-removal)
        group.MapDelete("/admins/{userId}", async (
            string userId,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var currentUserId = httpContext.GetUserId();
            if (currentUserId == userId)
                return Results.BadRequest(new { error = "Cannot remove your own admin privileges" });

            var success = await adminRepo.DeleteAdminAsync(userId).ConfigureAwait(false);
            if (!success)
                return Results.NotFound(new { error = "Admin not found" });

            return Results.Ok(new { message = "Admin removed successfully" });
        })
        .RequireAdminManagement()
        .WithName("DeleteAdmin");

        // Get all users (admin view)
        group.MapGet("/users", async (
            [FromServices] IUserRepository userRepo) =>
        {
            var users = await userRepo.GetAllUsersAsync().ConfigureAwait(false);
            var result = users.Select(u => new
            {
                u.UserId,
                u.Email,
                u.Username,
                u.IsActive,
                u.CreatedDate,
                u.LastLoginDate
            });
            return Results.Ok(result);
        })
        .RequireAdmin()
        .WithName("GetAllUsers");

        // Search users
        group.MapGet("/users/search", async (
            [FromQuery] string q,
            [FromServices] IUserRepository userRepo) =>
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Results.BadRequest(new { error = "Search query must be at least 2 characters" });

            var allUsers = await userRepo.GetAllUsersAsync().ConfigureAwait(false);
            var searchLower = q.ToLowerInvariant();
            var results = allUsers
                .Where(u => (u.Username?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (u.Email?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.Username,
                    u.IsActive,
                    u.CreatedDate,
                    u.LastLoginDate
                })
                .Take(50);
            return Results.Ok(results);
        })
        .RequireAdmin()
        .WithName("SearchUsers");

        // Get specific user (admin view)
        group.MapGet("/users/{userId}", async (
            string userId,
            [FromServices] IUserRepository userRepo,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            var isAdmin = await adminRepo.IsActiveAdminAsync(userId).ConfigureAwait(false);

            return Results.Ok(new
            {
                user.UserId,
                user.Email,
                user.Username,
                user.IsActive,
                user.CreatedDate,
                user.LastLoginDate,
                IsAdmin = isAdmin
            });
        })
        .RequireAdmin()
        .WithName("GetUserAdmin");

        // Update user (admin)
        group.MapPut("/users/{userId}", async (
            string userId,
            [FromBody] AdminUpdateUserRequest request,
            [FromServices] IUserRepository userRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            user.Username = request.Username;
            user.NormalizedUsername = request.Username.ToLowerInvariant();
            user.Email = request.Email.ToLowerInvariant();
            user.IsActive = request.IsActive;

            var success = await userRepo.UpdateUserAsync(user).ConfigureAwait(false);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update user" });

            return Results.Ok(new { message = "User updated successfully" });
        })
        .RequireAdmin()
        .WithName("UpdateUserAdmin");

        // Delete user (admin)
        group.MapDelete("/users/{userId}", async (
            string userId,
            [FromServices] IUserRepository userRepo) =>
        {
            var success = await userRepo.DeleteUserAsync(userId).ConfigureAwait(false);
            if (!success)
                return Results.NotFound(new { error = "User not found" });

            return Results.Ok(new { message = "User deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteUserAdmin");
    }
}

// Request DTOs
public record CreateAdminRequest(string UserId, bool IsActive, bool CanManageAdmins);
public record UpdateAdminRequest(bool IsActive, bool CanManageAdmins);
public record AdminUpdateUserRequest(string Username, string Email, bool IsActive);
