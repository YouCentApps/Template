using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using FluentAssertions;
using Moq;
using Template.Common.Models;
using Template.Common.Services.Auth;
using Template.Common.Services.Data;

namespace Template.Common.UnitTests.Services;

/// <summary>
/// Creates a fake AsyncPageable from an in-memory list so Azure Table Storage
/// query calls can be mocked without a real storage connection.
/// </summary>
file sealed class FakeAsyncPageable<T>(IEnumerable<T> items) : AsyncPageable<T>
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
public class RegistrationServiceTests
{
    private Mock<ITableClientFactory> _factoryMock = null!;
    private Mock<IUserRepository> _repoMock = null!;
    private Mock<TableClient> _pendingTableMock = null!;
    private Mock<TableClient> _otpTableMock = null!;
    private Mock<TableClient> _sessionTableMock = null!;
    private Mock<TableClient> _usersTableMock = null!;
    private List<string> _emailsSent = null!;
    private RegistrationService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _factoryMock = new Mock<ITableClientFactory>();
        _repoMock = new Mock<IUserRepository>();
        _pendingTableMock = new Mock<TableClient>();
        _otpTableMock = new Mock<TableClient>();
        _sessionTableMock = new Mock<TableClient>();
        _usersTableMock = new Mock<TableClient>();
        _emailsSent = [];

