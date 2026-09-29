export function loadConfig() {
    const baseUrl = (process.env.AUTH_BASE_URL ?? "https://auth.mercantec.tech").replace(/\/+$/, "");
    const apiKey = process.env.AUTH_MCP_API_KEY?.trim() || undefined;
    return { baseUrl, apiKey };
}
export class AuthApiError extends Error {
    status;
    body;
    constructor(message, status, body) {
        super(message);
        this.status = status;
        this.body = body;
        this.name = "AuthApiError";
    }
}
export async function authFetch(config, path, init = {}) {
    const { requireApiKey = false, headers: initHeaders, ...rest } = init;
    if (requireApiKey && !config.apiKey) {
        throw new AuthApiError("AUTH_MCP_API_KEY mangler i miljøet (kræves til admin-kald).", 401, "");
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
        throw new AuthApiError(`Auth API ${res.status} ${res.statusText} for ${path}`, res.status, text);
    }
    if (res.status === 204 || text.length === 0) {
        return null;
    }
    try {
        return JSON.parse(text);
    }
    catch {
        return text;
    }
}
export function textResult(data) {
    const text = typeof data === "string" ? data : JSON.stringify(data, null, 2);
    return { content: [{ type: "text", text }] };
}
export function errorResult(err) {
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
//# sourceMappingURL=client.js.map