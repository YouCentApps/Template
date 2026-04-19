using System.Net;
using Template.Common.Services.State;

namespace Template.Common.Services.Api;

public class AuthenticationMessageHandler(AuthStateService authStateService) : DelegatingHandler
{
    private readonly AuthStateService _authStateService = authStateService;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isAuthEndpoint = request.RequestUri?.PathAndQuery.Contains("/api/auth/", StringComparison.Ordinal) ?? false;

        if (!isAuthEndpoint && !string.IsNullOrEmpty(_authStateService.SessionId))
        {
            request.Headers.Add("X-Session-Id", _authStateService.SessionId);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            await _authStateService.HandleSessionExpiredAsync().ConfigureAwait(false);
        }

        return response;
    }
}
