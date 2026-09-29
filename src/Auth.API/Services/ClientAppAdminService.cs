using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public sealed class ClientAppAdminService(
    AuthDbContext db,
    IHostEnvironment env) : IClientAppAdminService
{
    private static readonly Regex ClientIdPattern = new(
        @"^[a-zA-Z0-9._-]{2,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<ClientAppAdminListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var clients = await db.ClientApps
            .AsNoTracking()
            .Include(c => c.RedirectUris)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return clients.Select(c => new ClientAppAdminListItem(
            c.Id,
            c.ClientId,
            c.Name,
            c.IsActive,
            c.IsPublic,
            c.RequirePkce,
            c.RedirectUris.Count,
            c.CreatedAt)).ToList();
    }

    public async Task<ClientAppAdminDetail?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var app = await FindTrackedAsync(clientId, asNoTracking: true, includeRedirects: true, cancellationToken);
        return app is null ? null : ToDetail(app);
    }

    public async Task<ClientAppAdminMutationResult> CreateAsync(
        CreateClientAppRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        var clientId = (request.ClientId ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Validation, "Udfyld et app-navn (max 120 tegn).");

        if (!IsValidClientId(clientId))
            return ClientAppAdminMutationResult.Fail(
                ClientAppAdminFailure.Validation,
                "Ugyldigt client_id (brug a-z, 0-9, -, _, . og 2–64 tegn).");

        if (await db.ClientApps.AsNoTracking().AnyAsync(c => c.ClientId == clientId, cancellationToken))
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Conflict, "client_id findes allerede.");

        string? secretHash = null;
        string? plaintext = null;
        if (!request.IsPublic)
        {
            plaintext = GenerateSecret(48);
            secretHash = BCrypt.Net.BCrypt.HashPassword(plaintext);
        }

        var entity = new ClientApp
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClientId = clientId,
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            RequirePkce = request.RequirePkce,
            ClientSecretHash = secretHash,
            AllowedScopes = string.IsNullOrWhiteSpace(request.AllowedScopes)
                ? "openid profile email offline_access"
                : request.AllowedScopes.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        db.ClientApps.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var detail = await GetByClientIdAsync(clientId, cancellationToken);
        return ClientAppAdminMutationResult.Ok(detail!, plaintext);
    }

    public async Task<ClientAppAdminMutationResult> UpdateAsync(
        string clientId,
        UpdateClientAppRequest request,
        CancellationToken cancellationToken = default)
    {
        var tracked = await FindTrackedAsync(clientId, asNoTracking: false, includeRedirects: true, cancellationToken);
        if (tracked is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Klienten findes ikke.");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
                return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Validation, "Udfyld et app-navn (max 120 tegn).");
            tracked.Name = name;
        }

        if (request.NewClientId is not null)
        {
            var newId = request.NewClientId.Trim();
            if (!IsValidClientId(newId))
                return ClientAppAdminMutationResult.Fail(
                    ClientAppAdminFailure.Validation,
                    "Ugyldigt client_id (brug a-z, 0-9, -, _, . og 2–64 tegn).");

            if (await db.ClientApps.AsNoTracking()
                    .AnyAsync(c => c.Id != tracked.Id && c.ClientId == newId, cancellationToken))
                return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Conflict, "client_id findes allerede.");

            tracked.ClientId = newId;
        }

        if (request.IsActive is { } active)
            tracked.IsActive = active;
        if (request.IsPublic is { } isPublic)
            tracked.IsPublic = isPublic;
        if (request.RequirePkce is { } pkce)
            tracked.RequirePkce = pkce;
        if (request.AllowedScopes is not null)
            tracked.AllowedScopes = string.IsNullOrWhiteSpace(request.AllowedScopes)
                ? null
                : request.AllowedScopes.Trim();

        if (request.ClearAccessTokenExpiryMinutes)
            tracked.AccessTokenExpiryMinutes = null;
        else if (request.AccessTokenExpiryMinutes is not null)
        {
            if (!TokenLifetimePolicy.IsValidAccessTokenMinutes(request.AccessTokenExpiryMinutes))
                return ClientAppAdminMutationResult.Fail(
                    ClientAppAdminFailure.Validation,
                    $"Access-token skal være mellem {TokenLifetimePolicy.MinimumAccessTokenMinutes} og {TokenLifetimePolicy.MaximumAccessTokenMinutes} minutter, eller stå tomt.");
            tracked.AccessTokenExpiryMinutes = request.AccessTokenExpiryMinutes;
        }

        if (request.ClearRefreshTokenExpiryDays)
            tracked.RefreshTokenExpiryDays = null;
        else if (request.RefreshTokenExpiryDays is not null)
        {
            if (!TokenLifetimePolicy.IsValidRefreshTokenDays(request.RefreshTokenExpiryDays))
                return ClientAppAdminMutationResult.Fail(
                    ClientAppAdminFailure.Validation,
                    $"Refresh-token skal være mellem {TokenLifetimePolicy.MinimumRefreshTokenDays} og {TokenLifetimePolicy.MaximumRefreshTokenDays} dage, eller stå tomt.");
            tracked.RefreshTokenExpiryDays = request.RefreshTokenExpiryDays;
        }

        if (request.ClearLoginThemeId)
            tracked.LoginThemeId = null;
        else if (request.LoginThemeId is not null)
            tracked.LoginThemeId = LoginThemeCatalog.NormalizeStored(
                string.IsNullOrWhiteSpace(request.LoginThemeId) ? null : request.LoginThemeId);

        if (request.UseAllLoginMethods)
            tracked.AllowedLoginMethods = null;
        else if (request.AllowedLoginMethods is not null)
        {
            var normalized = ClientLoginMethodCatalog.NormalizeStored(request.AllowedLoginMethods);
            if (normalized is null)
                return ClientAppAdminMutationResult.Fail(
                    ClientAppAdminFailure.Validation,
                    "Vælg mindst én login-metode, eller brug alle aktiverede metoder.");
            tracked.AllowedLoginMethods = normalized;
        }

        if (request.ClearRequiredLinkedProviders)
            tracked.RequiredLinkedProviders = null;
        else if (request.RequiredLinkedProviders is not null)
            tracked.RequiredLinkedProviders =
                ClientLoginMethodCatalog.NormalizeRequiredLinked(request.RequiredLinkedProviders);

        if (tracked.IsPublic)
            tracked.ClientSecretHash = null;

        await db.SaveChangesAsync(cancellationToken);
        return ClientAppAdminMutationResult.Ok(ToDetail(tracked));
    }

    public async Task<ClientAppAdminMutationResult> DeleteAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var tracked = await FindTrackedAsync(clientId, asNoTracking: false, includeRedirects: true, cancellationToken);
        if (tracked is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Klienten findes ikke.");

        db.ClientApps.Remove(tracked);
        await db.SaveChangesAsync(cancellationToken);
        return ClientAppAdminMutationResult.Deleted();
    }

    public async Task<ClientAppAdminMutationResult> AddRedirectUriAsync(
        string clientId,
        string uri,
        CancellationToken cancellationToken = default)
    {
        var tracked = await FindTrackedAsync(clientId, asNoTracking: false, includeRedirects: true, cancellationToken);
        if (tracked is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Klienten findes ikke.");

        if (!TryValidateRedirectUri(uri, out var parsed, out var error))
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Validation, error);

        var normalized = parsed.ToString();
        if (tracked.RedirectUris.Any(r => r.Uri == normalized))
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.Conflict, "URI findes allerede på klienten.");

        tracked.RedirectUris.Add(new ClientAppRedirectUri
        {
            Id = Guid.NewGuid(),
            ClientAppId = tracked.Id,
            Uri = normalized,
        });
        await db.SaveChangesAsync(cancellationToken);
        return ClientAppAdminMutationResult.Ok(ToDetail(tracked));
    }

    public async Task<ClientAppAdminMutationResult> RemoveRedirectUriAsync(
        string clientId,
        Guid redirectUriId,
        CancellationToken cancellationToken = default)
    {
        var tracked = await FindTrackedAsync(clientId, asNoTracking: false, includeRedirects: true, cancellationToken);
        if (tracked is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Klienten findes ikke.");

        var row = tracked.RedirectUris.FirstOrDefault(r => r.Id == redirectUriId);
        if (row is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Redirect URI findes ikke.");

        db.ClientAppRedirectUris.Remove(row);
        await db.SaveChangesAsync(cancellationToken);

        // Reload collection state
        tracked.RedirectUris.Remove(row);
        return ClientAppAdminMutationResult.Ok(ToDetail(tracked));
    }

    public async Task<ClientAppAdminMutationResult> RotateSecretAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var tracked = await FindTrackedAsync(clientId, asNoTracking: false, includeRedirects: true, cancellationToken);
        if (tracked is null)
            return ClientAppAdminMutationResult.Fail(ClientAppAdminFailure.NotFound, "Klienten findes ikke.");

        if (tracked.IsPublic)
            return ClientAppAdminMutationResult.Fail(
                ClientAppAdminFailure.Validation,
                "Public klient har ikke client secret.");

        var plain = GenerateSecret(48);
        tracked.ClientSecretHash = BCrypt.Net.BCrypt.HashPassword(plain);
        await db.SaveChangesAsync(cancellationToken);
        return ClientAppAdminMutationResult.Ok(ToDetail(tracked), plain);
    }

    private async Task<ClientApp?> FindTrackedAsync(
        string clientId,
        bool asNoTracking,
        bool includeRedirects,
        CancellationToken cancellationToken)
    {
        var id = (clientId ?? "").Trim();
        if (id.Length == 0)
            return null;

        IQueryable<ClientApp> q = db.ClientApps;
        if (asNoTracking)
            q = q.AsNoTracking();
        if (includeRedirects)
            q = q.Include(c => c.RedirectUris);

        return await q.FirstOrDefaultAsync(c => c.ClientId == id, cancellationToken);
    }

    private static ClientAppAdminDetail ToDetail(ClientApp c) => new(
        c.Id,
        c.ClientId,
        c.Name,
        c.IsActive,
        c.IsPublic,
        c.RequirePkce,
        c.AllowedScopes,
        c.AccessTokenExpiryMinutes,
        c.RefreshTokenExpiryDays,
        c.LoginThemeId,
        c.AllowedLoginMethods,
        c.RequiredLinkedProviders,
        !string.IsNullOrEmpty(c.ClientSecretHash),
        c.CreatedAt,
        c.RedirectUris
            .OrderBy(r => r.Uri, StringComparer.Ordinal)
            .Select(r => new ClientAppRedirectUriDto(r.Id, r.Uri))
            .ToList());

    internal static bool IsValidClientId(string value) =>
        !string.IsNullOrWhiteSpace(value) && ClientIdPattern.IsMatch(value.Trim());

    internal static string GenerateSecret(int bytes = 32)
    {
        var b = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private bool TryValidateRedirectUri(string raw, out Uri uri, out string error)
    {
        error = "";
        uri = null!;
        raw = (raw ?? "").Trim();
        if (raw.Length == 0)
        {
            error = "Udfyld en redirect URI.";
            return false;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed) || parsed is null)
        {
            error = "Ugyldig URI (skal være absolut, fx https://…).";
            return false;
        }

        uri = parsed;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            error = "Kun http/https er tilladt.";
            return false;
        }

        var isLocalHttp = uri.Scheme == Uri.UriSchemeHttp &&
                          (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                           || uri.Host == "127.0.0.1");
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing") && uri.Scheme != Uri.UriSchemeHttps)
        {
            if (!isLocalHttp)
            {
                error = "I produktion skal redirect URI bruge https.";
                return false;
            }
        }

        return true;
    }
}
