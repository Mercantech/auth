import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { authFetch, errorResult, loadConfig, textResult } from "./client.js";

export function createServer(): McpServer {
  const config = loadConfig();
  const server = new McpServer({
    name: "mercantec-auth",
    version: "1.0.0",
  });

  // --- Discovery ---

  server.tool(
    "auth_get_manifest",
    "Hent Mercantec Auth integrations-manifest (/.well-known/mercantec-auth.json).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/.well-known/mercantec-auth.json");
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_get_openid_configuration",
    "Hent OIDC discovery-dokument (/.well-known/openid-configuration).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/.well-known/openid-configuration");
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_health",
    "Tjek Auth liveness (GET /health).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/health");
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  // --- Clients ---

  server.tool(
    "auth_list_clients",
    "List alle OAuth-klienter (kræver AUTH_MCP_API_KEY).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/api/admin/clients", { requireApiKey: true });
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_get_client",
    "Hent én OAuth-klient inkl. redirect-URI'er (kræver AUTH_MCP_API_KEY).",
    { clientId: z.string().min(2).describe("OAuth client_id") },
    async ({ clientId }) => {
      try {
        const data = await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}`,
          { requireApiKey: true },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_create_client",
    "Opret OAuth-klient. For confidential (isPublic=false) returneres clientSecretPlaintext én gang — gem det sikkert.",
    {
      name: z.string().min(1).max(120),
      clientId: z.string().min(2).max(64),
      isActive: z.boolean().optional(),
      isPublic: z.boolean().optional().describe("true = SPA/PKCE uden secret (default)"),
      requirePkce: z.boolean().optional(),
      allowedScopes: z.string().optional(),
    },
    async (args) => {
      try {
        const data = await authFetch(config, "/api/admin/clients", {
          method: "POST",
          requireApiKey: true,
          body: JSON.stringify({
            name: args.name,
            clientId: args.clientId,
            isActive: args.isActive,
            isPublic: args.isPublic,
            requirePkce: args.requirePkce,
            allowedScopes: args.allowedScopes,
          }),
        });
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_update_client",
    "Opdatér OAuth-klient-indstillinger (PATCH). Brug clear*-flag for at nulstille valgfrie felter.",
    {
      clientId: z.string().min(2),
      name: z.string().min(1).max(120).optional(),
      newClientId: z.string().min(2).max(64).optional(),
      isActive: z.boolean().optional(),
      isPublic: z.boolean().optional(),
      requirePkce: z.boolean().optional(),
      allowedScopes: z.string().optional(),
      accessTokenExpiryMinutes: z.number().int().optional(),
      clearAccessTokenExpiryMinutes: z.boolean().optional(),
      refreshTokenExpiryDays: z.number().int().optional(),
      clearRefreshTokenExpiryDays: z.boolean().optional(),
      loginThemeId: z.string().optional(),
      clearLoginThemeId: z.boolean().optional(),
      allowedLoginMethods: z.array(z.string()).optional(),
      useAllLoginMethods: z.boolean().optional(),
      requiredLinkedProviders: z.array(z.string()).optional(),
      clearRequiredLinkedProviders: z.boolean().optional(),
    },
    async ({ clientId, ...body }) => {
      try {
        const data = await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}`,
          {
            method: "PATCH",
            requireApiKey: true,
            body: JSON.stringify(body),
          },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_delete_client",
    "Slet en OAuth-klient permanent (kræver AUTH_MCP_API_KEY). Kan ikke fortrydes.",
    { clientId: z.string().min(2) },
    async ({ clientId }) => {
      try {
        await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}`,
          { method: "DELETE", requireApiKey: true },
        );
        return textResult({ deleted: true, clientId });
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_add_redirect_uri",
    "Tilføj redirect URI til en OAuth-klient (eksakt match kræves i authorize).",
    {
      clientId: z.string().min(2),
      uri: z.string().url().describe("Absolut http(s) URL"),
    },
    async ({ clientId, uri }) => {
      try {
        const data = await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}/redirect-uris`,
          {
            method: "POST",
            requireApiKey: true,
            body: JSON.stringify({ uri }),
          },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_remove_redirect_uri",
    "Fjern en redirect URI fra en OAuth-klient.",
    {
      clientId: z.string().min(2),
      redirectUriId: z.string().uuid(),
    },
    async ({ clientId, redirectUriId }) => {
      try {
        const data = await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}/redirect-uris/${encodeURIComponent(redirectUriId)}`,
          { method: "DELETE", requireApiKey: true },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_rotate_client_secret",
    "Roter client secret for confidential klient. Returnerer nyt plaintext secret én gang — gem det sikkert.",
    { clientId: z.string().min(2) },
    async ({ clientId }) => {
      try {
        const data = await authFetch(
          config,
          `/api/admin/clients/${encodeURIComponent(clientId)}/rotate-secret`,
          { method: "POST", requireApiKey: true },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  // --- Users ---

  server.tool(
    "auth_users_directory",
    "List brugere med providers, e-mails og klient-brug (kræver AUTH_MCP_API_KEY).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/api/admin/users-directory", {
          requireApiKey: true,
        });
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_merge_users",
    "Sammenlæg donor-bruger ind i survivor (survivor beholder JWT sub). Begge refresh tokens invalideres.",
    {
      survivorUserId: z.string().uuid(),
      donorUserId: z.string().uuid(),
    },
    async ({ survivorUserId, donorUserId }) => {
      try {
        const data = await authFetch(config, "/api/admin/users/merge", {
          method: "POST",
          requireApiKey: true,
          body: JSON.stringify({ survivorUserId, donorUserId }),
        });
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_delete_user",
    "Slet en brugerkonto permanent. Kan ikke slette sidste Admin.",
    { userId: z.string().uuid() },
    async ({ userId }) => {
      try {
        await authFetch(config, `/api/admin/users/${encodeURIComponent(userId)}`, {
          method: "DELETE",
          requireApiKey: true,
        });
        return textResult({ deleted: true, userId });
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  // --- Usage ---

  server.tool(
    "auth_usage_summary",
    "Aggregeret Auth-brug pr. klient og seneste events (kræver AUTH_MCP_API_KEY).",
    {},
    async () => {
      try {
        const data = await authFetch(config, "/api/admin/usage/summary", {
          requireApiKey: true,
        });
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  server.tool(
    "auth_usage_events",
    "Filtrerbar Auth usage event-log.",
    {
      userId: z.string().uuid().optional(),
      clientId: z.string().optional(),
      eventType: z.string().optional(),
      limit: z.number().int().min(1).max(500).optional(),
    },
    async ({ userId, clientId, eventType, limit }) => {
      try {
        const qs = new URLSearchParams();
        if (userId) qs.set("userId", userId);
        if (clientId) qs.set("clientId", clientId);
        if (eventType) qs.set("eventType", eventType);
        if (limit != null) qs.set("limit", String(limit));
        const q = qs.toString();
        const data = await authFetch(
          config,
          `/api/admin/usage/events${q ? `?${q}` : ""}`,
          { requireApiKey: true },
        );
        return textResult(data);
      } catch (err) {
        return errorResult(err);
      }
    },
  );

  return server;
}
