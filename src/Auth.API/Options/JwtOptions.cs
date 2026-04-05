namespace Auth.API.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://auth.mercantec.tech";
    public string Audience { get; set; } = "mercantec-apps";
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 30;
    public string KeysDirectory { get; set; } = "keys";
    public string PrivateKeyFileName { get; set; } = "private.pem";
    public string PublicKeyFileName { get; set; } = "public.pem";
}
