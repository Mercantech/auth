namespace Auth.API.Options;

public class PasskeyOptions
{
    public const string SectionName = "Passkeys";

    public string RpId { get; set; } = "localhost";
    public string RpName { get; set; } = "Mercantec Auth";
    public string[] Origins { get; set; } = ["https://localhost:5001"];
}
