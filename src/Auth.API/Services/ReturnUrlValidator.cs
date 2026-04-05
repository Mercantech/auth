using Microsoft.Extensions.Configuration;

namespace Auth.API.Services;

public class ReturnUrlValidator(IConfiguration configuration) : IReturnUrlValidator
{
    public bool IsValidOAuthAuthorizeReturnUrl(string returnUrl, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (returnUrl.StartsWith('/'))
            return returnUrl.StartsWith("/oauth/authorize", StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        return uri.AbsolutePath.StartsWith("/oauth/authorize", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsSafePostLoginReturnUrl(string returnUrl, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return true;
        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal))
            return true;
        return IsValidOAuthAuthorizeReturnUrl(returnUrl, request);
    }

    public bool IsSafePostLogoutRedirectUrl(string returnUrl, HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal))
            return true;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return IsSpaOriginAllowed(uri);
    }

    private bool IsSpaOriginAllowed(Uri absoluteUri)
    {
        var origin = $"{absoluteUri.Scheme}://{absoluteUri.Authority}";
        var allowed = configuration.GetSection("Cors:SpaOrigins").Get<string[]>() ?? [];
        foreach (var entry in allowed)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;
            if (!Uri.TryCreate(entry.Trim(), UriKind.Absolute, out var allowedUri))
                continue;
            var allowedOrigin = $"{allowedUri.Scheme}://{allowedUri.Authority}";
            if (string.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
