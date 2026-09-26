using System.Net;
using System.Net.Http.Headers;

namespace ProductShop.Client.Services;

// Protiti API request e token lagay; token er meyad shesh (401) hole logout kore
public class AuthHandler : DelegatingHandler
{
    private readonly AuthState _state;

    public AuthHandler(AuthState state)
    {
        _state = state;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_state.Token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _state.Token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            _state.Clear();

        return response;
    }
}
