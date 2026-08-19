namespace Auth.API.Options;

public static class TokenLifetimePolicy
{
    public const int MinimumAccessTokenMinutes = 5;
    public const int MaximumAccessTokenMinutes = 24 * 60;
    public const int MinimumRefreshTokenDays = 1;
    public const int MaximumRefreshTokenDays = 365;

    public static int ResolveAccessTokenMinutes(int? clientValue, JwtOptions defaults) =>
        Math.Clamp(
            clientValue ?? defaults.AccessTokenExpiryMinutes,
            MinimumAccessTokenMinutes,
            MaximumAccessTokenMinutes);

    public static int ResolveRefreshTokenDays(int? clientValue, JwtOptions defaults) =>
        Math.Clamp(
            clientValue ?? defaults.RefreshTokenExpiryDays,
            MinimumRefreshTokenDays,
            MaximumRefreshTokenDays);

    public static bool IsValidAccessTokenMinutes(int? value) =>
        value is null or >= MinimumAccessTokenMinutes and <= MaximumAccessTokenMinutes;

    public static bool IsValidRefreshTokenDays(int? value) =>
        value is null or >= MinimumRefreshTokenDays and <= MaximumRefreshTokenDays;
}
