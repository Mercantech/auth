# Mercantec Auth

Central **OAuth 2.0**-login (authorization code + **PKCE**), **JWT (RS256)** med **JWKS**, refresh tokens, PostgreSQL og **.NET 10** (ASP.NET Core med **Blazor** `.razor`-UI + interaktiv server).

## Krav

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (valgfrit) til Postgres

## Kør lokalt

1. Start Postgres (eller brug Docker):

```powershell
docker compose -f docker/docker-compose.yml up -d postgres
```

2. Kør API (migrations kører automatisk ved opstart):

```powershell
cd src/Auth.API
dotnet run
```

Standard-URL: `http://localhost:5155` (se `Properties/launchSettings.json`).

3. Udfyld `OAuth:*` i `appsettings` eller brugerhemmeligheder for Google / Microsoft / GitHub / Discord.

4. **Første admin:** sæt `Bootstrap:AdminEmail` til den e-mail du registrerer med — så får den bruger rollen `Admin` ved oprettelse. Alternativt kan du sætte e-mail efter oprettelse manuelt i databasen.

## Docker (API + Postgres + demo-SPA)

```powershell
docker compose -f docker/docker-compose.yml up --build -d
```

- **API:** `http://localhost:8080`
- **OAuth demo-SPA:** `http://localhost:5173`
- JWT-nøgler ligger i volumen `jwt_keys`. Microsoft OAuth i container: brug `docker/.env` — se `docker/.env.example`.

## Ekstern login-test (HTML/JS, anden port)

- [external-spa-demo/](external-spa-demo/) — statisk side på fx port **5173** + PKCE mod auth (inkl. Azure/Microsoft-noter).

## Dokumentation

- [docs/CLIENT-INTEGRATION.md](docs/CLIENT-INTEGRATION.md) — PKCE, endpoints, demo-client
- [auth-mercantec-project.md](auth-mercantec-project.md) — oprindelig spec

## Struktur

- `src/Auth.API` — webapp (Blazor Web App + OAuth controllers + minimale account-endpoints til form-POST)
- `src/Auth.Tests` — tests (udvid efter behov)
- `docker/` — Dockerfile + compose
