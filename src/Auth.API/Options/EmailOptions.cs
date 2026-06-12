namespace Auth.API.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = true;
    public string SmtpHost { get; set; } = "smtp-relay.brevo.com";
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string FromAddress { get; set; } = "noreply@auth.mercantec.tech";
    public string FromName { get; set; } = "Mercantec Auth";
    public string? ReplyToAddress { get; set; }
    public string PublicBaseUrl { get; set; } = "https://auth.mercantec.tech";
    public int PasswordResetExpiryMinutes { get; set; } = 60;
    public int EmailConfirmExpiryHours { get; set; } = 48;
    public int RateLimitMaxAttempts { get; set; } = 3;
    public int RateLimitWindowMinutes { get; set; } = 15;
}
