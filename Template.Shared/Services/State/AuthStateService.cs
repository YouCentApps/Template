namespace Template.Shared.Services.State;

public class AuthStateService
{
    private const string StorageKey = "template_session";
    private readonly IStorageService? _storageService;
    private bool _isHandlingExpiration = false;

    private string? _userId;
    private string? _username;
    private string? _sessionId;

    public event Action? OnAuthStateChanged;
    public event Func<Task>? OnSessionExpired;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_userId);
    public string? UserId => _userId;
    public string? Username => _username;
    public string? SessionId => _sessionId;

    public AuthStateService(IStorageService? storageService = null)
    {
        _storageService = storageService;
    }

    public async Task InitializeAsync()
    {
        if (_storageService == null)
            return;

        var session = await _storageService.GetItemAsync<SessionData>(StorageKey);
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
            await _storageService.SetItemAsync(StorageKey, session);
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
            await _storageService.RemoveItemAsync(StorageKey);
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
            await ClearAuthStateAsync();

            if (OnSessionExpired != null)
            {
                await OnSessionExpired.Invoke();
            }
        }
        finally
        {
            _isHandlingExpiration = false;
        }
    }

    private void NotifyStateChanged() => OnAuthStateChanged?.Invoke();
}
