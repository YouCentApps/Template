using System.Net;
using Template.Shared.Services.State;

namespace Template.Shared.Services.Api;

public class AuthenticationMessageHandler : DelegatingHandler
{
    private readonly AuthStateService _authStateService;

    public AuthenticationMessageHandler(AuthStateService authStateService)
    {
        _authStateService = authStateService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isAuthEndpoint = request.RequestUri?.PathAndQuery.Contains("/api/auth/") ?? false;

        if (!isAuthEndpoint && !string.IsNullOrEmpty(_authStateService.SessionId))
        {
            request.Headers.Add("X-Session-Id", _authStateService.SessionId);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            await _authStateService.HandleSessionExpiredAsync();
        }

        return response;
    }
}
