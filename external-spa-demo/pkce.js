/**
 * PKCE helpers (RFC 7636) — ren browser, ingen dependencies.
 */
function randomVerifier(length = 64) {
  const bytes = new Uint8Array(length);
  crypto.getRandomValues(bytes);
  const charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
  let s = "";
  for (let i = 0; i < length; i++) s += charset[bytes[i] % charset.length];
  return s;
}

async function sha256Base64Url(plain) {
  const data = new TextEncoder().encode(plain);
  const hash = await crypto.subtle.digest("SHA-256", data);
  const b64 = btoa(String.fromCharCode(...new Uint8Array(hash)));
  return b64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/u, "");
}

function buildAuthorizeUrl({ authBaseUrl, clientId, redirectUri, state, codeChallenge }) {
  const u = new URL("/oauth/authorize", authBaseUrl);
  u.searchParams.set("response_type", "code");
  u.searchParams.set("client_id", clientId);
  u.searchParams.set("redirect_uri", redirectUri);
  u.searchParams.set("state", state);
  u.searchParams.set("code_challenge", codeChallenge);
  u.searchParams.set("code_challenge_method", "S256");
  return u.toString();
}
