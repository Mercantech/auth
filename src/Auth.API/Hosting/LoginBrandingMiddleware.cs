using Auth.API.Services;

namespace Auth.API.Hosting;

/// <summary>Sætter OAuth login-branding på Account Login/Register/Mfa før Razor render.</summary>
public sealed class LoginBrandingMiddleware(RequestDelegate next)
{
    private static readonly string[] BrandedPaths =
    [
        "/Account/Login",
        "/Account/Register",
        "/Account/Mfa",
    ];

    public async Task InvokeAsync(HttpContext context, IClientLoginBrandingService branding)
    {
        if (IsBrandedPath(context.Request.Path))
        {
            var returnUrl = context.Request.Query["ReturnUrl"].FirstOrDefault()
                ?? context.Request.Query["returnUrl"].FirstOrDefault();
            var clientId = context.Request.Query["client_id"].FirstOrDefault();

            var resolved = await branding.ResolveAsync(
                context,
                returnUrl,
                clientId,
                context.RequestAborted);

            if (resolved.IsOAuthFlow && !string.IsNullOrEmpty(resolved.OAuthClientId))
                branding.SetClientCookie(context, resolved.OAuthClientId);
            else if (!resolved.IsOAuthFlow)
                branding.ClearClientCookie(context);

            context.Items[LoginBrandingConstants.ContextItemKey] = resolved;
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
        }

        await next(context);
    }

    private static bool IsBrandedPath(PathString path)
    {
        if (!path.HasValue)
            return false;

        var value = path.Value!;
        foreach (var branded in BrandedPaths)
        {
            if (value.Equals(branded, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public static class LoginBrandingMiddlewareExtensions
{
    public static IApplicationBuilder UseLoginBranding(this IApplicationBuilder app) =>
        app.UseMiddleware<LoginBrandingMiddleware>();
}
