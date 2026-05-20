namespace Auth.API.Security;

public static class MfaPolicies
{
    /// <summary>Fuld session — MFA gennemført eller ikke påkrævet.</summary>
    public const string FullSession = "FullSession";

    /// <summary>Pending eller fuld — til MFA-verify og setup under pending.</summary>
    public const string MfaStep = "MfaStep";
}
