/**
 * Dekoder Mercantec access_token (JWT) payload til visning i SPA — uden signatur-check.
 * Roller bruger typisk claim URI fra .NET; fallback til "role".
 */
function mercantecB64UrlToUtf8(segment) {
  let s = segment.replace(/-/g, "+").replace(/_/g, "/");
  const pad = s.length % 4;
  if (pad) s += "=".repeat(4 - pad);
  const binary = atob(s);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new TextDecoder().decode(bytes);
}

function mercantecDecodeJwtPayload(accessToken) {
  if (!accessToken || typeof accessToken !== "string") return null;
  const parts = accessToken.split(".");
  if (parts.length < 2) return null;
  try {
    return JSON.parse(mercantecB64UrlToUtf8(parts[1]));
  } catch {
    return null;
  }
}

function mercantecJwtExpired(payload) {
  if (!payload || payload.exp == null) return false;
  return payload.exp * 1000 < Date.now();
}

function mercantecRolesFromPayload(payload) {
  if (!payload) return [];
  const roleUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
  let r = payload[roleUri];
  if (Array.isArray(r)) return r.filter(Boolean);
  if (typeof r === "string" && r) return [r];
  r = payload.role;
  if (Array.isArray(r)) return r.filter(Boolean);
  if (typeof r === "string" && r) return [r];
  return [];
}

/** Dansk label for login_method (Mercantec Auth). */
function mercantecLoginMethodDa(method) {
  if (!method) return "—";
  const m = {
    password: "E-mail og adgangskode",
    google: "Google",
    github: "GitHub",
    discord: "Discord",
    microsoft: "Microsoft",
    "microsoft-work": "Microsoft 365 (arbejde/skole)",
    "microsoft-school": "Microsoft (skole)",
    unknown: "Ukendt",
  };
  return m[method] || method;
}
