# auth.mercantec.tech — Project Specification & AI Prompt

## 🎯 Project Context (til dig, AI)

Du hjælper med at bygge en **centraliseret authentication service** til en underviser (MAGS) på en erhvervsskole (Mercantec). Servicen skal fungere som en **Identity Provider / Auth Broker** — alle MAGS's webplatforme peger på denne ene service for login, og den håndterer al OAuth, email/password og token-udstedelse.

### Hvorfor dette projekt eksisterer

MAGS bygger flere platforme til sine elever:
- `videnstjek.mags.dk` — en quiz-app (Blazor WASM, allerede live)
- En fremtidig elevportal — beskeder, ugeplaner, hold, gamification
- Flere services over tid

I stedet for at implementere auth i hver app, bygger vi **én central auth-service** som alle apps bruger. Elever logger ind én gang, med den metode de foretrækker (skolekonto, Discord, Google, GitHub, eller email/password).

### Vigtige designbeslutninger

- **ASP.NET Core 9** — bevidst valg fordi eleverne lærer C#/.NET. Dogfooding.
- **PostgreSQL** i produktion, **SQLite** til lokal dev (EF Core gør det nemt at skifte)
- **Docker-first** — alt deployes via Docker Compose. MAGS kører allerede Docker på sine services.
- **Ingen third-party auth platforms** (Auth0, Firebase Auth, etc.) — dette ER auth-platformen. Vi bygger den selv.
- **Multi-provider per bruger** — én elev kan linke Discord + skolekonto + email til samme bruger
- **JWT-baseret** — client apps validerer tokens med public key, ingen roundtrip til auth

---

## 🏗️ Arkitektur

### System overview

```
videnstjek.mags.dk ──┐
elevportalen.dk ─────┤──→ redirect til auth.mercantec.tech/login
fremtidig-app.dk ────┘                    │
                                          ▼
                               auth.mercantec.tech
                               (denne service)
                                          │
                     ┌──────────┬─────────┼──────────┬────────────┐
                     ▼          ▼         ▼          ▼            ▼
                  Discord    Google    GitHub    Microsoft     Email/
                   OAuth      OAuth     OAuth   Entra ID     Password
```

### Auth flow (OAuth)

1. Bruger besøger `videnstjek.mags.dk` → klikker "Log ind"
2. Redirect til `auth.mercantec.tech/login?redirect_uri=https://videnstjek.mags.dk/callback&client_id=videnstjek`
3. Bruger vælger provider (Discord, Google, etc.)
4. OAuth flow med valgt provider
5. Auth-service opretter/finder bruger, genererer JWT + refresh token
6. Redirect tilbage til `videnstjek.mags.dk/callback?token=xxx`
7. Client app gemmer tokens, bruger er logget ind

### Auth flow (Email/Password)

1. Samme redirect til auth.mercantec.tech
2. Bruger vælger email/password, udfylder form
3. Auth-service validerer credentials
4. Samme token-udstedelse og redirect

---

## 📊 Datamodel

### Users
```csharp
public class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public string? Email { get; set; }        // nullable — Discord-login har ikke altid email
    public bool EmailConfirmed { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    
    // Navigation
    public List<ExternalLogin> ExternalLogins { get; set; }
    public LocalLogin? LocalLogin { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; }
}
```

### ExternalLogins
```csharp
public class ExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; }       // "discord", "google", "github", "microsoft"
    public string ProviderUserId { get; set; } // external ID from provider
    public string? ProviderEmail { get; set; }
    public string? ProviderDisplayName { get; set; }
    public string? AccessToken { get; set; }   // encrypted, for API calls if needed
    public DateTime LinkedAt { get; set; }
    
    public User User { get; set; }
}
```

### LocalLogins (email/password)
```csharp
public class LocalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }   // BCrypt
    public DateTime CreatedAt { get; set; }
    
    public User User { get; set; }
}
```

### RefreshTokens
```csharp
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; }          // random secure string
    public string? DeviceInfo { get; set; }    // user-agent or device name
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    
    public User User { get; set; }
}
```

