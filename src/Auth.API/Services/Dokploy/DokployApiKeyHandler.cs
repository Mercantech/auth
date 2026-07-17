using Auth.API.Options;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployApiKeyHandler(IOptions<DokployOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var key = options.Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(key))
            request.Headers.TryAddWithoutValidation("x-api-key", key);

        return base.SendAsync(request, cancellationToken);
    }
}
