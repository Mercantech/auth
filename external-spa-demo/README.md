# Ekstern SPA-demo (HTML + JS)

Simpel statisk side på **egen port** der logger ind via Mercantec Auth med **PKCE** og viser **access_token** / **refresh_token** fra `POST /oauth/token`.

## Forudsætninger

1. **Auth API kører** (fx `https://auth.mercantec.tech` i produktion eller Docker på `http://localhost:8080` i dev).
2. **CORS**: Tilføj SPA-origin under `Cors:SpaOrigins` (produktion: `https://auth-spa.mercantec.tech`, dev: `http://localhost:5173`).
3. **Microsoft 365 / Azure AD** (valgfrit til “Log ind med Microsoft” på auth-siden):
   - **Arbejde / mercantec.dk** — App registration i Mercantec-tenant:
     - Redirect URI (Web): `http://localhost:8080/signin-microsoft` (+ produktion `https://<auth-host>/signin-microsoft`)
     - Config: `OAuth__Microsoft__ClientId`, `OAuth__Microsoft__ClientSecret`, `OAuth__Microsoft__TenantId`
   - **Skole / edu.mercantec.dk** — separat App registration i edu-tenant:
     - Redirect URI (Web): `http://localhost:8080/signin-microsoft-edu` (+ produktion `https://<auth-host>/signin-microsoft-edu`)
     - Config: `OAuth__MicrosoftEDU__ClientId`, `OAuth__MicrosoftEDU__ClientSecret`, `OAuth__MicrosoftEDU__TenantId` (`Scope` valgfri)

## Kør test-SPA’en

### Med Docker Compose (anbefalet)

Fra repo-rod:

```powershell
docker compose -f docker/docker-compose.yml up --build -d
```

Åbn **http://localhost:5173** (service `spa`) i dev — eller brug jeres hostede SPA. Auth API er i produktion på **https://auth.mercantec.tech**.

**Microsoft i Docker:** secrets i `appsettings.json` på din maskine kommer **ikke** med i image. Sæt dem i `docker/.env` (kopier fra `docker/.env.example`) og fjern kommentaren på `env_file: .env` under `api` i compose, **eller** sæt `OAuth__Microsoft__*` som miljøvariabler i compose.

### Lokalt uden Docker SPA

Fra denne mappe:

```powershell
npx --yes serve . -l 5173
```

## Sider

| Fil | Formål |
|-----|--------|
| `index.html` | Start PKCE-login |
| `docs.html` | **Visuel undervisningsside**: hvorfor central auth, OAuth2 + PKCE (sekvensdiagram), OIDC, scopes/klienttyper, token-livscyklus, session vs. tokens, MFA/passkeys, angreb & forsvar, ordliste og quiz — med Mermaid-diagrammer og **interaktive demoer** (PKCE-legeplads, authorize-URL-bygger, live token-nedtælling + ægte refresh, Base64URL-koder, JWT-tamper-værksted, CSRF-simulator, redirect_uri-tester) |
| `docs-demos.js` | Logikken bag de interaktive demoer på docs-siden (genbruger `pkce.js` og `session-jwt.js`; JWT-verifikation via jose fra CDN) |
| `ai-prompt.html` | **Kopierbar AI-prompt**: vælg scenarie (SPA / backend / fuld stack) og kopiér en færdig prompt der lærer en AI at integrere korrekt mod platformen; kan også hente integrations-manifestet live |
| `ai-prompt.js` | Genererer prompten dynamisk ud fra `shared-config.js` (authBaseUrl, clientId, issuer, audience) |
| `callback.html` | Modtager `code`, kalder `/oauth/token`, gemmer tokens i `sessionStorage` |
| `jwt.html` | Viser JWT header/payload + **RS256-verifikation** mod `/.well-known/jwks.json` (via [jose](https://github.com/panva/jose) fra CDN) |
| `users.html` | Admin-brugere: liste, **sammenlæg** og **slet** via `GET /api/admin/users-directory`, `POST …/merge`, `DELETE …/{id}` med Bearer og **Admin**-rolle |
| `appsettings-to-env.html` | **appsettings.json → .env**: fladgør nested JSON til `Section__Key` som ASP.NET Core / Docker forventer; kopiér eller download |
| `appsettings-env.js` | Ren logik til flatten + escaping (bruges af siden ovenfor) |
| `demo-theme.css` | Fælles visuelt tema for demo-SPA’en (varm “protokol”-stil, Epilogue + Newsreader) |
| `logout.js` | `mercantecLogout()` — rydder `sessionStorage` og sender browser til `GET /signout` på auth-host med sikker `returnUrl` tilbage til SPA’en |
| `session-jwt.js` | Hjælpere til at læse JWT-payload i browseren (kun visning): navn, e-mail (maskeret som `**@domæne`), `login_method`, roller, `sub`, udløb |

### Log ud (SPA ↔ cookie på auth-host)

OAuth-flowet sætter en **session-cookie** på auth-serveren, så næste `/oauth/authorize` kan logge dig ind uden at spørge igen. For at vælge en anden konto skal cookien slettes via **`GET /signout`**.

- **`logout.js`** kalder først `sessionStorage` clear (Mercantec JWT i browseren), derefter  
  `{authBaseUrl}/signout?returnUrl={encodeURIComponent(nuværende side)}`.
- **`returnUrl`** må kun være en **http(s)-URL hvis origin** findes i **`Cors:SpaOrigins`** (samme liste som CORS), ellers falder API’et tilbage til redirect til `/` på auth-hosten. Tilføj fx `http://localhost:5173` og evt. `http://127.0.0.1:5173` (allerede i `appsettings.Development.json`).

I jeres egen Vite/React/Vue-app: gentag samme mønster (clear lokale tokens → fuld navigation til `/signout?returnUrl=...`).

Konfiguration samles i **`shared-config.js`** (`authBaseUrl`, `clientId`, `expectedIssuer`, `expectedAudience` — sidstnævnte skal matche `Jwt` i API `appsettings`).

`redirect_uri` beregnes som `window.location.origin + "/callback.html"` og skal være whitelistet på `ClientAppRedirectUris`.

## Produktion

- Brug **HTTPS** overalt.
- Tilføj jeres SPA-origin under `Cors:SpaOrigins` (eller miljøvariabler `Cors__SpaOrigins__0=...`).
- Opret en **rigtig** `client_id` i DB med præcis callback-URL.
- Azure: tilføj produktions-redirect `https://auth.ditdomæne.dk/signin-microsoft` og evt. `…/signin-microsoft-edu` for edu.
