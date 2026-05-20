namespace Auth.API.Options;

public class MfaOptions
{
    public const string SectionName = "Mfa";

    /// <summary>Issuer-navn i authenticator-app (fx Mercantec Auth).</summary>
    public string IssuerName { get; set; } = "Mercantec Auth";

    /// <summary>Roller der skal have konfigureret 2FA før fuld session (tom = kun opt-in når 2FA er sat op).</summary>
    public string[] RequireForRoles { get; set; } = [];

    public int PendingSessionMinutes { get; set; } = 10;

    public int RecoveryCodeCount { get; set; } = 10;
}
