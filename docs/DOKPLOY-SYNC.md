# Dokploy-sync (Auth → Dokploy)

Auth kan provisionere Dokploy-brugere og synce **projekt-ACL** (`accessedProjects`) to-vejs.

## Aktivering

```env
Dokploy__Enabled=true
Dokploy__BaseUrl=https://deploy.mags.dk/api
Dokploy__ApiKey=<service-api-key>
Dokploy__AclSyncIntervalMinutes=15
Dokploy__MemberRole=member
```

Alle kald bruger header **`x-api-key`** (ikke Bearer).

## Brugere (1-way, opt-in)

1. **Ved signup** (`/Account/Register`): checkbox **Opret også konto på deploy** — Auth opretter Dokploy-bruger med **samme e-mail + adgangskode**
2. **Self-service** (`/Account/LinkedAccounts`): vælg en Dokploy-adgangskode → **Opret Dokploy-bruger**

Kun `POST /user.createUserWithCredentials` (ingen invite). Password gemmes ikke i Auth — kun i Dokploy.

Projektadgang styres stadig af admin (`/Admin/Dokploy`) eller i Dokploy UI.
## Projekt-ACL (to-vejs)

- Admin: `/Admin/Dokploy` — vælg projekter pr. bruger → `assignPermissions` (Auth sætter `AclDirty` og pusher)
- Ændringer i Dokploy UI importeres ved periodisk sync / **Sync nu**, når Auth **ikke** er dirty (`GET /user.getPermissions`)

## Login

- Apps: Auth (OIDC) som hidtil
- Dokploy UI: Dokploy’s eget login (ingen SSO i v1)
