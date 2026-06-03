using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Collections.Generic;
using Template.Common.Services.Data;
using Template.Common.Models;
using Template.Common.Services.Auth;
using AuthService = Template.Common.Services.Auth.IAuthenticationService;

namespace Template.Api.UnitTests;

[TestClass]
public class ProfileEndpointsTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private Mock<IUserRepository> _userRepoMock = null!;
    private Mock<IAdminRepository> _adminRepoMock = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _adminRepoMock = new Mock<IAdminRepository>();
        var authServiceMock = new Mock<AuthService>();

        // Mock all IAuthenticationService methods
        authServiceMock.Setup(s => s.ValidateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((true, "user123", "testuser", "mock-session-id", string.Empty));

        // CA2000: The factory is disposed in [TestCleanup] which runs after each test method.
        // MSTest guarantees Cleanup() execution, so we suppress the warning.
        #pragma warning disable CA2000 // Dispose objects before losing scope
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => _userRepoMock.Object);
                    services.AddScoped(_ => _adminRepoMock.Object);
                    services.AddScoped(_ => authServiceMock.Object);

                    // Mock Authentication
                    services.AddAuthentication("TestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

                    services.AddAuthorization(options => {
                        // No special policies needed for just the handler; 
                        // the handler will provide the principal.
                    });
                });
            });
        #pragma warning restore CA2000

        _client = _factory.CreateClient();

        // Set default headers for all requests
        _client.DefaultRequestHeaders.Add("X-Test-Role", "User");
        _client.DefaultRequestHeaders.Add("X-Session-Id", "mock-session-id");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── GET /api/profile ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetProfile_NoSession_Returns401()
    {
        // Remove session header to simulate unauthenticated request
        _client.DefaultRequestHeaders.Remove("X-Session-Id");

        var response = await _client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task GetProfile_ExistingUser_ReturnsProfile()
    {
        var userId = "user123";
        var user = new User { UserId = userId, Email = "test@test.com", Username = "testuser" };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _adminRepoMock.Setup(r => r.IsActiveAdminAsync(userId)).ReturnsAsync(false);

        var response = await _client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task GetProfile_UserNotFound_ReturnsNotFound()
    {
        _userRepoMock.Setup(r => r.GetUserByIdAsync("user123")).ReturnsAsync((User?)null);

        var response = await _client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/profile/username ────────────────────────────────────────

    [TestMethod]
    public async Task UpdateUsername_ValidRequest_ReturnsOk()
    {
        var userId = "user123";
        var user = new User { UserId = userId };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetUserByUsernameAsync("newname")).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).ReturnsAsync(true);

        var request = new { NewUsername = "newname" };
        var response = await _client.PostAsJsonAsync("/api/profile/username", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task UpdateUsername_TooShort_ReturnsBadRequest()
    {
        var userId = "user123";
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });

        var request = new { NewUsername = "ab" }; // Below MinimumUsernameLength (3)
        var response = await _client.PostAsJsonAsync("/api/profile/username", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task UpdateUsername_Taken_ReturnsBadRequest()
    {
        var userId = "user123";
        var user = new User { UserId = userId };
        var existingUser = new User { UserId = "other123" };

        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.GetUserByUsernameAsync("taken")).ReturnsAsync(existingUser);

        var request = new { NewUsername = "taken" };
        var response = await _client.PostAsJsonAsync("/api/profile/username", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/profile/password ──────────────────────────────────────────

    [TestMethod]
    public async Task UpdatePassword_ValidRequest_ReturnsOk()
    {
        // Test first-time password setup (no existing password)
        var userId = "user123";
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new User { UserId = userId, PasswordHash = "", PasswordSalt = "" });
        _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).ReturnsAsync(true);

        var request = new { NewPassword = "SecurePassword123!" };
        var response = await _client.PostAsJsonAsync("/api/profile/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task UpdatePassword_MissingOldPasswordWhenHasPassword_ReturnsBadRequest()
    {
        var userId = "user123";
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId))
            .ReturnsAsync(new User { UserId = userId, PasswordHash = "hash", PasswordSalt = "salt" });

        var request = new { NewPassword = "SecurePassword123!" };
        var response = await _client.PostAsJsonAsync("/api/profile/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task UpdatePassword_TooShort_ReturnsBadRequest()
    {
        var userId = "user123";
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });

        var request = new { NewPassword = "short" }; // Below MinimumPasswordLength (8)
        var response = await _client.PostAsJsonAsync("/api/profile/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/profile/auth-method ──────────────────────────────────────

    [TestMethod]
    public async Task UpdateAuthMethod_ValidRequest_ReturnsOk()
    {
        var userId = "user123";
        var user = new User { UserId = userId, PasswordHash = "hash", PasswordSalt = "salt", Email = "user@test.com" };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.UpdateUserAsync(It.IsAny<User>())).ReturnsAsync(true);

        var request = new { NewAuthMethod = "Both" };
        var response = await _client.PostAsJsonAsync("/api/profile/auth-method", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task UpdateAuthMethod_InvalidMethod_ReturnsBadRequest()
    {
        var userId = "user123";
        var user = new User { UserId = userId };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var request = new { NewAuthMethod = "Invalid" };
        var response = await _client.PostAsJsonAsync("/api/profile/auth-method", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task UpdateAuthMethod_PasswordWithoutPasswordSet_ReturnsBadRequest()
    {
        var userId = "user123";
        var user = new User { UserId = userId, PasswordHash = "", PasswordSalt = "" };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var request = new { NewAuthMethod = "Password" };
        var response = await _client.PostAsJsonAsync("/api/profile/auth-method", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task UpdateAuthMethod_EmailWithoutEmailSet_ReturnsBadRequest()
    {
        var userId = "user123";
        var user = new User { UserId = userId, Email = "", PasswordHash = "hash", PasswordSalt = "salt" };
        _userRepoMock.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

        var request = new { NewAuthMethod = "Email" };
        var response = await _client.PostAsJsonAsync("/api/profile/auth-method", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
