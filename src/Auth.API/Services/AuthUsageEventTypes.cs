namespace Auth.API.Services;

public static class AuthUsageEventTypes
{
    public const string ProviderLogin = "provider_login";
    public const string PasswordLogin = "password_login";
    public const string PasswordSignup = "password_signup";
    public const string PasswordLink = "password_link";
    public const string MfaTotpVerify = "mfa_totp_verify";
    public const string MfaRecoveryUsed = "mfa_recovery_used";
    public const string PasskeyAuth = "passkey_auth";
    public const string PasskeyRegister = "passkey_register";
    public const string OAuthAuthorize = "oauth_authorize";
    public const string OAuthTokenExchange = "oauth_token_exchange";
    public const string OAuthRefresh = "oauth_refresh";
    public const string AccountLink = "account_link";
}
