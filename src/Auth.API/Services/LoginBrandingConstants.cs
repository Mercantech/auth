namespace Auth.API.Services;

public static class LoginBrandingConstants
{
    public const string ClientCookieName = "mercantec_login_client";
    public const string ContextItemKey = "LoginBranding";
    public static readonly TimeSpan ClientCookieLifetime = TimeSpan.FromMinutes(20);
}
