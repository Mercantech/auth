namespace Auth.API.Services;

public interface IReturnUrlValidator
{
    /// <summary>ReturnUrl til /oauth/authorize på samme host.</summary>
    bool IsValidOAuthAuthorizeReturnUrl(string returnUrl, HttpRequest request);

    /// <summary>Efter login: relativ sti på samme site eller authorize-URL på samme host.</summary>
    bool IsSafePostLoginReturnUrl(string returnUrl, HttpRequest request);

    /// <summary>Efter logud: relativ sti på auth-host eller absolut URL hvis origin står i Cors:SpaOrigins.</summary>
    bool IsSafePostLogoutRedirectUrl(string returnUrl, HttpRequest request);
}
