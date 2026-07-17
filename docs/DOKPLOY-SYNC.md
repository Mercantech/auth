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
3. **Nulstil adgangskode** (`/Account/LinkedAccounts` når konto findes): `user.remove` + `user.createUserWithCredentials`, derefter gen-push af Auth-ACL

Kun `POST /user.createUserWithCredentials` (ingen invite). Password gemmes ikke i Auth — kun i Dokploy.
Dokploy har ingen admin-set-password for andre brugere, derfor slettes/genoprettes brugeren ved nulstilling.

Projektadgang styres stadig af admin (`/Admin/Dokploy`) eller i Dokploy UI.
## Adgangsanmodninger

- Brugere: [`/dokploy`](/dokploy) — vælg projekter + rettigheder, send anmodning (opretter Dokploy-konto med password hvis mangler)
- Admin: [`/Admin/Dokploy`](/Admin/Dokploy) — **Godkend** / **Afvis** afventende anmodninger
- Ved godkendelse: projekter merges (union), `can*`-flags OR’es ind, derefter `assignPermissions`
  (`id` = Better Auth **userId**, ikke organisation-member-id)
- Admin kan **delvist godkende**: fravælg projekter/rettigheder på anmodningen før “Godkend valgte”
- Ved push ekspanderes hvert valgt projekt til **alle** environments + services under projektet
  (`accessedEnvironments` / `accessedServices`)
- To-vejs ACL-pull læser rettigheder fra `user.all` (member-rækken). `user.getPermissions` bruges **ikke** — den returnerer kun API-nøglens egen session.

## Login

- Apps: Auth (OIDC) som hidtil
- Dokploy UI: Dokploy’s eget login (ingen SSO i v1)
