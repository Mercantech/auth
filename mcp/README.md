# Mercantec Auth MCP

stdio MCP-server til Cursor, der wrapper Mercantec Auth discovery + admin-API via `x-api-key`.

## Krav

- Node 18+
- Auth-server med `Mcp__ApiKey` sat (samme værdi som `AUTH_MCP_API_KEY`)

## Build

```bash
cd mcp
npm install
npm run build
```

## Cursor (`~/.cursor/mcp.json`)

```json
{
  "mcpServers": {
    "mercantec-auth": {
      "command": "node",
      "args": ["C:/Users/Private/Documents/GitHub/auth/mcp/build/index.js"],
      "env": {
        "AUTH_BASE_URL": "https://auth.mercantec.tech",
        "AUTH_MCP_API_KEY": "${env:AUTH_MCP_API_KEY}"
      }
    }
  }
}
```

Sæt `AUTH_MCP_API_KEY` i dit OS-miljø (eller indsæt nøglen direkte i `env` lokalt — commit aldrig nøglen).

## Tools

| Tool | Auth |
|------|------|
| `auth_get_manifest` | Offentlig |
| `auth_get_openid_configuration` | Offentlig |
| `auth_health` | Offentlig |
| `auth_list_clients` / `auth_get_client` / `auth_create_client` / `auth_update_client` / `auth_delete_client` | API-nøgle |
| `auth_add_redirect_uri` / `auth_remove_redirect_uri` / `auth_rotate_client_secret` | API-nøgle |
| `auth_users_directory` / `auth_merge_users` / `auth_delete_user` | API-nøgle |
| `auth_usage_summary` / `auth_usage_events` | API-nøgle |

## Server-env (Dokploy)

```
Mcp__ApiKey=<samme-nøgle-som-AUTH_MCP_API_KEY>
```
