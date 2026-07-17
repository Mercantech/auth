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

Ved lokal signup (`/Account/Register`) vises checkboxen **Opret også konto på deploy**, når integrationen er enabled.

- Match på e-mail via `GET /user.all`
- Ellers `POST /organization.inviteMember` (fallback: `user.createUserWithCredentials`)
- Best-effort: Auth-signup fejler ikke, hvis Dokploy er nede

## Projekt-ACL (to-vejs)

- Admin: `/Admin/Dokploy` — vælg projekter pr. bruger → `assignPermissions` (Auth sætter `AclDirty` og pusher)
- Ændringer i Dokploy UI importeres ved periodisk sync / **Sync nu**, når Auth **ikke** er dirty (`GET /user.getPermissions`)

## Login

- Apps: Auth (OIDC) som hidtil
- Dokploy UI: Dokploy’s eget login (ingen SSO i v1)
