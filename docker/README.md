# Docker / Dokploy

## Lokal udvikling

```bash
docker compose -f docker/docker-compose.yml up --build
```

Kopiér `docker/.env.example` → `docker/.env` og udfyld værdier.

## Dokploy (produktion)

Standard-indstillinger i Dokploy:

| Felt | Værdi |
|------|--------|
| Compose-fil | `docker/docker-compose.yml` |
| Arbejdsmappe | repo-roden |

### Fejl: container name already in use

Eksempel:

```text
Conflict. The container name "/auth-authprod-zuyvik-api-1" is already in use
```

**Årsag:** Build lykkedes, men en gammel API-container blev ikke fjernet (afbrydt deploy eller compose-state ude af sync). Den kørende container er stadig **gammel kode**.

**Hurtig løsning** (SSH eller Dokploy-server-terminal):

```bash
docker rm -f auth-authprod-zuyvik-api-1
```

Genudrul derefter i Dokploy. Postgres-data og `jwt_keys`-volume påvirkes ikke.

Valgfrit — ryd hele stacken og start forfra (kort nedetid):

```bash
docker compose -p auth-authprod-zuyvik -f docker/docker-compose.yml down
docker compose -p auth-authprod-zuyvik -f docker/docker-compose.yml up -d --build
```

### Forebyggelse: deploy-script

Brug `docker/deploy.sh` som deploy-kommando i Dokploy i stedet for standard `compose up`:

```bash
sh docker/deploy.sh
```

Scriptet fjerner en stuck `*-api-1`-container og kører `up --build --force-recreate`.

Miljøvariabel `COMPOSE_PROJECT_NAME` skal matche Dokploy-projektet (fx `auth-authprod-zuyvik`) hvis det afviger.