### ClientApps (registrerede apps der må bruge auth)
```csharp
public class ClientApp
{
    public Guid Id { get; set; }
    public string ClientId { get; set; }       // "videnstjek", "elevportalen"
    public string Name { get; set; }
    public List<string> RedirectUris { get; set; }  // allowed callback URLs
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## 🔌 API Endpoints

### Login UI
- `GET /login?client_id=xxx&redirect_uri=xxx` — Login-side med provider-knapper + email form
- `GET /register` — Registrering med email/password
- `GET /forgot-password` — Password reset request
- `GET /reset-password?token=xxx` — Password reset form

### OAuth flows
- `GET /auth/{provider}?client_id=xxx&redirect_uri=xxx` — Start OAuth (provider: discord, google, github, microsoft)
- `GET /auth/{provider}/callback` — OAuth callback (intern, håndterer provider response)

### Email/Password
- `POST /auth/local/register` — `{ email, password, displayName }`
- `POST /auth/local/login` — `{ email, password }`
- `POST /auth/local/forgot-password` — `{ email }`
- `POST /auth/local/reset-password` — `{ token, newPassword }`
- `POST /auth/local/confirm-email` — `{ token }`

### Token management
- `POST /auth/refresh` — `{ refreshToken }` → nyt access + refresh token
- `POST /auth/logout` — revoke refresh token
- `POST /auth/revoke-all` — revoke alle refresh tokens for bruger (nødknap)

### User management (kræver gyldig JWT)
- `GET /api/user/me` — brugerinfo (id, displayName, email, linked providers)
- `PUT /api/user/me` — opdater displayName, avatar
- `GET /api/user/me/providers` — liste over linkede providers
- `POST /api/user/me/link/{provider}` — start OAuth flow for at linke ny provider
- `DELETE /api/user/me/link/{provider}` — unlink provider (kun hvis mindst én login-metode er tilbage)

### Admin (kræver admin-rolle)
- `GET /api/admin/users` — liste brugere med pagination + søgning
- `GET /api/admin/users/{id}` — brugerdetaljer
- `DELETE /api/admin/users/{id}` — deaktiver bruger
- `GET /api/admin/clients` — registrerede client apps
- `POST /api/admin/clients` — opret ny client app

### Utility
- `GET /.well-known/jwks.json` — public keys til JWT-validering (client apps bruger dette)
- `GET /health` — health check

---

## 🔒 Token-strategi

### Access Token (JWT)
- **Levetid:** 15 minutter
- **Signering:** RS256 (asymmetrisk — private key på auth-server, public key tilgængelig via JWKS)
- **Claims:**
  ```json
  {
    "sub": "user-guid",
    "name": "Elev Navn",
    "email": "elev@mercantec.dk",
    "roles": ["user"],
    "iss": "https://auth.mercantec.tech",
    "aud": "mercantec-apps",
    "iat": 1234567890,
    "exp": 1234568790
  }
  ```

### Refresh Token
- **Levetid:** 30 dage
- **Format:** Cryptographically random string (ikke JWT)
- **Gemt i DB**, kan revokes individuelt
- **Rotation:** Nyt refresh token ved hver refresh (gammel invalideres)

### Client app integration
Apps skal kun:
1. Redirect til `auth.mercantec.tech/login` med deres `client_id` + `redirect_uri`
2. Modtage JWT i callback
3. Validere JWT med public key fra `/.well-known/jwks.json`
4. Bruge refresh token endpoint til silent refresh

---

## 📧 Email Service

- **Bekræftelsesmail** ved registrering (med token-link)
- **Password reset** mail
- **Brug SMTP** som default (konfigurérbart) — eller Resend/SendGrid via interface
- **Interface:** `IEmailService` så implementeringen kan skiftes

---

## 🏗️ Tech Stack

| Lag | Teknologi |
|-----|-----------|
| Framework | ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Database (prod) | PostgreSQL |
| Database (dev) | SQLite |
| Password hashing | BCrypt.Net-Next |
| JWT | Microsoft.IdentityModel.Tokens |
| OAuth | Microsoft.AspNetCore.Authentication.OAuth |
| Microsoft login | Microsoft.Identity.Web |
| Email | MailKit (SMTP) eller Resend SDK |
| Container | Docker + Docker Compose |
| Reverse proxy | Nginx (eller Caddy for auto-SSL) |

---

## 📁 Mappestruktur

```
auth-mercantec/
├── src/
│   ├── Auth.API/                          # Hoved-projektet
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs          # OAuth + local login endpoints
│   │   │   ├── TokenController.cs         # Refresh, revoke
│   │   │   ├── UserController.cs          # /api/user/me
│   │   │   └── AdminController.cs         # Admin endpoints
│   │   ├── Services/
│   │   │   ├── ITokenService.cs
│   │   │   ├── TokenService.cs            # JWT generation, refresh logic
│   │   │   ├── IEmailService.cs
│   │   │   ├── SmtpEmailService.cs
│   │   │   ├── IOAuthService.cs
│   │   │   └── OAuthService.cs            # Provider-agnostic OAuth handling
│   │   ├── Models/
│   │   │   ├── Entities/
│   │   │   │   ├── User.cs
│   │   │   │   ├── ExternalLogin.cs
│   │   │   │   ├── LocalLogin.cs
│   │   │   │   ├── RefreshToken.cs
│   │   │   │   └── ClientApp.cs
│   │   │   └── DTOs/
│   │   │       ├── LoginRequest.cs
│   │   │       ├── RegisterRequest.cs
│   │   │       ├── TokenResponse.cs
│   │   │       ├── UserInfoResponse.cs
│   │   │       └── ...
│   │   ├── Data/
│   │   │   ├── AuthDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Config/
│   │   │   ├── JwtConfig.cs
│   │   │   ├── OAuthProviderConfig.cs
│   │   │   └── EmailConfig.cs
│   │   ├── Middleware/
│   │   │   └── ErrorHandlingMiddleware.cs
│   │   ├── Pages/                         # Razor Pages for login UI
│   │   │   ├── Login.cshtml
│   │   │   ├── Register.cshtml
│   │   │   ├── ForgotPassword.cshtml
│   │   │   └── ResetPassword.cshtml
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   └── js/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Auth.API.csproj
│   └── Auth.Tests/                        # Unit + integration tests
│       ├── TokenServiceTests.cs
│       ├── AuthFlowTests.cs
│       └── Auth.Tests.csproj
├── docker/
│   ├── Dockerfile
│   ├── docker-compose.yml                 # Dev (SQLite)
│   └── docker-compose.prod.yml            # Prod (PostgreSQL + Nginx)
├── docs/
│   ├── CLIENT-INTEGRATION.md              # Guide til at integrere i nye apps
│   └── API.md                             # Full API docs
├── .github/
│   └── workflows/
│       └── deploy.yml                     # CI/CD (optional)
├── README.md
├── .gitignore
└── auth-mercantec.sln
```

---

## 🐣 MVP Roadmap (Påske-sprint)

### Dag 1: Fundament
- [ ] Solution + projekt setup
- [ ] Datamodel + EF Core DbContext + migrations (SQLite for dev)
- [ ] BCrypt password hashing
- [ ] `POST /auth/local/register` + `POST /auth/local/login`
- [ ] TokenService: JWT generation med RS256
- [ ] `POST /auth/refresh` + `POST /auth/logout`
- [ ] `GET /.well-known/jwks.json`

### Dag 2: OAuth
- [ ] OAuth infrastructure (generisk provider-handling)
- [ ] Discord OAuth implementering
- [ ] Google OAuth implementering
- [ ] Account linking logic (find eksisterende bruger via email, eller opret ny)

### Dag 3: Flere providers + Email
- [ ] GitHub OAuth
- [ ] Microsoft/Entra ID OAuth (forberedt, fuld integration når IT leverer App Registration)
- [ ] Email confirmation flow
- [ ] Password reset flow
- [ ] `IEmailService` + SMTP implementation

### Dag 4: UI + Deploy
- [ ] Login Razor Page (provider-knapper + email form, rent design)
- [ ] Register + Forgot Password pages
- [ ] Docker Compose (dev + prod)
- [ ] Deploy til test-server
- [ ] CLIENT-INTEGRATION.md guide (så du nemt kan koble videnstjek.mags.dk på)

### Post-MVP
- [ ] Admin dashboard (Razor Pages eller Blazor)
- [ ] Rate limiting på login endpoints
- [ ] Bruger-profil side (se linkede providers, skift password)
- [ ] Two-factor auth (TOTP)
- [ ] Audit log (login attempts, provider links)
- [ ] Kobl videnstjek.mags.dk på som første client app

---

## ⚙️ Konfiguration (appsettings.json skeleton)

```json
{
  "Jwt": {
    "Issuer": "https://auth.mercantec.tech",
    "Audience": "mercantec-apps",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 30,
    "PrivateKeyPath": "./keys/private.pem",
    "PublicKeyPath": "./keys/public.pem"
  },
  "OAuth": {
    "Discord": {
      "ClientId": "",
      "ClientSecret": "",
      "CallbackPath": "/auth/discord/callback"
    },
    "Google": {
      "ClientId": "",
      "ClientSecret": "",
      "CallbackPath": "/auth/google/callback"
    },
    "GitHub": {
      "ClientId": "",
      "ClientSecret": "",
      "CallbackPath": "/auth/github/callback"
    },
    "Microsoft": {
      "ClientId": "",
      "ClientSecret": "",
      "TenantId": "",
      "CallbackPath": "/auth/microsoft/callback"
    }
  },
  "Email": {
    "Provider": "smtp",
    "SmtpHost": "",
    "SmtpPort": 587,
    "SmtpUser": "",
    "SmtpPassword": "",
    "FromAddress": "noreply@mercantec.tech",
    "FromName": "Mercantec Auth"
  },
  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=auth.db"
  }
}
```

---

## 🎨 Login UI Guidelines

- **Clean og simpelt** — ikke fancy, bare funktionelt og professionelt
- Provider-knapper med officielle farver/ikoner (Discord lilla, Google rød/blå, GitHub sort, Microsoft blå)
- Email/password form under providerne
- Mobil-responsivt (elever bruger telefon)
- Mercantec branding (logo, farver) hvis tilgængeligt
- Dark mode support (nice to have)

---

## 🔑 Vigtige principper

1. **Security first** — BCrypt, RS256, HTTPS, refresh token rotation, rate limiting
2. **Separation of concerns** — auth-service ved intet om quiz-spørgsmål eller elevportaler. Den laver auth. Period.
3. **Easy client integration** — en ny app skal kunne integrere på under 30 minutter med CLIENT-INTEGRATION.md
4. **Docker-native** — skal deployes med `docker-compose up` og bare virke
5. **No third-party auth dependencies** — vi ER auth-platformen, vi lejer den ikke
