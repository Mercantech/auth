/**
 * Dekoder JWT til visning og verificerer RS256 mod /.well-known/jwks.json via jose (CDN).
 */
import * as jose from "https://esm.sh/jose@5.9.6";

function b64UrlToUtf8(segment) {
  let s = segment.replace(/-/g, "+").replace(/_/g, "/");
  const pad = s.length % 4;
  if (pad) s += "=".repeat(4 - pad);
  const binary = atob(s);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new TextDecoder().decode(bytes);
}

function decodeJwtParts(token) {
  const [h, p] = token.split(".");
  if (!h || !p) throw new Error("Ugyldigt JWT-format");
  return {
    header: JSON.parse(b64UrlToUtf8(h)),
    payload: JSON.parse(b64UrlToUtf8(p)),
  };
}

function cfg() {
  return window.__MERCANTEC_AUTH__;
}

/** Vis den konkrete JWKS-URL på undervisningssiden (jwt.html). */
function showJwksUrlForTeaching() {
  const el = document.getElementById("jwks-url-display");
  const c = cfg();
  if (!el || !c?.authBaseUrl) return;
  const base = c.authBaseUrl.endsWith("/") ? c.authBaseUrl : c.authBaseUrl + "/";
  el.textContent = new URL("/.well-known/jwks.json", base).href;
}

showJwksUrlForTeaching();

const token = sessionStorage.getItem("mercantec_access_token");
const elDecode = document.getElementById("decode");
const elVerify = document.getElementById("verify");
const elPayload = document.getElementById("payload-pre");
const elHeader = document.getElementById("header-pre");

if (!token) {
  elDecode.textContent = "Ingen access token i session. Log ind fra forsiden først.";
  elVerify.textContent = "—";
} else {
  try {
    const { header, payload } = decodeJwtParts(token);
    elHeader.textContent = JSON.stringify(header, null, 2);
    elPayload.textContent = JSON.stringify(payload, null, 2);
    elDecode.textContent = "Payload og header er dekodet (uden at stole på signatur).";
  } catch (e) {
    elDecode.textContent = "Kunne ikke dekode: " + e.message;
  }

  (async () => {
    const c = cfg();
    const jwksUrl = new URL("/.well-known/jwks.json", c.authBaseUrl.endsWith("/") ? c.authBaseUrl : c.authBaseUrl + "/");
    try {
      const JWKS = jose.createRemoteJWKSet(jwksUrl);
      const { payload } = await jose.jwtVerify(token, JWKS, {
        issuer: c.expectedIssuer,
        audience: c.expectedAudience,
      });
      const lm = payload.login_method != null ? " <code>login_method</code> = <code>" + payload.login_method + "</code>." : "";
      elVerify.innerHTML =
        '<span style="color:#00ba7c">Signatur OK</span> — JWT er udstedt af denne backend og issuer/audience stemmer. ' +
        "<code>sub</code> = <code>" +
        (payload.sub ?? "—") +
        "</code>." +
        lm;
    } catch (e) {
      elVerify.innerHTML =
        '<span style="color:#f4212e">Verifikation fejlede:</span> ' +
        (e && e.message ? e.message : String(e)) +
        "<br/><small>Tjek at expectedIssuer / expectedAudience i shared-config.js matcher API.</small>";
    }
  })();
}
