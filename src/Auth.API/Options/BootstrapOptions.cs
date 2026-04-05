namespace Auth.API.Options;

public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>E-mail for første bruger der får Admin ved opstart (hvis brugeren findes).</summary>
    public string? AdminEmail { get; set; }
}
