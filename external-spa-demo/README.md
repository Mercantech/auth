# Ekstern SPA-demo (HTML + JS)

Simpel statisk side på **egen port** der logger ind via Mercantec Auth med **PKCE** og viser **access_token** / **refresh_token** fra `POST /oauth/token`.

## Forudsætninger

1. **Auth API kører** (fx Docker på `http://localhost:8080`) med `ASPNETCORE_ENVIRONMENT=Development`, så `demo`-klienten og redirect til `callback.html` findes.
2. **CORS**: I Development tillader API `http://localhost:5173` og `127.0.0.1:5173` (se `appsettings.Development.json` → `Cors:SpaOrigins`).
3. **Microsoft 365 / Azure AD** (valgfrit til “Log ind med Microsoft” på auth-siden):
   - I **Azure Portal** → **App registrations** → jeres app → **Authentication** → **Platform: Web**  
     Tilføj **Redirect URI**:  
     `http://localhost:8080/signin-microsoft`  
     (tilpas host/port hvis auth kører andetsteds).
   - **Certificates & secrets** → client secret.
   - I auth-projektet: sæt `OAuth:Microsoft:ClientId`, `OAuth:Microsoft:ClientSecret`, og `OAuth:Microsoft:TenantId` (jeres **Directory (tenant) ID** for Mercantec — **ikke** `common` hvis I bruger single-tenant endpoints i koden).

## Kør test-SPA’en

### Med Docker Compose (anbefalet)

Fra repo-rod:

```powershell
docker compose -f docker/docker-compose.yml up --build -d
```

Åbn **http://localhost:5173** (service `spa`) — auth API på **http://localhost:8080**.

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
| `callback.html` | Modtager `code`, kalder `/oauth/token`, gemmer tokens i `sessionStorage` |
| `jwt.html` | Viser JWT header/payload + **RS256-verifikation** mod `/.well-known/jwks.json` (via [jose](https://github.com/panva/jose) fra CDN) |
| `users.html` | Kalder `GET /api/admin/users-directory` med Bearer — kræver **Admin**-rolle på Mercantec-brugeren |
| `logout.js` | `mercantecLogout()` — rydder `sessionStorage` og sender browser til `GET /signout` på auth-host med sikker `returnUrl` tilbage til SPA’en |
| `session-jwt.js` | Hjælpere til at læse JWT-payload i browseren (kun visning): navn, e-mail, `login_method`, roller, `sub`, udløb |

### Log ud (SPA ↔ cookie på auth-host)

OAuth-flowet sætter en **session-cookie** på auth-serveren (`localhost:8080`), så næste `/oauth/authorize` kan logge dig ind uden at spørge igen. For at vælge en anden konto skal cookien slettes via **`GET /signout`**.

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
- Azure: tilføj produktions-redirect `https://auth.ditdomæne.dk/signin-microsoft`.
