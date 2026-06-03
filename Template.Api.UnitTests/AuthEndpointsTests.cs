using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Template.Common.Services.Auth;

namespace Template.Api.UnitTests;

[TestClass]
public class AuthEndpointsTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private Mock<IAuthenticationService> _authServiceMock = null!;
    private Mock<IRegistrationService> _registrationServiceMock = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _authServiceMock = new Mock<IAuthenticationService>();
        _registrationServiceMock = new Mock<IRegistrationService>();

        // CA2000: The factory is disposed in [TestCleanup] which runs after each test method.
        // MSTest guarantees Cleanup() execution, so we suppress the warning.
        #pragma warning disable CA2000 // Dispose objects before losing scope
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace real services with mocks
                    services.AddScoped(_ => _authServiceMock.Object);
                    services.AddScoped(_ => _registrationServiceMock.Object);
                });
            });
        #pragma warning restore CA2000

        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── POST /api/auth/signin ─────────────────────────────────────────────────

    [TestMethod]
    public async Task SignIn_ValidCredentials_Returns200WithSessionInfo()
    {
        _authServiceMock
            .Setup(s => s.SignInWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, "user1", "johndoe", "session123", string.Empty));

        var response = await _client.PostAsJsonAsync("/api/auth/signin",
            new { emailOrUsername = "user@test.com", password = "Pass1!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("sessionId");
        body.Should().ContainKey("userId");
    }

    [TestMethod]
    public async Task SignIn_InvalidCredentials_Returns400WithError()
    {
        _authServiceMock
            .Setup(s => s.SignInWithPasswordAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, string.Empty, string.Empty, "Invalid credentials"));

        var response = await _client.PostAsJsonAsync("/api/auth/signin",
            new { emailOrUsername = "user@test.com", password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("error");
    }

    // ── POST /api/auth/send-otp ───────────────────────────────────────────────

    [TestMethod]
    public async Task SendOTP_KnownUser_Returns200()
    {
        _authServiceMock
            .Setup(s => s.SendOTPAsync(It.IsAny<string>()))
            .ReturnsAsync((true, "user@test.com", string.Empty));

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp",
            new { emailOrUsername = "user@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task SendOTP_UnknownUser_Returns400()
    {
        _authServiceMock
            .Setup(s => s.SendOTPAsync(It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, "User not found"));

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp",
            new { emailOrUsername = "ghost@test.com" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/verify-otp ─────────────────────────────────────────────

    [TestMethod]
    public async Task VerifyOTP_ValidCode_Returns200WithSessionInfo()
    {
        _authServiceMock
            .Setup(s => s.VerifyOTPAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, "user1", "johndoe", "session123", string.Empty));

        var response = await _client.PostAsJsonAsync("/api/auth/verify-otp",
            new { email = "user@test.com", otpCode = "123456" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("sessionId");
    }

    [TestMethod]
    public async Task VerifyOTP_InvalidCode_Returns400()
    {
        _authServiceMock
            .Setup(s => s.VerifyOTPAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, string.Empty, string.Empty, "Invalid or expired code"));

        var response = await _client.PostAsJsonAsync("/api/auth/verify-otp",
            new { email = "user@test.com", otpCode = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/validate-session ──────────────────────────────────────

    [TestMethod]
    public async Task ValidateSession_ValidSession_Returns200()
    {
        _authServiceMock
            .Setup(s => s.ValidateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((true, "user1", "johndoe", "session123", string.Empty));

        var response = await _client.PostAsJsonAsync("/api/auth/validate-session",
            new { sessionId = "session123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task ValidateSession_InvalidSession_Returns401()
    {
        _authServiceMock
            .Setup(s => s.ValidateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, string.Empty, string.Empty, "Session expired"));

        var response = await _client.PostAsJsonAsync("/api/auth/validate-session",
            new { sessionId = "bad-session" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/auth/signout ────────────────────────────────────────────────

    [TestMethod]
    public async Task SignOut_AnySession_Returns200()
    {
        _authServiceMock
            .Setup(s => s.SignOutAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsJsonAsync("/api/auth/signout",
            new { sessionId = "session123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/auth/register/send-verification ─────────────────────────────

    [TestMethod]
    public async Task StartRegistration_ValidInputs_Returns200WithTempUserId()
    {
        _registrationServiceMock
            .Setup(s => s.StartRegistrationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, "tempid123", string.Empty));

        var response = await _client.PostAsJsonAsync("/api/auth/register/send-verification",
            new { email = "new@test.com", username = "johndoe", password = "Pass1!", preferredAuthMethod = "Both" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("tempUserId");
    }

    [TestMethod]
    public async Task StartRegistration_DuplicateEmail_Returns400()
    {
        _registrationServiceMock
            .Setup(s => s.StartRegistrationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, string.Empty, "Email already registered"));

        var response = await _client.PostAsJsonAsync("/api/auth/register/send-verification",
            new { email = "taken@test.com", username = "johndoe", password = "Pass1!", preferredAuthMethod = "Both" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/health ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task HealthCheck_Returns200WithHealthyStatus()
    {
        var response = await _client.GetAsync(new Uri("/api/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("status");
        body!["status"].ToString().Should().Be("healthy");
    }
}
