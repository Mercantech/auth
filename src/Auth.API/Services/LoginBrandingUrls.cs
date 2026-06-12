namespace Auth.API.Services;

public static class LoginBrandingUrls
{
    public static string? ClientIdFromContext(HttpContext http)
    {
        var fromQuery = http.Request.Query["client_id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromQuery))
            return fromQuery.Trim();

        return http.Request.Cookies.TryGetValue(LoginBrandingConstants.ClientCookieName, out var cookie)
            && !string.IsNullOrWhiteSpace(cookie)
            ? cookie.Trim()
            : null;
    }

    public static string ClientIdFromReturnUrlOrCookie(string? returnUrl, HttpContext? http = null)
    {
        var fromReturn = ClientLoginBrandingService.TryParseClientIdFromReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(fromReturn))
            return fromReturn;

        if (http is not null)
        {
            var fromCtx = ClientIdFromContext(http);
            if (!string.IsNullOrWhiteSpace(fromCtx))
                return fromCtx;
        }

        return string.Empty;
    }

    public static string Login(string? returnUrl = null, string? error = null, string? clientId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(returnUrl))
            q.Add($"ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        AppendClientId(q, clientId);
        return q.Count == 0 ? "/Account/Login" : $"/Account/Login?{string.Join('&', q)}";
    }

    public static string Register(string? returnUrl = null, string? error = null, string? clientId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(returnUrl))
            q.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        AppendClientId(q, clientId);
        return q.Count == 0 ? "/Account/Register" : $"/Account/Register?{string.Join('&', q)}";
    }

    public static string Mfa(string returnUrl, string? clientId = null, string? error = null)
    {
        var q = new List<string> { $"returnUrl={Uri.EscapeDataString(returnUrl)}" };
        AppendClientId(q, clientId);
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        return $"/Account/Mfa?{string.Join('&', q)}";
    }

    public static string LinkRequired(string returnUrl, string? clientId = null, string? error = null)
    {
        var q = new List<string> { $"returnUrl={Uri.EscapeDataString(returnUrl)}" };
        AppendClientId(q, clientId);
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        return $"/Account/LinkRequired?{string.Join('&', q)}";
    }

    public static string AuthorizeLoginRedirect(string authorizePathAndQuery, string clientId) =>
        Login(authorizePathAndQuery, clientId: clientId);

    public static string ForgotPassword(string? returnUrl = null, string? error = null, string? clientId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(returnUrl))
            q.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        AppendClientId(q, clientId);
        return q.Count == 0 ? "/Account/ForgotPassword" : $"/Account/ForgotPassword?{string.Join('&', q)}";
    }

    public static string ResetPassword(string? token = null, string? error = null, string? clientId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(token))
            q.Add($"token={Uri.EscapeDataString(token)}");
        if (!string.IsNullOrWhiteSpace(error))
            q.Add($"error={Uri.EscapeDataString(error)}");
        AppendClientId(q, clientId);
        return q.Count == 0 ? "/Account/ResetPassword" : $"/Account/ResetPassword?{string.Join('&', q)}";
    }

    public static string ConfirmEmailSent(string? returnUrl = null, string? email = null, string? status = null, string? clientId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(returnUrl))
            q.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
        if (!string.IsNullOrWhiteSpace(email))
            q.Add($"email={Uri.EscapeDataString(email)}");
        if (!string.IsNullOrWhiteSpace(status))
            q.Add($"status={Uri.EscapeDataString(status)}");
        AppendClientId(q, clientId);
        return q.Count == 0 ? "/Account/ConfirmEmailSent" : $"/Account/ConfirmEmailSent?{string.Join('&', q)}";
    }

    private static void AppendClientId(List<string> q, string? clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
            q.Add($"client_id={Uri.EscapeDataString(clientId.Trim())}");
    }
}
