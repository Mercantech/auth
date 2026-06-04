# Klientintegration (Mercantec Auth)

## E-mail / adgangskode (valgfrit)

I `appsettings.json` eller miljø: **`Auth:EnableEmailPasswordLogin`** (default `true`). Sæt til `false` for kun OAuth (Google, Microsoft, …): så skjules formularer på `/Account/Login` og `/Account/Register`, og `POST /signin` samt `POST /signup` afvises.

Miljøvariabel: `Auth__EnableEmailPasswordLogin=false`

Når det er **slået til**, gælder samme **én bruger / ét `sub`** som for OAuth:

- **Login** (`POST /signin`): finder bruger via normaliseret e-mail i `UserEmails`, profil-e-mail eller `LocalLogins` — ikke kun eksakt match på `LocalLogin.Email`.
- **Opret konto** (`POST /signup`): hvis e-mail allerede matcher en OAuth-bruger **uden** adgangskode, tilknyttes `LocalLogin` til **den eksisterende** bruger (samme implicit logik som OAuth e-mail-match). Har brugeren allerede adgangskode, afvises med `error=email`.
- **Tilføj adgangskode (anbefalet)**: logget-ind bruger på `/Account/LinkedAccounts` → `POST /account/password/set` (e-mail skal tilhøre kontoen: `UserEmails`, profil, `ExternalLogins.ProviderEmail` eller eksisterende `LocalLogin`).

Efter password-tilknytning kører **`UserPrimaryEmailSync`** som ved OAuth, så JWT `email` og downstream sync forbliver konsistente.

## Overblik

1. Registrér en **client** i databasen (`ClientApps` + tilladte **redirect URIs** præcist som din app kalder med).
2. Send brugeren til **authorization** med **PKCE (S256)**.
3. Efter login redirectes til din `redirect_uri` med `?code=...&state=...`.
4. Din backend kalder **token-endpoint** med `code` og `code_verifier`.

Hvis brugeren allerede har **session-cookie** på auth-domænet, springes login-UI over og der udstedes straks en `code` — **kun** hvis sessionen er **fuld** (ikke `mfa_pending` efter primær login når brugeren har TOTP eller passkey).

## MFA (TOTP) og passkeys (WebAuthn)

Valgfrit pr. bruger under **`/Account/Security`** (kræver fuld session).

| Trin | Beskrivelse |
|------|-------------|
| Primær login | Adgangskode, OAuth eller passwordless passkey |
| `mfa_pending` | Kortlivet cookie (ca. 10 min) hvis brugeren har aktiv TOTP eller passkey **og** primær login **ikke** var passkey |
| `/Account/Mfa` | TOTP-kode eller passkey-bekræftelse (kun efter adgangskode/OAuth) |
| Passkey-login | Springer MFA over — WebAuthn tæller som stærk 2. faktor |
| Fuld session | OAuth `GET /oauth/authorize` og øvrige beskyttede flows |

**API (JSON, antiforgery på beskyttede kald):**

| Endpoint | Formål |
|----------|--------|
| `POST /account/mfa/totp/setup` | Start TOTP (QR/secret) |
| `POST /account/mfa/totp/confirm` | Aktivér efter første gyldige kode |
| `POST /account/mfa/verify` | Afslut MFA-trin (pending → fuld) |
| `POST /account/mfa/totp/disable` | Slå TOTP fra (kræver kode) |
| `POST /account/passkeys/register/options` + `.../complete` | Registrér passkey |
| `POST /account/passkeys/assert/options` + `.../complete` | Passkey som 2. faktor |
| `POST /account/passkeys/login/options` + `.../complete` | Passwordless login (anonym) |

**OIDC `amr` i id_token** (ved `openid` scope), når MFA er gennemført i samme session:

- `pwd` — adgangskode som primær faktor
- `otp` — TOTP eller recovery-kode
- `webauthn` — passkey

Konfiguration: `Mfa:RequireForRoles` (tom som standard), `Passkeys:RpId`, `Passkeys:Origins` i `appsettings` / miljø.

## Login-UI branding per klient

