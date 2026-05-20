namespace Auth.API.Hosting;

/// <summary>Nøgler i <see cref="Microsoft.AspNetCore.Authentication.AuthenticationProperties"/> under OAuth-account-linking.</summary>
public static class AccountLinkAuthProperties
{
    /// <summary>Når sat (bruger-GUID), kører callback <see cref="Auth.API.Services.IExternalAccountService.LinkExternalToUserAsync"/> i stedet for find/opret.</summary>
    public const string TargetUserIdKey = "mercantec.account_link_target_user_id";
}
