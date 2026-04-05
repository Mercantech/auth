using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Auth.API.Controllers;

/// <summary>
/// Maskinlæsbart integrations-manifest til nye platforme, AI-agenter og udviklere.
/// </summary>
[AllowAnonymous]
[EnableCors("MercantecSpa")]
public class IntegrationManifestController(
    IOptions<JwtOptions> jwtOptions,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [HttpGet("/.well-known/mercantec-auth.json")]
    [Produces("application/json")]
    public IActionResult Get()
    {
        var jwt = jwtOptions.Value;
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
        var manifest = BuildManifest(baseUrl, jwt, authOptions.Value.EnableEmailPasswordLogin);
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        return Content(json, "application/json");
    }

    private static object BuildManifest(string baseUrl, JwtOptions jwt, bool enableEmailPasswordLogin) => new
    {
        schema_version = "1.0",
        name = "Mercantec Auth",
        purpose = "OAuth 2.0 authorization server med PKCE, JWT (RS256) og refresh tokens til Mercantec-platforme.",
        platform_attribution = new
        {
            maintainer = "MAGS",
            role = "underviser",
            contact_email = "mags@mercantec.dk",
            notice_da = "Denne installation er et undervisningsprojekt ved MAGS og er ikke Mercantecs officielle produktions-login. Kontakt mags@mercantec.dk ved spørgsmål.",
        },
        email_password_login_enabled = enableEmailPasswordLogin,
        auth_configuration_note = "Auth:EnableEmailPasswordLogin (eller miljøvariabel Auth__EnableEmailPasswordLogin) styrer e-mail/adgangskode på /Account/Login, /Account/Register og POST /signin, /signup.",
        audience_for_this_document = new[]
        {
            "Udviklere der tilslutter en ny webapp, mobilapp eller backend.",
            "AI-agenter der skal generere korrekt login- og API-kode mod Mercantec Auth.",
        },
        issuer = jwt.Issuer,
        jwt = new
        {
            algorithm = "RS256",
            issuer_must_equal = jwt.Issuer,
            audience_must_equal = jwt.Audience,
            access_token_lifetime_minutes = jwt.AccessTokenExpiryMinutes,
            refresh_token_lifetime_days = jwt.RefreshTokenExpiryDays,
            jwks_uri = $"{baseUrl}/.well-known/jwks.json",
            validate_signature_against_jwks = true,
            standard_claims = new object[]
            {
                new { claim = "sub", meaning = "Brugerens stabile GUID som streng." },
                new { claim = "name", meaning = "Vist navn." },
                new { claim = "email", meaning = "E-mail når den findes på brugeren." },
                new { claim = "login_method", meaning = "Sidste login: password, google, github, discord, microsoft-work, …" },
                new { claim = "role", meaning = "Rolle (gentaget claim pr. rolle). Bruges til autorisation i jeres API." },
                new { claim = "iss", meaning = "JWT-udsteder — skal matche issuer_must_equal." },
                new { claim = "aud", meaning = "Audience — skal matche audience_must_equal." },
                new { claim = "exp", meaning = "Udløb (Unix sekunder)." },
                new { claim = "iat", meaning = "Udstedt (Unix sekunder)." },
            },
            dotnet_role_claim_type = ClaimTypes.Role,
        },
        oauth2 = new
        {
            flows_supported = new[] { "authorization_code" },
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            pkce = new
            {
                required = "Ja for klienter hvor RequirePkce er sand i databasen (anbefalet for alle offentlige SPA'er).",
                code_challenge_method = "S256",
                code_challenge = "BASE64URL(SHA256(code_verifier)) uden padding",
                code_verifier = "Kryptografisk tilfældig streng (RFC 7636)",
            },
            endpoints = new
            {
                authorization = $"{baseUrl}/oauth/authorize",
                token = $"{baseUrl}/oauth/token",
            },
            authorize_query_parameters = new object[]
            {
                new { name = "response_type", value = "code", required = true },
                new { name = "client_id", required = true },
                new { name = "redirect_uri", required = true, note = "Skal matche en registreret URI præcist (whitelist)." },
                new { name = "state", required = false, note = "Stærkt anbefalet mod CSRF." },
                new { name = "code_challenge", required = true, note = "Når PKCE er påkrævet for klienten." },
                new { name = "code_challenge_method", value = "S256", required = true, note = "Når PKCE er påkrævet." },
            },
            token_endpoint = new
            {
                method = "POST",
                content_type = "application/x-www-form-urlencoded",
                authorization_code_body = new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = "Autorizationskode fra redirect til redirect_uri",
                    ["redirect_uri"] = "Samme værdi som ved /oauth/authorize",
                    ["client_id"] = "Registreret client id",
                    ["code_verifier"] = "PKCE-verifier (offentlig klient)",
                    ["client_secret"] = "Kun for fortrolige (confidential) klienter — sendes i form body",
                },
                refresh_token_body = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = "Seneste refresh_token fra forrige token-svar",
                    ["client_id"] = "Registreret client id",
                    ["client_secret"] = "Kun for fortrolige klienter",
                },
                token_response_json_fields = new[]
                {
                    "access_token (Mercantec JWT)",
                    "refresh_token (roterende opaque token)",
                    "token_type (Bearer)",
                    "expires_in (sekunder)",
                    "microsoft_access_token (valgfri — Azure AD til Graph når bruger loggede ind med Microsoft)",
                    "microsoft_expires_in (valgfri)",
                },
            },
        },
        session_and_logout = new
        {
            note = "Brugeren får session-cookie på auth-hosten under login. Næste /oauth/authorize kan derfor springe login-UI over.",
            signout_get = $"{baseUrl}/signout",
            signout_query_returnUrl = new
            {
                name = "returnUrl",
                rules = new[]
                {
                    "Relativ sti (/...) redirecter på auth-hosten.",
                    "Absolut http(s)-URL kun hvis origin er whitelisted i Cors:SpaOrigins (samme liste som CORS for jeres SPA).",
                },
                example_spa = $"{baseUrl}/signout?returnUrl=" + "{encodeURIComponent('https://din-app.example/')}",
            },
            recommendation_for_spas = "Ryd lokale tokens (fx sessionStorage), derefter fuld browser-navigation til /signout?returnUrl=... tilbage til jeres app.",
        },
        browser_login_ui = $"{baseUrl}/Account/Login",
        health = $"{baseUrl}/health",
        this_manifest = $"{baseUrl}/.well-known/mercantec-auth.json",
        client_registration = new
        {
            summary = "Hver platform skal have rækker i ClientApps og ClientAppRedirectUri med præcis redirect_uri.",
            public_client = "IsPublic=true, ingen client_secret, PKCE påkrævet hvis RequirePkce er sat.",
            confidential_client = "client_secret hashes med BCrypt i databasen; send secret i token-kald.",
            development = "I Development miljø oprettes ofte en demo-klient automatisk — se server-dokumentation.",
        },
        cors = new
        {
            setting = "Cors:SpaOrigins",
            purpose = "Browser-fetch til /oauth/token og beslægtede API'er fra jeres SPA-origin.",
        },
        checklist_new_platform = new[]
        {
            "Registrér client_id og alle produktions- og udviklings-redirect_uri'er i databasen (eksakt match, inkl. trailing slash hvis relevant).",
            "Implementér authorization code flow med PKCE S256 i frontend eller native app.",
            "Gem access_token og refresh_token sikkert (browser: overvej sessionStorage vs memory; undgå localStorage til refresh hvis muligt).",
            "Valider Mercantec JWT på backend med JWKS (issuer, audience, signatur, exp).",
            "Brug Authorization: Bearer <access_token> mod jeres egne API'er; tjek rolle-claims for admin m.m.",
            "Implementér refresh før exp eller ved 401; brug nyt refresh_token fra hvert refresh-svar.",
            "Tilføj jeres SPA-origin til Cors:SpaOrigins og brug /signout?returnUrl= for kontoskift.",
            "Hvis I skal kalde Microsoft Graph: læs microsoft_access_token fra token-svar når det findes (kun ved Microsoft-login).",
        },
        ai_agent_briefing_da = "Mercantec Auth er en OAuth 2.0-server med authorization code + PKCE (S256). " +
            "Start med GET /oauth/authorize med client_id, redirect_uri, state, code_challenge og code_challenge_method=S256. " +
            "Efter redirect med ?code= udveksles koden på POST /oauth/token med grant_type=authorization_code, code_verifier, redirect_uri og client_id. " +
            "access_token er et RS256-JWT med iss=" + jwt.Issuer + " og aud=" + jwt.Audience + "; valider med " + baseUrl + "/.well-known/jwks.json. " +
            "refresh_token bruges med grant_type=refresh_token. Session-cookie på auth-domænet gør at brugeren kan være 'automatisk logget ind' ved næste authorize — brug GET /signout?returnUrl= for at logge ud af session.",
        references = new
        {
            human_documentation_path_in_repo = "docs/CLIENT-INTEGRATION.md",
            rfc_pkce = "https://datatracker.ietf.org/doc/html/rfc7636",
        },
    };
}
