using Microsoft.AspNetCore.Mvc;
using Template.Shared.Services.Auth;

namespace Template.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        // Send registration verification email
        group.MapPost("/register/send-verification", async (
            [FromBody] RegisterRequest request,
            [FromServices] IRegistrationService registrationService) =>
        {
            var (success, tempUserId, errorMessage) = await registrationService.StartRegistrationAsync(
                request.Email, request.Username, request.Password, request.PreferredAuthMethod ?? "Both").ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { tempUserId, message = "Verification code sent to your email" });
        })
        .WithName("SendRegistrationVerification");

        // Verify email and complete registration
        group.MapPost("/register/verify", async (
            [FromBody] VerifyRegistrationRequest request,
            [FromServices] IRegistrationService registrationService) =>
        {
            var (success, userId, username, sessionId, errorMessage) = await registrationService.CompleteRegistrationAsync(
                request.TempUserId, request.OtpCode).ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { userId, username, sessionId, message = "Registration successful" });
        })
        .WithName("VerifyRegistration");

        // Direct registration (no email verification, for password-only users)
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            [FromServices] IRegistrationService registrationService) =>
        {
            var (success, userId, errorMessage) = await registrationService.RegisterDirectAsync(
                request.Email, request.Username, request.Password, request.PreferredAuthMethod ?? "Password").ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { userId, message = "Registration successful" });
        })
        .WithName("Register");

        // Send OTP to email
        group.MapPost("/send-otp", async (
            [FromBody] SendOtpRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, actualEmail, errorMessage) = await authService.SendOTPAsync(request.EmailOrUsername).ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { message = "OTP sent to email", email = actualEmail });
        })
        .WithName("SendOTP");

        // Verify OTP
        group.MapPost("/verify-otp", async (
            [FromBody] VerifyOtpRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, sessionId, errorMessage) = await authService.VerifyOTPAsync(
                request.Email, request.OtpCode).ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { userId, username, sessionId, message = "Authentication successful" });
        })
        .WithName("VerifyOTP");

        // Sign in with password
        group.MapPost("/signin", async (
            [FromBody] SignInRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, sessionId, errorMessage) = await authService.SignInWithPasswordAsync(
                request.EmailOrUsername, request.Password).ConfigureAwait(false);

            if (!success)
                return Results.BadRequest(new { error = errorMessage });

            return Results.Ok(new { userId, username, sessionId, message = "Sign in successful" });
        })
        .WithName("SignIn");

        // Validate session
        group.MapPost("/validate-session", async (
            [FromBody] ValidateSessionRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, sessionId, errorMessage) =
                await authService.ValidateSessionAsync(request.SessionId).ConfigureAwait(false);

            if (!success)
                return Results.Unauthorized();

            return Results.Ok(new { userId, username, sessionId });
        })
        .WithName("ValidateSession");

        // Sign out
        group.MapPost("/signout", async (
            [FromBody] SignOutRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            await authService.SignOutAsync(request.SessionId).ConfigureAwait(false);
            return Results.Ok(new { message = "Signed out successfully" });
        })
        .WithName("SignOut");
    }
}

// Request DTOs
public record RegisterRequest(string Email, string Username, string Password, string? PreferredAuthMethod);
public record VerifyRegistrationRequest(string TempUserId, string OtpCode);
public record SendOtpRequest(string EmailOrUsername);
public record VerifyOtpRequest(string Email, string OtpCode);
public record SignInRequest(string EmailOrUsername, string Password);
public record ValidateSessionRequest(string SessionId);
public record SignOutRequest(string SessionId);
