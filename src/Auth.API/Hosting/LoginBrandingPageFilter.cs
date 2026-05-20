using Auth.API.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.API.Hosting;

/// <summary>OAuth login-branding på Login, Register og Mfa.</summary>
public sealed class LoginBrandingPageFilter(IClientLoginBrandingService branding) : IAsyncPageFilter
{
    private static readonly HashSet<string> BrandedPageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/Register",
        "/Account/Mfa",
    };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.HandlerInstance is not PageModel)
        {
            await next();
            return;
        }

        var pagePath = ResolvePagePath(context);
        if (pagePath is null || !BrandedPageNames.Contains(pagePath))
        {
            await next();
            return;
        }

        var returnUrl = context.HttpContext.Request.Query["ReturnUrl"].FirstOrDefault()
            ?? context.HttpContext.Request.Query["returnUrl"].FirstOrDefault();
        var clientId = context.HttpContext.Request.Query["client_id"].FirstOrDefault();

        var resolved = await branding.ResolveAsync(
            context.HttpContext,
            returnUrl,
            clientId,
            context.HttpContext.RequestAborted);

        if (resolved.IsOAuthFlow && !string.IsNullOrEmpty(resolved.OAuthClientId))
            branding.SetClientCookie(context.HttpContext, resolved.OAuthClientId);
        else if (!resolved.IsOAuthFlow)
            branding.ClearClientCookie(context.HttpContext);

        context.HttpContext.Items[LoginBrandingConstants.ContextItemKey] = resolved;

        await next();
    }

    private static string? ResolvePagePath(PageHandlerExecutingContext context)
    {
        if (context.ActionDescriptor.RouteValues.TryGetValue("page", out var page) && !string.IsNullOrEmpty(page))
            return "/" + page.TrimStart('/');

        var path = context.HttpContext.Request.Path.Value;
        return string.IsNullOrEmpty(path) ? null : path;
    }
}

public static class LoginBrandingHttpExtensions
{
    public static LoginBrandingContext? GetLoginBranding(this HttpContext http) =>
        http.Items.TryGetValue(LoginBrandingConstants.ContextItemKey, out var value) && value is LoginBrandingContext ctx
            ? ctx
            : null;
}
