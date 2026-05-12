using FluentAssertions;
using Moq;
using Template.Common.Services.State;

namespace Template.Common.UnitTests.Services;

[TestClass]
public class AuthStateServiceTests
{
    private Mock<IStorageService> _storageMock = null!;
    private AuthStateService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _storageMock = new Mock<IStorageService>();
        _sut = new AuthStateService(_storageMock.Object);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [TestMethod]
    public void InitialState_IsNotAuthenticated()
    {
        _sut.IsAuthenticated.Should().BeFalse();
        _sut.UserId.Should().BeNull();
        _sut.Username.Should().BeNull();
        _sut.SessionId.Should().BeNull();
    }

    // ── SetAuthStateAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task SetAuthStateAsync_SetsAllProperties()
    {
        await _sut.SetAuthStateAsync("user1", "johndoe", "session123");

        _sut.IsAuthenticated.Should().BeTrue();
        _sut.UserId.Should().Be("user1");
        _sut.Username.Should().Be("johndoe");
        _sut.SessionId.Should().Be("session123");
    }

    [TestMethod]
    public async Task SetAuthStateAsync_PersistsToStorage()
    {
        await _sut.SetAuthStateAsync("user1", "johndoe", "session123");

        _storageMock.Verify(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<SessionData>()), Times.Once);
    }

    [TestMethod]
    public async Task SetAuthStateAsync_RaisesOnAuthStateChanged()
    {
        var raised = false;
        _sut.OnAuthStateChanged += (_, _) => raised = true;

        await _sut.SetAuthStateAsync("user1", "johndoe", "session123");

        raised.Should().BeTrue();
    }

    // ── ClearAuthStateAsync ───────────────────────────────────────────────────

    [TestMethod]
    public async Task ClearAuthStateAsync_ResetsAllProperties()
    {
        await _sut.SetAuthStateAsync("user1", "johndoe", "session123");
        await _sut.ClearAuthStateAsync();

        _sut.IsAuthenticated.Should().BeFalse();
        _sut.UserId.Should().BeNull();
        _sut.Username.Should().BeNull();
        _sut.SessionId.Should().BeNull();
    }

    [TestMethod]
    public async Task ClearAuthStateAsync_RemovesFromStorage()
    {
        await _sut.ClearAuthStateAsync();

        _storageMock.Verify(s => s.RemoveItemAsync(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task ClearAuthStateAsync_RaisesOnAuthStateChanged()
    {
        var raised = false;
        _sut.OnAuthStateChanged += (_, _) => raised = true;

        await _sut.ClearAuthStateAsync();

        raised.Should().BeTrue();
    }

    // ── InitializeAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task InitializeAsync_ValidNonExpiredSession_RestoresAuthState()
    {
        var session = new SessionData
        {
            UserId = "user1",
            Username = "johndoe",
            SessionId = "session123",
            ExpiryDate = DateTime.UtcNow.AddDays(1)
        };
        _storageMock.Setup(s => s.GetItemAsync<SessionData>(It.IsAny<string>()))
                    .ReturnsAsync(session);

        await _sut.InitializeAsync();

        _sut.IsAuthenticated.Should().BeTrue();
        _sut.UserId.Should().Be("user1");
        _sut.Username.Should().Be("johndoe");
        _sut.SessionId.Should().Be("session123");
    }

    [TestMethod]
    public async Task InitializeAsync_ExpiredSession_DoesNotRestoreAuthState()
    {
        var session = new SessionData
        {
            UserId = "user1",
            Username = "johndoe",
            SessionId = "session123",
            ExpiryDate = DateTime.UtcNow.AddDays(-1)   // already expired
        };
        _storageMock.Setup(s => s.GetItemAsync<SessionData>(It.IsAny<string>()))
                    .ReturnsAsync(session);

        await _sut.InitializeAsync();

        _sut.IsAuthenticated.Should().BeFalse();
    }

    [TestMethod]
    public async Task InitializeAsync_NoStoredSession_RemainsUnauthenticated()
    {
        _storageMock.Setup(s => s.GetItemAsync<SessionData>(It.IsAny<string>()))
                    .ReturnsAsync((SessionData?)null);

        await _sut.InitializeAsync();

        _sut.IsAuthenticated.Should().BeFalse();
    }

    // ── HandleSessionExpiredAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task HandleSessionExpiredAsync_ClearsAuthStateAndFiresCallback()
    {
        await _sut.SetAuthStateAsync("user1", "johndoe", "session123");

        var callbackFired = false;
        _sut.OnSessionExpired += (_, _) =>
        {
            callbackFired = true;
            return Task.CompletedTask;
        };

        await _sut.HandleSessionExpiredAsync();

        _sut.IsAuthenticated.Should().BeFalse();
        callbackFired.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleSessionExpiredAsync_WhileHandling_DoesNotReenter()
    {
        var callCount = 0;
        _sut.OnSessionExpired += async (_, _) =>
        {
            callCount++;
            // attempt re-entrant call
            await _sut.HandleSessionExpiredAsync();
        };

        await _sut.HandleSessionExpiredAsync();

        callCount.Should().Be(1);
    }

    // ── No storage ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SetAuthStateAsync_WithoutStorage_SetsStateInMemory()
    {
        var sut = new AuthStateService(storageService: null);

        await sut.SetAuthStateAsync("user1", "johndoe", "session123");

        sut.IsAuthenticated.Should().BeTrue();
    }

    [TestMethod]
    public async Task InitializeAsync_WithoutStorage_RemainsUnauthenticated()
    {
        var sut = new AuthStateService(storageService: null);

        await sut.InitializeAsync();

        sut.IsAuthenticated.Should().BeFalse();
    }
}
