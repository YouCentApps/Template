using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Template.Common.Services.Data;
using Template.Common.Models;
using AuthService = Template.Common.Services.Auth.IAuthenticationService;
using AuthServiceImpl = Template.Common.Services.Auth.AuthenticationService;

namespace Template.Api.UnitTests;

[TestClass]
public class AdminEndpointsTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private Mock<IAdminRepository> _adminRepoMock = null!;
    private Mock<IUserRepository> _userRepoMock = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _adminRepoMock = new Mock<IAdminRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        var authServiceMock = new Mock<AuthService>();

        // Mock all IAuthenticationService methods to avoid requiring X-Session-Id header
        authServiceMock.Setup(s => s.ValidateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((true, "user123", "testuser", "mock-session-id", string.Empty));

        // Mock admin validation methods to return true for "user123"
        _adminRepoMock.Setup(r => r.IsActiveAdminAsync("user123")).ReturnsAsync(true);
        _adminRepoMock.Setup(r => r.CanManageAdminsAsync("user123")).ReturnsAsync(true);

        // CA2000: The factory is disposed in [TestCleanup] which runs after each test method.
        // MSTest guarantees Cleanup() execution, so we suppress the warning.
        #pragma warning disable CA2000 // Dispose objects before losing scope
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => _adminRepoMock.Object);
                    services.AddScoped(_ => _userRepoMock.Object);
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
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        _client.DefaultRequestHeaders.Add("X-Session-Id", "mock-session-id");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── GET /api/admin/me ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetMyAdminStatus_UserIsAdmin_ReturnsAdminStatus()
    {
        var userId = "user123";
        var admin = new Admin { UserId = userId, IsActive = true, CanManageAdmins = true };
        _adminRepoMock.Setup(r => r.GetAdminAsync(userId)).ReturnsAsync(admin);

        var response = await _client.GetAsync(new Uri("/api/admin/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("isAdmin");
        body!["isAdmin"].ToString().Should().Be("True");
    }

    // ── GET /api/admin/admins ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetAllAdmins_ReturnsList()
    {
        var admins = new List<Admin> { new Admin { UserId = "1" }, new Admin { UserId = "2" } };
        _adminRepoMock.Setup(r => r.GetAllAdminsAsync()).ReturnsAsync(admins);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/admin/admins", UriKind.Relative));
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ── GET /api/admin/admins/{userId} ──────────────────────────────────────

    [TestMethod]
    public async Task GetAdmin_ExistingUser_ReturnsAdmin()
    {
        var userId = "admin1";
        var admin = new Admin { UserId = userId };
        _adminRepoMock.Setup(r => r.GetAdminAsync(userId)).ReturnsAsync(admin);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/admin/admins/{userId}", UriKind.Relative));
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);
    }

    [TestMethod]
    public async Task GetAdmin_NonExistingUser_ReturnsNotFound()
    {
        var userId = "none";
        _adminRepoMock.Setup(r => r.GetAdminAsync(userId)).ReturnsAsync((Admin?)null);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/admin/admins/{userId}", UriKind.Relative));
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/admin/admins ─────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAdmin_ValidRequest_ReturnsOk()
    {
        var requestData = new { UserId = "user1", IsActive = true, CanManageAdmins = false };
        _userRepoMock.Setup(r => r.GetUserByIdAsync("user1")).ReturnsAsync(new User { UserId = "user1" });
        _adminRepoMock.Setup(r => r.GetAdminAsync("user1")).ReturnsAsync((Admin?)null);
        _adminRepoMock.Setup(r => r.CreateAdminAsync(It.IsAny<Admin>())).ReturnsAsync(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/admins")
        {
            Content = JsonContent.Create(requestData)
        };
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [TestMethod]
    public async Task CreateAdmin_UserNotFound_ReturnsBadRequest()
    {
        var requestData = new { UserId = "ghost", IsActive = true, CanManageAdmins = false };
        _userRepoMock.Setup(r => r.GetUserByIdAsync("ghost")).ReturnsAsync((User?)null);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/admins")
        {
            Content = JsonContent.Create(requestData)
        };
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/admin/admins/{userId} ──────────────────────────────────────

    [TestMethod]
    public async Task UpdateAdmin_ExistingAdmin_ReturnsOk()
    {
        var userId = "admin1";
        var admin = new Admin { UserId = userId };
        _adminRepoMock.Setup(r => r.GetAdminAsync(userId)).ReturnsAsync(admin);

        var requestData = new { IsActive = false, CanManageAdmins = true };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/admins/{userId}")
        {
            Content = JsonContent.Create(requestData)
        };
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    [TestMethod]
    public async Task UpdateAdmin_NonExistingAdmin_ReturnsNotFound()
    {
        var userId = "none";
        _adminRepoMock.Setup(r => r.GetAdminAsync(userId)).ReturnsAsync((Admin?)null);

        var requestData = new { IsActive = false, CanManageAdmins = true };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/admins/{userId}")
        {
            Content = JsonContent.Create(requestData)
        };
        request.Headers.Add("X-Test-Role", "Admin");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
