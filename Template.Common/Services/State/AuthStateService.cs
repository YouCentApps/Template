namespace Template.Common.Services.State;

public delegate Task SessionExpiredCallback(object? sender, EventArgs e);

public class AuthStateService(IStorageService? storageService = null)
{
    private const string StorageKey = "template_session";
    private readonly IStorageService? _storageService = storageService;
    private bool _isHandlingExpiration;

    private string? _userId;
    private string? _username;
    private string? _sessionId;

    public event EventHandler? OnAuthStateChanged;

#pragma warning disable CA1003 // EventHandler<T> cannot return Task; a custom delegate is required for async event invocation
    public event SessionExpiredCallback? OnSessionExpired;
#pragma warning restore CA1003

    public bool IsAuthenticated => !string.IsNullOrEmpty(_userId);
    public string? UserId => _userId;
    public string? Username => _username;
    public string? SessionId => _sessionId;

    public async Task InitializeAsync()
    {
        if (_storageService == null)
            return;

        var session = await _storageService.GetItemAsync<SessionData>(StorageKey).ConfigureAwait(false);
        if (session != null && session.ExpiryDate > DateTime.UtcNow)
        {
            _userId = session.UserId;
            _username = session.Username;
            _sessionId = session.SessionId;
            NotifyStateChanged();
        }
    }

    public async Task SetAuthStateAsync(string userId, string username, string sessionId)
    {
        _userId = userId;
        _username = username;
        _sessionId = sessionId;

        if (_storageService != null)
        {
            var session = new SessionData
            {
                UserId = userId,
                Username = username,
                SessionId = sessionId,
                ExpiryDate = DateTime.UtcNow.AddDays(14)
            };
            await _storageService.SetItemAsync(StorageKey, session).ConfigureAwait(false);
        }

        NotifyStateChanged();
    }

    public async Task ClearAuthStateAsync()
    {
        _userId = null;
        _username = null;
        _sessionId = null;

        if (_storageService != null)
        {
            await _storageService.RemoveItemAsync(StorageKey).ConfigureAwait(false);
        }

        NotifyStateChanged();
    }

    public async Task HandleSessionExpiredAsync()
    {
        if (_isHandlingExpiration)
            return;

        _isHandlingExpiration = true;

        try
        {
            await ClearAuthStateAsync().ConfigureAwait(false);

            if (OnSessionExpired != null)
            {
                await OnSessionExpired.Invoke(this, EventArgs.Empty).ConfigureAwait(false);
            }
        }
        finally
        {
            _isHandlingExpiration = false;
        }
    }

    private void NotifyStateChanged() => OnAuthStateChanged?.Invoke(this, EventArgs.Empty);
}
