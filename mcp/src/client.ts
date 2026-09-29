export type AuthClientConfig = {
  baseUrl: string;
  apiKey: string | undefined;
};

export function loadConfig(): AuthClientConfig {
  const baseUrl = (process.env.AUTH_BASE_URL ?? "https://auth.mercantec.tech").replace(/\/+$/, "");
  const apiKey = process.env.AUTH_MCP_API_KEY?.trim() || undefined;
  return { baseUrl, apiKey };
}

export class AuthApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly body: string,
  ) {
    super(message);
    this.name = "AuthApiError";
  }
}

export async function authFetch(
  config: AuthClientConfig,
  path: string,
  init: RequestInit & { requireApiKey?: boolean } = {},
): Promise<unknown> {
  const { requireApiKey = false, headers: initHeaders, ...rest } = init;
  if (requireApiKey && !config.apiKey) {
    throw new AuthApiError(
      "AUTH_MCP_API_KEY mangler i miljøet (kræves til admin-kald).",
      401,
      "",
    );
  }

  const headers = new Headers(initHeaders);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }
  if (config.apiKey) {
    headers.set("x-api-key", config.apiKey);
  }
  if (rest.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const url = `${config.baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  const res = await fetch(url, { ...rest, headers });
  const text = await res.text();

  if (!res.ok) {
    throw new AuthApiError(
      `Auth API ${res.status} ${res.statusText} for ${path}`,
      res.status,
      text,
    );
  }

  if (res.status === 204 || text.length === 0) {
    return null;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

export function textResult(data: unknown): { content: { type: "text"; text: string }[] } {
  const text =
    typeof data === "string" ? data : JSON.stringify(data, null, 2);
  return { content: [{ type: "text", text }] };
}

export function errorResult(err: unknown): {
  content: { type: "text"; text: string }[];
  isError: true;
} {
  if (err instanceof AuthApiError) {
    return {
      isError: true,
      content: [
        {
          type: "text",
          text: `${err.message}\n${err.body || "(tom body)"}`,
        },
      ],
    };
  }
  const message = err instanceof Error ? err.message : String(err);
  return {
    isError: true,
    content: [{ type: "text", text: message }],
  };
}
