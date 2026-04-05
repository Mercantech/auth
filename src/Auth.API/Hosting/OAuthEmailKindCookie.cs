using Auth.API.Models;

namespace Auth.API.Hosting;

public static class OAuthEmailKindCookie
{
    public const string CookieName = "MercantecOAuthEmailKind";

    public static void Append(HttpContext httpContext, UserEmailKind kind)
    {
        httpContext.Response.Cookies.Append(CookieName, ((int)kind).ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/",
        });
    }

    public static UserEmailKind ReadAndClear(HttpContext httpContext)
    {
        var raw = httpContext.Request.Cookies[CookieName];
        httpContext.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });

        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var n) || !Enum.IsDefined(typeof(UserEmailKind), n))
            return UserEmailKind.Personal;

        return (UserEmailKind)n;
    }

    public static UserEmailKind ParseQuery(string? emailKind) =>
        emailKind?.Trim().ToLowerInvariant() switch
        {
            "work" => UserEmailKind.Work,
            "school" => UserEmailKind.School,
            "personal" or "mail" or "" or null => UserEmailKind.Personal,
            _ => UserEmailKind.Personal,
        };
}
