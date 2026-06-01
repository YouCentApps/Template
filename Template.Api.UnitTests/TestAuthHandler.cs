using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Template.Api.UnitTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    #pragma warning disable CS0618 // Obsolete ISystemClock
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        System.Text.Encodings.Web.UrlEncoder encoder,
        ISystemClock clock)
        : base(options, loggerFactory, encoder, clock)
    {
    }
    #pragma warning restore CS0618

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var context = Context.Request;
        var role = context.Headers["X-Test-Role"].ToString();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Name, "testuser")
        };

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));

            // If the role is "Admin", also grant "AdminManagement" 
            // as most endpoints use .RequireAdminManagement()
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, "AdminManagement"));
            }
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestAuth");

        // Also set the UserId in HttpContext.Items to match the ClaimTypes.NameIdentifier
        // This is required for the app's ValidateAdminSession logic to work
        Context.Items["UserId"] = "user123";
        Context.Items["Username"] = "testuser";

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
