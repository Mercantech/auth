/**
 * Rydder SPA-tokens og logger ud af Mercantec Auth-cookien på auth-host,
 * derefter redirect tilbage til den aktuelle side (kræver origin i Cors:SpaOrigins).
 */
function mercantecLogout() {
  try {
    sessionStorage.removeItem("mercantec_access_token");
    sessionStorage.removeItem("mercantec_refresh_token");
    sessionStorage.removeItem("pkce_verifier");
    sessionStorage.removeItem("oauth_state");
  } catch (_) {
    /* ignore */
  }
  const cfg = window.__MERCANTEC_AUTH__;
  if (!cfg?.authBaseUrl) {
    console.warn("mercantecLogout: sæt window.__MERCANTEC_AUTH__.authBaseUrl (shared-config.js)");
    return;
  }
  const base = cfg.authBaseUrl.replace(/\/$/, "");
  const back = window.location.href.split("#")[0];
  window.location.href = base + "/signout?returnUrl=" + encodeURIComponent(back);
}
