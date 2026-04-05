namespace Auth.API.Services;

public static class EmailNormalizer
{
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return email.Trim().ToLowerInvariant();
    }
}