Under OAuth-flow kan login-siderne (**`/Account/Login`**, **`/Account/Register`**, **`/Account/Mfa`**) vises med et **forvalgt tema** knyttet til `client_id`.

| Admin | Felt `LoginThemeId` på `ClientApps` (dropdown i **Admin → Klienter**) |
|-------|------------------------------------------------------------------------|
| Presets | `mercantec` (standard), `mercanlink`, `gf2learn` (GF2 Learn / `gf2-learn`), … — se `login_themes` i [manifest](https://auth.mercantec.tech/.well-known/mercantec-auth.json) |

Flow: `GET /oauth/authorize?client_id=…` → redirect til login med samme `client_id` og kortlivet cookie `mercantec_login_client`, så temaet bevares gennem sign-in og MFA. Direkte besøg på `/Account/Security` uden OAuth bruger altid Mercantec-standard.

Nye temaer tilføjes i kode (`LoginThemeCatalog` + `wwwroot/themes/{id}.css`) — ikke vilkårlig CSS upload.

## Login-metoder per klient

Under OAuth-flow kan hver klient begrænse hvilke login-metoder brugeren ser på **`/Account/Login`** og **`/Account/Register`**.

| Admin | Felt `AllowedLoginMethods` på `ClientApps` (checkbokse i **Admin → Klienter**) |
|-------|--------------------------------------------------------------------------------|
| Standard | Tom / «Alle aktiverede metoder» — alle metoder serveren har slået til |
| Whitelist | Komma-separeret liste, fx `passkey,password,google,microsoft` |
| Metoder | `passkey`, `password`, `google`, `microsoft`, `microsoft_edu`, `github`, `discord` |

Effektiv liste = **klient-whitelist ∩ server-konfiguration** (OAuth-nøgler i `appsettings`, `Auth:EnableEmailPasswordLogin`). Passkey er altid tilgængelig på serveren når den er konfigureret.

Gælder kun når brugeren kommer via **`GET /oauth/authorize?client_id=…`** (eller har cookie `mercantec_login_client`). Direkte besøg på login uden OAuth-klient viser alle server-metoder.

Backend afviser også forsøg på deaktiverede metoder (`GET /signin/challenge`, `POST /signin`, `POST /signup`, passkey-login) med `error=provider`.

## Krævet tilknytning per klient

Nogle apps skal sikre at brugeren har **tilknyttet** en bestemt udbyder (fx Mercantec arbejdslogin / Microsoft 365) — uanset hvordan de logger ind.

| Admin | Felt `RequiredLinkedProviders` på `ClientApps` (checkbokse i **Admin → Klienter → Krævet tilknytning**) |
|-------|----------------------------------------------------------------------------------------------------------|
| Standard | Tom — intet krav |
| Eksempel | `microsoft` — bruger skal have tilknyttet Microsoft 365 / arbejdskonto |
| Metoder | Samme udbyder-id'er som OAuth: `google`, `microsoft`, `microsoft_edu`, `github`, `discord` |

Flow: bruger logger ind (passkey, password, GitHub, …) → ved `GET /oauth/authorize` tjekkes tilknytning → mangler den, sendes brugeren til **`/Account/LinkRequired`** med link-knapper → efter tilknytning fortsættes OAuth automatisk.

Microsoft arbejde kræver `ExternalLogin` med provider `microsoft` **og** tilknyttet arbejds-e-mail. Skolemail accepterer `microsoft-edu` eller legacy `microsoft` + skole-e-mail.

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
| `POST /account/password/set` | Tilføj eller skift adgangskode på **aktuelt loggede** bruger (session + antiforgery). Felter: `email`, `password`, `passwordConfirm`, `returnUrl` |
| `GET /api/admin/users-directory` | Oversigt over brugere (kun **Bearer JWT med rolle Admin**) |
| `POST /api/admin/users/merge` | Sammenlæg to brugerkonti — se afsnit nedenfor (**Admin-JWT**) |
| `DELETE /api/admin/users/{userId}` | Slet bruger og tilhørende login-/OAuth-data (**Admin-JWT**) — ikke dig selv, ikke sidste Admin |
| `GET /api/admin/usage/summary` | Aggregeret klient-brug og seneste hændelser (**Admin-JWT**) |
| `GET /api/admin/usage/events` | Filtrerbar hændelseslog (**Admin-JWT**) |

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

### Administrativ sammenlægning af to brugerrækker (merge)

Bruges når samme person **allerede har to `User`-rækker** (fx to forskellige `sub`/GUID og separate login-spor), og alle client-systemer kan acceptere at **kun survivor-GUID bruges** fremover. Donor-rækken **slettes** efter flyt af identiteter.

**Krav:**

- Bearer **JWT med rolle `Admin`** (`Authorization: Bearer ...`), samme udsteder/signatur som for jeres øvrige OAuth-tokens fra Mercantec Auth.

**Endpoint:**

- `POST /api/admin/users/merge`
- Body (JSON): `{ "survivorUserId": "<GUID>", "donorUserId": "<GUID>" }`
  - **Survivor** beholder JWT `sub` (= bruger-ID). **Donor** lægges ned i survivor og fjernes.

**Hvad sker:**

- Alle `ExternalLogin` på donor flyttes til survivor.
- Hvis survivor **ikke** har lokalt login: donors `LocalLogin` flyttes til survivor. Hvis **begge** har lokalt login, fjernes donors (adgangskode til donors e-mail bortfalder — advarsler i svaret beskriver).
- `UserEmails` på donor flyttes, medmindre survivor allerede har samme «Kind» (`Personal`/`Work`/`School`); overlappende `UserEmails`-slags rækker fra donor fjernes (advarsler i svar).
- Roller føres til union (kun nye Roller tilføjes til survivor).
- Primær profil-/JWT-e-mail på survivor gensynkes fra `UserEmails`.
- Alle **refresh tokens** og ikke-brugte **authorization codes** for **begge** bruger-IDs slettes (tvungen genlogin).
- Survivor-profilit suppleres dækkende hvis fx `AvatarUrl`/displaynavn mangler og donor har det; `EmailConfirmed` sættes til union.

**Ikke automatisk:** Downstream databaser der gemmer `sub`/bruger-ID skal migrate eller opdatere henvisning fra donors gamle GUID til survivor — JWT udsteder kun den canonicale bruger derefter.

`GET /api/admin/users-directory` er på samme **Admin-JWT-beskyttelse** og kan bruges til at finde GUIDs før merge eller sletning.

**Slet bruger:** `DELETE /api/admin/users/{userId}` → `204` ved success. Afviser sletning af egen konto (403) og af **sidste** bruger med rolle Admin (409). Fjerner også refresh tokens, ikke-brugte auth codes samt lokale/OAuth-login for den pågældende bruger.

### Sporing af brug (admin)

Mercantec Auth logger hændelser i **`AuthUsageEvents`** og aggregerer OAuth-klient-brug i **`UserClientUsages`** (pr. bruger + `client_id`: authorize, token, refresh). Ved provider-login opdateres **`ExternalLogins.LastUsedAtUtc`**. Refresh tokens gemmer **`ClientId`** når de udstedes.

- Admin UI: `/Admin/Usage` (oversigt) og udvidede kolonner på `/Admin/Users`
- API (Admin-JWT): `GET /api/admin/usage/summary`, `GET /api/admin/usage/events?userId=&clientId=&eventType=&limit=`

Hændelsestyper: `provider_login`, `password_login`, `password_signup`, `password_link`, `oauth_authorize`, `oauth_token_exchange`, `oauth_refresh`, `account_link`.

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
- Claims: `sub` (bruger-id), `name`, `email` (hvis findes), `role` (flere), `amr` (ved openid + MFA), `iss`, `aud`, `exp`

## Demo-klient (kun Development)

Ved opstart oprettes `client_id=demo` med callback `http://localhost:5155/oauth/callback` (og `127.0.0.1`). Brug et lille script eller Postman til at generere PKCE og kalde token.

## Miljø / Docker

`docker compose -f docker/docker-compose.yml up --build` starter Postgres + API på port **8080**. Tilpas OAuth-secrets i miljøvariabler eller mountet `appsettings`.
