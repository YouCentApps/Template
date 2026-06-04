using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using FluentAssertions;
using Moq;
using Template.Common.Helpers;
using Template.Common.Models;
using Template.Common.Services.Auth;
using Template.Common.Services.Data;

namespace Template.Common.UnitTests.Services;

/// <summary>
/// Creates a fake AsyncPageable from an in-memory list so Azure Table Storage
/// query calls can be mocked without a real storage connection.
/// </summary>
file sealed class FakeOtpAsyncPageable<T>(IEnumerable<T> items) : AsyncPageable<T>
    where T : notnull
{
    private readonly List<T> _items = [.. items];

    public override async IAsyncEnumerable<Page<T>> AsPages(
        string? continuationToken = null, int? pageSizeHint = null)
    {
        yield return Page<T>.FromValues(_items, continuationToken: null, response: null!);
        await Task.CompletedTask;
    }
}

[TestClass]
public class AuthenticationServiceTests
{
    private Mock<ITableClientFactory> _factoryMock = null!;
    private Mock<IUserRepository> _repoMock = null!;
    private Mock<TableClient> _otpTableMock = null!;
    private Mock<TableClient> _sessionTableMock = null!;
    private List<string> _emailsSent = null!;
    private List<string> _emailBodies = null!;
    private AuthenticationService _sut = null!;

    private static readonly User ActiveUser = new()
    {
        UserId = "user1",
        Email = "USER@TEST.COM",
        Username = "johndoe",
        NormalizedUsername = "JOHNDOE",
        PasswordHash = string.Empty,
        PasswordSalt = string.Empty,
        IsActive = true
    };

