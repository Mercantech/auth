# Klientintegration (Mercantec Auth)

## E-mail / adgangskode (valgfrit)

I `appsettings.json` eller miljø: **`Auth:EnableEmailPasswordLogin`** (default `true`). Sæt til `false` for kun OAuth (Google, Microsoft, …): så skjules formularer på `/Account/Login` og `/Account/Register`, og `POST /signin` samt `POST /signup` afvises.

Miljøvariabel: `Auth__EnableEmailPasswordLogin=false`

## Overblik

1. Registrér en **client** i databasen (`ClientApps` + tilladte **redirect URIs** præcist som din app kalder med).
2. Send brugeren til **authorization** med **PKCE (S256)**.
3. Efter login redirectes til din `redirect_uri` med `?code=...&state=...`.
4. Din backend kalder **token-endpoint** med `code` og `code_verifier`.

Hvis brugeren allerede har **session-cookie** på auth-domænet, springes login-UI over og der udstedes straks en `code`.

## Endpoints (relativt til auth-base-URL)

| Endpoint | Formål |
|----------|--------|
| `GET /oauth/authorize` | Start flow |
| `POST /oauth/token` | Byt code / refresh |
| `GET /.well-known/jwks.json` | Verificér JWT-signatur |
| `GET /.well-known/mercantec-auth.json` | **Integrations-manifest** (JSON): maskinlæsbar guide til nye platforme og AI — endpoints, PKCE, JWT-claims, checklist, kort dansk briefing |
| `GET /health` | Liveness |
| `GET /account/link/start` | Eksplicit OAuth-tilknytning til **aktuelt loggede** bruger (kræver session). Query: `provider`, `returnUrl` |
| `POST /account/link/remove` | Fjern en `ExternalLogin` (mindst én login-metode skal blive tilbage) |

## Brugerkonto: flere login-udbydere (account linking)

Mercantec Auth udsteder **ét stabilt `sub`** (bruger-GUID) til jeres OAuth-klienter, uanset hvilken ekstern udbyder brugeren valgte sidst.

### Implicit sammenlægning (via e-mail)

Når en bruger logger ind med en **ny** udbyder (fx GitHub), forsøger vi at finde en eksisterende bruger med **samme normaliserede e-mail** (fra `UserEmails`, primær profil-e-mail eller lokalt login). Matcher det, **tilføjes** den nye udbyder til **samme** bruger uden at spørge først. Det gør onboarding let, men kræver at OAuth-udbyderen returnerer pålidelig e-mail.

### Eksplicit tilknytning (anbefalet ved forskellige e-mails)

Når brugeren **allerede har session** på auth-domænet (`mercantec_auth` cookie), kan de under **Tilknyttede login** (`/Account/LinkedAccounts`) vælge en udbyder eller bruge:

- `GET /account/link/start?provider=google|microsoft|microsoft-edu|github|discord&returnUrl=/Account/LinkedAccounts`

Svaret er en **OAuth challenge** med `mercantec.account_link_target_user_id` i state, så callback **altid** binder den eksterne identitet til den **nuværende** session-bruger. Hvis udbyder-identiteten **allerede er knyttet til en anden Mercantec-bruger**, vises fejl (link-konflikt) — ingen stille sammenfletning på tværs af konti.

Fjern tilknytning: `POST /account/link/remove` (formular med antiforgery-token, felter `id` = `ExternalLogin`-rækkens id, `returnUrl`).

Azure / IdP: ingen **ekstra** redirect-URI i forhold til almindeligt login — linking bruger samme OAuth-callback-stier som ved login (fx `/signin-google`, `/signin-microsoft`, `/signin-microsoft-edu`, `/signin-github`, `/signin-discord`).

### OAuth-klienter (SPAs / API’er)

Jeres app skal **ingen** særlig kode for linking: `sub` i JWT forbliver det samme på tværs af tilknyttede udbydere. Alle accounts-håndtering sker på Mercantec Auth via browser og session.

## `GET /oauth/authorize`

Query-parametre:

- `response_type=code` (påkrævet)
- `client_id` — dit registrerede id (fx `demo` i dev)
- `redirect_uri` — skal matche whitelist **eksakt**
- `state` — anbefalet (CSRF)
- `code_challenge` — BASE64URL(SHA256(code_verifier)) uden padding
- `code_challenge_method=S256`

Eksempel (én linje, tilpas host og PKCE):

```
GET /oauth/authorize?response_type=code&client_id=demo&redirect_uri=http%3A%2F%2Flocalhost%3A5155%2Foauth%2Fcallback&state=random&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256
```

## `POST /oauth/token`

`Content-Type: application/x-www-form-urlencoded`

### Authorization code

- `grant_type=authorization_code`
- `code` — fra redirect
- `redirect_uri` — samme som i authorize
- `client_id`
- `code_verifier` — original PKCE-verifier (offentlig klient)
- `client_secret` — kun for **confidential** klienter

Svar (JSON): `access_token`, `refresh_token`, `token_type`, `expires_in`.

### Refresh

- `grant_type=refresh_token`
- `refresh_token`
- `client_id`
- `client_secret` hvis confidential

## JWT

- Algoritme: **RS256**
- Valider med nøgler fra **JWKS**
- Claims: `sub` (bruger-id), `name`, `email` (hvis findes), `role` (flere), `iss`, `aud`, `exp`

## Demo-klient (kun Development)

Ved opstart oprettes `client_id=demo` med callback `http://localhost:5155/oauth/callback` (og `127.0.0.1`). Brug et lille script eller Postman til at generere PKCE og kalde token.

## Miljø / Docker

`docker compose -f docker/docker-compose.yml up --build` starter Postgres + API på port **8080**. Tilpas OAuth-secrets i miljøvariabler eller mountet `appsettings`.