        _pendingTableMock.Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Response<TableItem>?)null);
        _otpTableMock.Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
                     .ReturnsAsync((Response<TableItem>?)null);
        _sessionTableMock.Setup(t => t.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Response<TableItem>?)null);

        _pendingTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(Mock.Of<Response>());
        _otpTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Mock.Of<Response>());
        _sessionTableMock.Setup(t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(Mock.Of<Response>());

        _factoryMock.Setup(f => f.GetTableClient("TemplatePendingReg")).Returns(_pendingTableMock.Object);
        _factoryMock.Setup(f => f.GetTableClient("TemplateOTPCodes")).Returns(_otpTableMock.Object);
        _factoryMock.Setup(f => f.GetTableClient("TemplateAuthSessions")).Returns(_sessionTableMock.Object);
        _factoryMock.Setup(f => f.GetTableClient("TemplateUsers")).Returns(_usersTableMock.Object);

        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _repoMock.Setup(r => r.GetUserByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        _sut = new RegistrationService(
            _factoryMock.Object,
            _repoMock.Object,
            sendEmailCallback: (email, msg) => _emailsSent.Add(email));
    }

    // ── StartRegistrationAsync – validation failures ───────────────────────────

    [TestMethod]
    public async Task StartRegistration_EmptyUsername_ReturnsFalse()
    {
        var (success, _, error) = await _sut.StartRegistrationAsync(
            "user@test.com", username: "", "Pass1!", "Both");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task StartRegistration_EmailRequiredForEmailAuth_ReturnsFalse()
    {
        var (success, _, error) = await _sut.StartRegistrationAsync(
            email: "", "johndoe", "Pass1!", "Email");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task StartRegistration_PasswordRequiredForPasswordAuth_ReturnsFalse()
    {
        var (success, _, error) = await _sut.StartRegistrationAsync(
            "user@test.com", "johndoe", password: "", "Password");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task StartRegistration_EmailAlreadyRegistered_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                 .ReturnsAsync(new User { UserId = "existing" });

        var (success, _, error) = await _sut.StartRegistrationAsync(
            "taken@test.com", "johndoe", "Pass1!", "Both");

        success.Should().BeFalse();
        error.Should().Contain("already");
    }

    [TestMethod]
    public async Task StartRegistration_UsernameAlreadyTaken_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByUsernameAsync(It.IsAny<string>()))
                 .ReturnsAsync(new User { UserId = "existing" });

        var (success, _, error) = await _sut.StartRegistrationAsync(
            "new@test.com", "takenname", "Pass1!", "Both");

        success.Should().BeFalse();
        error.Should().Contain("taken");
    }

    // ── StartRegistrationAsync – success ──────────────────────────────────────

    [TestMethod]
    public async Task StartRegistration_ValidInputs_ReturnsSuccessWithTempId()
    {
        var (success, tempUserId, error) = await _sut.StartRegistrationAsync(
            "new@test.com", "johndoe", "Pass1!", "Both");

        success.Should().BeTrue();
        tempUserId.Should().NotBeNullOrWhiteSpace();
        error.Should().BeEmpty();
    }

    [TestMethod]
    public async Task StartRegistration_Success_StoresPendingRegistration()
    {
        await _sut.StartRegistrationAsync("new@test.com", "johndoe", "Pass1!", "Both");

        _pendingTableMock.Verify(
            t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartRegistration_Success_StoresOTPCode()
    {
        await _sut.StartRegistrationAsync("new@test.com", "johndoe", "Pass1!", "Both");

        _otpTableMock.Verify(
            t => t.AddEntityAsync(It.IsAny<TableEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartRegistration_Success_SendsVerificationEmail()
    {
        await _sut.StartRegistrationAsync("new@test.com", "johndoe", "Pass1!", "Both");

        // The service passes the original (non-normalized) email to the callback
        _emailsSent.Should().ContainSingle().Which.Should().Be("new@test.com");
    }

    // ── CompleteRegistrationAsync – validation failures ───────────────────────

    [TestMethod]
    public async Task CompleteRegistration_NullTempUserId_ReturnsFalse()
    {
        var (success, _, _, _, error) = await _sut.CompleteRegistrationAsync(null!, "123456");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task CompleteRegistration_NoMatchingOTP_ReturnsFalse()
    {
        _otpTableMock.Setup(t => t.QueryAsync<TableEntity>(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(new FakeAsyncPageable<TableEntity>([]));

        var (success, _, _, _, error) = await _sut.CompleteRegistrationAsync("tempid", "000000");

        success.Should().BeFalse();
        error.Should().Contain("expired");
    }

    // ── RegisterDirectAsync – validation ─────────────────────────────────────

    [TestMethod]
    public async Task RegisterDirect_EmptyUsername_ReturnsFalse()
    {
        var (success, _, error) = await _sut.RegisterDirectAsync(
            "user@test.com", username: "", "Pass1!", "Password");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task RegisterDirect_PasswordRequiredForPasswordAuth_ReturnsFalse()
    {
        var (success, _, error) = await _sut.RegisterDirectAsync(
            "user@test.com", "johndoe", password: "", "Password");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task RegisterDirect_EmailAlreadyRegistered_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                 .ReturnsAsync(new User { UserId = "existing" });

        var (success, _, error) = await _sut.RegisterDirectAsync(
            "taken@test.com", "johndoe", "Pass1!", "Password");

        success.Should().BeFalse();
        error.Should().Contain("already");
    }

    [TestMethod]
    public async Task RegisterDirect_UsernameAlreadyTaken_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetUserByUsernameAsync(It.IsAny<string>()))
                 .ReturnsAsync(new User { UserId = "existing" });

        var (success, _, error) = await _sut.RegisterDirectAsync(
            "new@test.com", "takenname", "Pass1!", "Password");

        success.Should().BeFalse();
        error.Should().Contain("taken");
    }

    // ── RegisterDirectAsync – success ─────────────────────────────────────────

    [TestMethod]
    public async Task RegisterDirect_ValidInputs_CreatesUserAndReturnsSuccess()
    {
        _repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync(true);

        var (success, userId, error) = await _sut.RegisterDirectAsync(
            "new@test.com", "johndoe", "Pass1!", "Password");

        success.Should().BeTrue();
        userId.Should().NotBeNullOrWhiteSpace();
        error.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RegisterDirect_RepositoryFailure_ReturnsFalse()
    {
        _repoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>())).ReturnsAsync(false);

        var (success, _, error) = await _sut.RegisterDirectAsync(
            "new@test.com", "johndoe", "Pass1!", "Password");

        success.Should().BeFalse();
        error.Should().NotBeEmpty();
    }
}
