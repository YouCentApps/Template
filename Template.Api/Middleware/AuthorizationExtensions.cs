using Template.Shared.Services.Auth;
using Template.Shared.Services.Data;

namespace Template.Api.Middleware;

public static class AuthorizationExtensions
{
    public static async Task<IResult?> ValidateSession(
        HttpContext context,
        IAuthenticationService authService)
    {
        if (!context.Request.Headers.TryGetValue("X-Session-Id", out var sessionId) ||
            string.IsNullOrEmpty(sessionId))
        {
            return Results.Unauthorized();
        }

        var (success, userId, username, validSessionId, _) =
            await authService.ValidateSessionAsync(sessionId!);

        if (!success || string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        context.Items["UserId"] = userId;
        context.Items["Username"] = username;
        context.Items["SessionId"] = validSessionId;

        return null;
    }

    public static async Task<IResult?> ValidateAdminSession(
        HttpContext context,
        IAuthenticationService authService,
        IAdminRepository adminRepo)
    {
        var sessionResult = await ValidateSession(context, authService);
        if (sessionResult != null)
            return sessionResult;

        var userId = context.Items["UserId"] as string;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var isActiveAdmin = await adminRepo.IsActiveAdminAsync(userId);
        if (!isActiveAdmin)
        {
            return Results.Json(
                new { error = "Administrator privileges required" },
                statusCode: 403);
        }

        return null;
    }

    public static async Task<IResult?> ValidateAdminManagementPermission(
        HttpContext context,
        IAuthenticationService authService,
        IAdminRepository adminRepo)
    {
        var adminResult = await ValidateAdminSession(context, authService, adminRepo);
        if (adminResult != null)
            return adminResult;

        var userId = context.Items["UserId"] as string;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var canManageAdmins = await adminRepo.CanManageAdminsAsync(userId);
        if (!canManageAdmins)
        {
            return Results.Json(
                new { error = "Admin management privileges required" },
                statusCode: 403);
        }

        return null;
    }

    public static string? GetUserId(this HttpContext context)
        => context.Items["UserId"] as string;

    public static string? GetUsername(this HttpContext context)
        => context.Items["Username"] as string;

    public static string? GetSessionId(this HttpContext context)
        => context.Items["SessionId"] as string;
}

public static class EndpointAuthorizationExtensions
{
    public static RouteHandlerBuilder RequireAuth(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();

            var result = await AuthorizationExtensions.ValidateSession(httpContext, authService);
            if (result != null)
                return result;

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
            var adminRepo = httpContext.RequestServices.GetRequiredService<IAdminRepository>();

            var result = await AuthorizationExtensions.ValidateAdminSession(httpContext, authService, adminRepo);
            if (result != null)
                return result;

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireAdminManagement(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
            var adminRepo = httpContext.RequestServices.GetRequiredService<IAdminRepository>();

            var result = await AuthorizationExtensions.ValidateAdminManagementPermission(httpContext, authService, adminRepo);
            if (result != null)
                return result;

            return await next(context);
        });
    }
}
