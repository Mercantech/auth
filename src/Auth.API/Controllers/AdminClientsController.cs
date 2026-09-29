using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

/// <summary>Admin-API til OAuth-klienter (Admin-JWT eller MCP <c>x-api-key</c>).</summary>
[ApiController]
[Route("api/admin/clients")]
[EnableCors("MercantecSpa")]
[Authorize(Policy = AdminApiPolicies.Name)]
public class AdminClientsController(IClientAppAdminService clients) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientAppAdminListItem>>> List(CancellationToken cancellationToken) =>
        Ok(await clients.ListAsync(cancellationToken));

    [HttpGet("{clientId}")]
    public async Task<ActionResult<ClientAppAdminDetail>> Get(string clientId, CancellationToken cancellationToken)
    {
        var detail = await clients.GetByClientIdAsync(clientId, cancellationToken);
        return detail is null ? NotFound(new { error = "Klienten findes ikke." }) : Ok(detail);
    }

    [HttpPost]
    public async Task<ActionResult<ClientAppCreateResponse>> Create(
        [FromBody] CreateClientAppApiRequest body,
        CancellationToken cancellationToken)
    {
        var result = await clients.CreateAsync(
            new CreateClientAppRequest(
                body.Name,
                body.ClientId,
                body.IsActive ?? true,
                body.IsPublic ?? true,
                body.RequirePkce ?? true,
                body.AllowedScopes),
            cancellationToken);

        return ToActionResult(result, created: true);
    }

    [HttpPatch("{clientId}")]
    public async Task<ActionResult<ClientAppAdminDetail>> Update(
        string clientId,
        [FromBody] UpdateClientAppApiRequest body,
        CancellationToken cancellationToken)
    {
        var result = await clients.UpdateAsync(
            clientId,
            new UpdateClientAppRequest(
                Name: body.Name,
                NewClientId: body.NewClientId,
                IsActive: body.IsActive,
                IsPublic: body.IsPublic,
                RequirePkce: body.RequirePkce,
                AllowedScopes: body.AllowedScopes,
                AccessTokenExpiryMinutes: body.AccessTokenExpiryMinutes,
                ClearAccessTokenExpiryMinutes: body.ClearAccessTokenExpiryMinutes ?? false,
                RefreshTokenExpiryDays: body.RefreshTokenExpiryDays,
                ClearRefreshTokenExpiryDays: body.ClearRefreshTokenExpiryDays ?? false,
                LoginThemeId: body.LoginThemeId,
                ClearLoginThemeId: body.ClearLoginThemeId ?? false,
                AllowedLoginMethods: body.AllowedLoginMethods,
                UseAllLoginMethods: body.UseAllLoginMethods ?? false,
                RequiredLinkedProviders: body.RequiredLinkedProviders,
                ClearRequiredLinkedProviders: body.ClearRequiredLinkedProviders ?? false),
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{clientId}")]
    public async Task<IActionResult> Delete(string clientId, CancellationToken cancellationToken)
    {
        var result = await clients.DeleteAsync(clientId, cancellationToken);
        if (result.Success)
            return NoContent();
        return ToActionResult(result);
    }

    [HttpPost("{clientId}/redirect-uris")]
    public async Task<ActionResult<ClientAppAdminDetail>> AddRedirectUri(
        string clientId,
        [FromBody] AddRedirectUriRequest body,
        CancellationToken cancellationToken)
    {
        var result = await clients.AddRedirectUriAsync(clientId, body.Uri, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{clientId}/redirect-uris/{redirectUriId:guid}")]
    public async Task<ActionResult<ClientAppAdminDetail>> RemoveRedirectUri(
        string clientId,
        Guid redirectUriId,
        CancellationToken cancellationToken)
    {
        var result = await clients.RemoveRedirectUriAsync(clientId, redirectUriId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{clientId}/rotate-secret")]
    public async Task<ActionResult<ClientAppCreateResponse>> RotateSecret(
        string clientId,
        CancellationToken cancellationToken)
    {
        var result = await clients.RotateSecretAsync(clientId, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult ToActionResult(ClientAppAdminMutationResult result, bool created = false)
    {
        if (result.Success)
        {
            if (result.Client is null)
                return NoContent();

            if (result.PlaintextSecret is not null)
            {
                var payload = new ClientAppCreateResponse(result.Client, result.PlaintextSecret);
                return created
                    ? CreatedAtAction(nameof(Get), new { clientId = result.Client.ClientId }, payload)
                    : Ok(payload);
            }

            return created
                ? CreatedAtAction(nameof(Get), new { clientId = result.Client.ClientId }, result.Client)
                : Ok(result.Client);
        }

        return result.Failure switch
        {
            ClientAppAdminFailure.NotFound => NotFound(new { error = result.Error }),
            ClientAppAdminFailure.Conflict => Conflict(new { error = result.Error }),
            ClientAppAdminFailure.Validation => BadRequest(new { error = result.Error }),
            _ => Problem(detail: result.Error),
        };
    }
}

public sealed record CreateClientAppApiRequest(
    string Name,
    string ClientId,
    bool? IsActive = null,
    bool? IsPublic = null,
    bool? RequirePkce = null,
    string? AllowedScopes = null);

public sealed record UpdateClientAppApiRequest(
    string? Name = null,
    string? NewClientId = null,
    bool? IsActive = null,
    bool? IsPublic = null,
    bool? RequirePkce = null,
    string? AllowedScopes = null,
    int? AccessTokenExpiryMinutes = null,
    bool? ClearAccessTokenExpiryMinutes = null,
    int? RefreshTokenExpiryDays = null,
    bool? ClearRefreshTokenExpiryDays = null,
    string? LoginThemeId = null,
    bool? ClearLoginThemeId = null,
    IReadOnlyList<string>? AllowedLoginMethods = null,
    bool? UseAllLoginMethods = null,
    IReadOnlyList<string>? RequiredLinkedProviders = null,
    bool? ClearRequiredLinkedProviders = null);

public sealed record AddRedirectUriRequest(string Uri);

/// <summary>
/// Svar ved create/rotate når et plaintext secret udstedes (kun én gang).
/// </summary>
public sealed record ClientAppCreateResponse(ClientAppAdminDetail Client, string? ClientSecretPlaintext);