    [TestInitialize]
    public void Setup()
    {
        _factoryMock = new Mock<ITableClientFactory>();
        _repoMock = new Mock<IUserRepository>();
        _otpTableMock = new Mock<TableClient>();
        _sessionTableMock = new Mock<TableClient>();
        _emailsSent = [];
        _emailBodies = [];

        _otpTableMock.Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Response<TableItem>?)null);
        _sessionTableMock.Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Response<TableItem>?)null);
        _otpTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Mock.Of<Response>());
        _sessionTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(Mock.Of<Response>());

        _factoryMock.Setup(f => f.GetTableClient("TemplateOTPCodes")).Returns(_otpTableMock.Object);
        _factoryMock.Setup(f => f.GetTableClient("TemplateAuthSessions")).Returns(_sessionTableMock.Object);

        _sut = new AuthenticationService(
            _factoryMock.Object,
            _repoMock.Object,
            sendEmailCallback: (email, body) => { _emailsSent.Add(email); _emailBodies.Add(body); });
    }

    // ── SendOTPAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendOTP_EmptyInput_ReturnsFalse()
    {
        var (success, _, error) = await _sut.SendOTPAsync(string.Empty);

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task SendOTP_UserNotFound_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _repoMock.Setup(r => r.GetUserByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var (success, _, error) = await _sut.SendOTPAsync("unknown@test.com");

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    [TestMethod]
    public async Task SendOTP_InactiveUser_ReturnsFalse()
    {
        var inactiveUser = new User
        {
            UserId = ActiveUser.UserId, Email = ActiveUser.Email, Username = ActiveUser.Username,
            NormalizedUsername = ActiveUser.NormalizedUsername, IsActive = false
        };
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(inactiveUser);

        var (success, _, error) = await _sut.SendOTPAsync("user@test.com");

        success.Should().BeFalse();
        error.Should().Contain("inactive");
    }

    [TestMethod]
    public async Task SendOTP_ValidEmail_ReturnsSuccessAndSendsEmail()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(ActiveUser);

        var (success, actualEmail, error) = await _sut.SendOTPAsync("user@test.com");

        success.Should().BeTrue();
        actualEmail.Should().Be(ActiveUser.Email);
        error.Should().BeEmpty();
        _emailsSent.Should().ContainSingle();
    }

    [TestMethod]
    public async Task SendOTP_EmailBodyContainsOtpCode()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(ActiveUser);

        await _sut.SendOTPAsync("user@test.com");

        _emailBodies.Should().ContainSingle();
        _emailBodies[0].Should().MatchRegex(@"\b\d{6}\b",
            because: "the email body should contain a 6-digit OTP code");
    }

    [TestMethod]
    public async Task SendOTP_OtpTableAddFails_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(ActiveUser);
        _otpTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new RequestFailedException(500, "storage failure"));

        var (success, _, error) = await _sut.SendOTPAsync("user@test.com");

        success.Should().BeFalse();
        error.Should().Contain("Failed to send OTP");
        _emailsSent.Should().BeEmpty(because: "no email should be sent when storage write fails");
    }

    [TestMethod]
    public async Task SendOTP_ValidUsername_LooksUpByUsername()
    {
        _repoMock.Setup(r => r.GetUserByUsernameAsync(It.IsAny<string>())).ReturnsAsync(ActiveUser);

        var (success, _, _) = await _sut.SendOTPAsync("johndoe");

        success.Should().BeTrue();
        _repoMock.Verify(r => r.GetUserByUsernameAsync(It.IsAny<string>()), Times.Once);
        _repoMock.Verify(r => r.GetUserByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    // ── VerifyOTPAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task VerifyOTP_NoMatchingOtp_ReturnsFalse()
    {
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeOtpAsyncPageable<TableEntity>([]));

        var (success, _, _, _, error) = await _sut.VerifyOTPAsync("user@test.com", "000000");

        success.Should().BeFalse();
        error.Should().Contain("Invalid or expired");
    }

    [TestMethod]
    public async Task VerifyOTP_WrongCode_ReturnsFalse()
    {
        var otp = new TableEntity("OTP", "row1")
        {
            ["UserId"] = ActiveUser.UserId,
            ["Code"] = "123456",
            ["Email"] = ActiveUser.Email,
            ["IsUsed"] = false,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10)
        };
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeOtpAsyncPageable<TableEntity>([otp]));

        var (success, _, _, _, error) = await _sut.VerifyOTPAsync("user@test.com", "999999");

        success.Should().BeFalse();
        error.Should().Contain("Invalid or expired");
    }

    [TestMethod]
    public async Task VerifyOTP_ValidCode_CreatesSessionAndReturnsUserInfo()
    {
        var otp = new TableEntity("OTP", "row1")
        {
            ["UserId"] = ActiveUser.UserId,
            ["Code"] = "123456",
            ["Email"] = ActiveUser.Email,
            ["IsUsed"] = false,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10)
        };
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeOtpAsyncPageable<TableEntity>([otp]));
        _repoMock.Setup(r => r.GetUserByIdAsync(ActiveUser.UserId)).ReturnsAsync(ActiveUser);

        var (success, userId, username, sessionId, error) =
            await _sut.VerifyOTPAsync("user@test.com", "123456");

        success.Should().BeTrue();
        userId.Should().Be(ActiveUser.UserId);
        username.Should().Be(ActiveUser.Username);
        sessionId.Should().NotBeNullOrWhiteSpace();
        error.Should().BeEmpty();

        _repoMock.Verify(r => r.UpdateLastLoginAsync(ActiveUser.UserId), Times.Once);
        _sessionTableMock.Verify(
            t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task VerifyOTP_InactiveUser_ReturnsFalse()
    {
        var inactiveUser = new User
        {
            UserId = ActiveUser.UserId,
            Email = ActiveUser.Email,
            Username = ActiveUser.Username,
            NormalizedUsername = ActiveUser.NormalizedUsername,
            IsActive = false
        };
        var otp = new TableEntity("OTP", "row1")
        {
            ["UserId"] = inactiveUser.UserId,
            ["Code"] = "123456",
            ["Email"] = inactiveUser.Email,
            ["IsUsed"] = false,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10)
        };
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeOtpAsyncPageable<TableEntity>([otp]));
        _repoMock.Setup(r => r.GetUserByIdAsync(inactiveUser.UserId)).ReturnsAsync(inactiveUser);

        var (success, _, _, _, error) = await _sut.VerifyOTPAsync("user@test.com", "123456");

        success.Should().BeFalse();
        error.Should().Contain("inactive");
        _sessionTableMock.Verify(
            t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task VerifyOTP_UserNotFoundById_ReturnsFalse()
    {
        var otp = new TableEntity("OTP", "row1")
        {
            ["UserId"] = "ghost-user",
            ["Code"] = "123456",
            ["Email"] = "ghost@test.com",
            ["IsUsed"] = false,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10)
        };
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeOtpAsyncPageable<TableEntity>([otp]));
        _repoMock.Setup(r => r.GetUserByIdAsync("ghost-user")).ReturnsAsync((User?)null);

        var (success, _, _, _, error) = await _sut.VerifyOTPAsync("ghost@test.com", "123456");

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── SignInWithPasswordAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task SignIn_EmptyCredentials_ReturnsFalse()
    {
        var (success, _, _, _, error) = await _sut.SignInWithPasswordAsync(string.Empty, string.Empty);

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task SignIn_UserNotFound_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var (success, _, _, _, error) = await _sut.SignInWithPasswordAsync("ghost@test.com", "Pass1!");

        success.Should().BeFalse();
        error.Should().Contain("Invalid");
    }

    [TestMethod]
    public async Task SignIn_InactiveUser_ReturnsFalse()
    {
        var inactiveUser = new User
        {
            UserId = ActiveUser.UserId, Email = ActiveUser.Email, Username = ActiveUser.Username,
            NormalizedUsername = ActiveUser.NormalizedUsername, IsActive = false
        };
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(inactiveUser);

        var (success, _, _, _, error) = await _sut.SignInWithPasswordAsync("user@test.com", "Pass1!");

        success.Should().BeFalse();
        error.Should().Contain("inactive");
    }

    [TestMethod]
    public async Task SignIn_WrongPassword_ReturnsFalse()
    {
        var salt = PasswordHelper.GenerateSalt();
        var user = new User
        {
            UserId = ActiveUser.UserId, Email = ActiveUser.Email, Username = ActiveUser.Username,
            NormalizedUsername = ActiveUser.NormalizedUsername, IsActive = true,
            PasswordHash = PasswordHelper.HashPassword("CorrectPass1!", salt),
            PasswordSalt = salt
        };
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var (success, _, _, _, error) = await _sut.SignInWithPasswordAsync("user@test.com", "WrongPass1!");

        success.Should().BeFalse();
        error.Should().Contain("Invalid");
    }

    [TestMethod]
    public async Task SignIn_CorrectPassword_ReturnsSuccessWithSessionId()
    {
        var salt = PasswordHelper.GenerateSalt();
        var user = new User
        {
            UserId = ActiveUser.UserId, Email = ActiveUser.Email, Username = ActiveUser.Username,
            NormalizedUsername = ActiveUser.NormalizedUsername, IsActive = true,
            PasswordHash = PasswordHelper.HashPassword("CorrectPass1!", salt),
            PasswordSalt = salt
        };
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _repoMock.Setup(r => r.UpdateLastLoginAsync(It.IsAny<string>())).ReturnsAsync(true);

        var (success, userId, username, sessionId, error) =
            await _sut.SignInWithPasswordAsync("user@test.com", "CorrectPass1!");

        success.Should().BeTrue();
        userId.Should().Be(ActiveUser.UserId);
        username.Should().Be(ActiveUser.Username);
        sessionId.Should().NotBeNullOrWhiteSpace();
        error.Should().BeEmpty();
    }

    // ── ValidateSessionAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task ValidateSession_SessionNotFound_ReturnsFalse()
    {
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        var (success, _, _, _, error) = await _sut.ValidateSessionAsync("invalid-session");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task ValidateSession_ExpiredSession_ReturnsFalse()
    {
        var session = new TableEntity("Session", "sid")
        {
            ["UserId"] = "user1",
            ["Username"] = "johndoe",
            ["IsActive"] = true,
            ["ExpiryDate"] = DateTimeOffset.UtcNow.AddDays(-1)   // expired
        };
        var responseMock = Mock.Of<Response<TableEntity>>(r => r.Value == session);
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock);

        var (success, _, _, _, error) = await _sut.ValidateSessionAsync("expired-session");

        success.Should().BeFalse();
        error.Should().Contain("expired");
    }

    [TestMethod]
    public async Task ValidateSession_InactiveSession_ReturnsFalse()
    {
        var session = new TableEntity("Session", "sid")
        {
            ["UserId"] = "user1",
            ["Username"] = "johndoe",
            ["IsActive"] = false,
            ["ExpiryDate"] = DateTimeOffset.UtcNow.AddDays(1)
        };
        var responseMock = Mock.Of<Response<TableEntity>>(r => r.Value == session);
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock);

        var (success, _, _, _, error) = await _sut.ValidateSessionAsync("inactive-session");

        success.Should().BeFalse();
        error.Should().Contain("expired");
    }

    [TestMethod]
    public async Task ValidateSession_ValidSession_ReturnsUserInfo()
    {
        var session = new TableEntity("Session", "sid")
        {
            ["UserId"] = "user1",
            ["Username"] = "johndoe",
            ["IsActive"] = true,
            ["ExpiryDate"] = DateTimeOffset.UtcNow.AddDays(30)
        };
        var responseMock = Mock.Of<Response<TableEntity>>(r => r.Value == session);
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock);

        var (success, userId, username, sessionId, error) = await _sut.ValidateSessionAsync("valid-session");

        success.Should().BeTrue();
        userId.Should().Be("user1");
        username.Should().Be("johndoe");
        sessionId.Should().Be("valid-session");
        error.Should().BeEmpty();
    }

    // ── SignOutAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SignOut_ValidSession_MarksSessionInactive()
    {
        var session = new TableEntity("Session", "sid")
        {
            ["IsActive"] = true
        };
        var responseMock = Mock.Of<Response<TableEntity>>(r => r.Value == session);
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock);
        _sessionTableMock
            .Setup(t => t.UpdateEntityAsync(
                It.IsAny<TableEntity>(), It.IsAny<ETag>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response>());

        await _sut.SignOutAsync("valid-session");

        _sessionTableMock.Verify(
            t => t.UpdateEntityAsync(
                It.Is<TableEntity>(e => e.GetBoolean("IsActive") == false),
                It.IsAny<ETag>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SignOut_SessionNotFound_DoesNotThrow()
    {
        _sessionTableMock
            .Setup(t => t.GetEntityAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));

        await _sut.Invoking(s => s.SignOutAsync("ghost-session"))
                  .Should().NotThrowAsync();
    }
}
