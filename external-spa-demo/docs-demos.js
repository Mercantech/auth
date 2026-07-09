/**
 * docs-demos.js — interaktive undervisnings-demoer på docs.html.
 * Genbruger pkce.js (randomVerifier, sha256Base64Url, buildAuthorizeUrl)
 * og session-jwt.js (mercantecDecodeJwtPayload, mercantecMaskEmail, …).
 */
(function () {
  const cfg = window.__MERCANTEC_AUTH__;
  const base = cfg.authBaseUrl.replace(/\/+$/, "");
  const $ = (id) => document.getElementById(id);

  function b64UrlEncodeText(text) {
    const bytes = new TextEncoder().encode(text);
    let binary = "";
    for (const b of bytes) binary += String.fromCharCode(b);
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/u, "");
  }

  function b64UrlDecodeText(segment) {
    let s = segment.replace(/-/g, "+").replace(/_/g, "/");
    const pad = s.length % 4;
    if (pad) s += "=".repeat(4 - pad);
    const binary = atob(s);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return new TextDecoder().decode(bytes);
  }

  /* ---------- 1. PKCE-legeplads ---------- */
  const pkceInput = $("pkce-verifier-input");
  const pkceChallenge = $("pkce-challenge-out");

  async function updatePkce() {
    const v = pkceInput.value;
    if (!v) {
      pkceChallenge.textContent = "—";
      return;
    }
    if (v.length < 43) {
      pkceChallenge.textContent = "(verifier skal være mindst 43 tegn — skriv videre eller generér)";
      return;
    }
    pkceChallenge.textContent = await sha256Base64Url(v);
  }

  $("pkce-generate").addEventListener("click", () => {
    pkceInput.value = randomVerifier(64);
    updatePkce();
  });
  pkceInput.addEventListener("input", updatePkce);
  pkceInput.value = randomVerifier(64);
  updatePkce();

  /* ---------- 2. Authorize-URL-bygger ---------- */
  const azClient = $("az-client");
  const azRedirect = $("az-redirect");
  const azState = $("az-state");
  const azOut = $("az-url-out");

  azClient.value = cfg.clientId;
  azRedirect.value = window.location.origin + "/callback.html";
  azState.value = randomVerifier(24);

  async function updateAuthorizeUrl() {
    const verifier = pkceInput.value || randomVerifier(64);
    const challenge = verifier.length >= 43 ? await sha256Base64Url(verifier) : "(challenge)";
    const u = new URL("/oauth/authorize", base);
    const parts = [
      ["response_type", "code"],
      ["client_id", azClient.value || "(mangler)"],
      ["redirect_uri", azRedirect.value || "(mangler)"],
      ["state", azState.value || "(mangler)"],
      ["code_challenge", challenge],
      ["code_challenge_method", "S256"],
    ];
    azOut.innerHTML =
      '<span class="az-base">' + u.origin + u.pathname + "</span>" +
      parts
        .map(
          ([k, v], i) =>
            '<span class="az-sep">' + (i === 0 ? "?" : "&") + "</span>" +
            '<span class="az-key">' + k + "</span>=" +
            '<span class="az-val az-val-' + k + '">' + encodeURIComponent(v) + "</span>",
        )
        .join("");
  }

  for (const el of [azClient, azRedirect, azState]) el.addEventListener("input", updateAuthorizeUrl);
  pkceInput.addEventListener("input", updateAuthorizeUrl);
  $("pkce-generate").addEventListener("click", updateAuthorizeUrl);
  $("az-new-state").addEventListener("click", () => {
    azState.value = randomVerifier(24);
    updateAuthorizeUrl();
  });
  updateAuthorizeUrl();

  /* ---------- 3. Live token-status + refresh ---------- */
  const tokenStatus = $("live-token-status");
  const tokenBar = $("live-token-bar");
  const tokenBarFill = $("live-token-bar-fill");
  const refreshBtn = $("live-refresh-btn");
  const refreshOut = $("live-refresh-out");
  let tokenTimer = null;

  function renderTokenStatus() {
    const t = sessionStorage.getItem("mercantec_access_token");
    const payload = mercantecDecodeJwtPayload(t);
    if (!t || !payload || payload.exp == null || payload.iat == null) {
      tokenStatus.innerHTML =
        'Ingen access token i denne session — <a href="index.html">log ind fra forsiden</a> for at se din egen token-nedtælling og prøve refresh.';
      tokenBar.hidden = true;
      refreshBtn.disabled = !sessionStorage.getItem("mercantec_refresh_token");
      return;
    }
    const now = Date.now() / 1000;
    const total = payload.exp - payload.iat;
    const left = payload.exp - now;
    tokenBar.hidden = false;
    refreshBtn.disabled = false;
    if (left <= 0) {
      tokenStatus.innerHTML =
        '<span class="demo-text-err">Dit access token er udløbet</span> (exp er passeret). Præcis nu ville et API-kald svare 401 — prøv refresh-knappen.';
      tokenBarFill.style.width = "0%";
      return;
    }
    const pct = Math.max(0, Math.min(100, (left / total) * 100));
    tokenBarFill.style.width = pct.toFixed(1) + "%";
    const mins = Math.floor(left / 60);
    const secs = Math.floor(left % 60);
    tokenStatus.innerHTML =
      "Dit access token udløber om <strong>" + mins + " min " + String(secs).padStart(2, "0") + " sek</strong> " +
      "(levetid i alt: " + Math.round(total / 60) + " min). Når baren rammer nul, afvises Bearer-kald med 401.";
  }

  async function tryRefresh() {
    const rt = sessionStorage.getItem("mercantec_refresh_token");
    if (!rt) {
      refreshOut.innerHTML = '<span class="demo-text-err">Intet refresh token i sessionStorage — log ind først.</span>';
      return;
    }
    refreshBtn.disabled = true;
    refreshOut.textContent = "Kalder POST /oauth/token med grant_type=refresh_token …";
    try {
      const res = await fetch(base + "/oauth/token", {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({
          grant_type: "refresh_token",
          refresh_token: rt,
          client_id: cfg.clientId,
        }),
      });
      const json = await res.json().catch(() => null);
      if (!res.ok || !json || !json.access_token) {
        refreshOut.innerHTML =
          '<span class="demo-text-err">Refresh afvist (' + res.status + ")</span> — refresh tokenet er måske udløbet eller allerede roteret. Log ind igen.";
        return;
      }
      const oldRt = rt;
      sessionStorage.setItem("mercantec_access_token", json.access_token);
      if (json.refresh_token) sessionStorage.setItem("mercantec_refresh_token", json.refresh_token);
      const rotated = json.refresh_token && json.refresh_token !== oldRt;
      refreshOut.innerHTML =
        '<span class="demo-text-ok">Nyt access token modtaget!</span> ' +
        (rotated
          ? "Bemærk: refresh tokenet <strong>roterede</strong> også — det gamle er nu ugyldigt. Præcis derfor skal man altid gemme det nye fra hvert svar."
          : "") +
        " (expires_in: " + (json.expires_in ?? "?") + " sek)";
      renderTokenStatus();
    } catch (e) {
      refreshOut.innerHTML = '<span class="demo-text-err">Netværksfejl (CORS eller server nede?):</span> ' + (e && e.message ? e.message : String(e));
    } finally {
      refreshBtn.disabled = false;
    }
  }

  refreshBtn.addEventListener("click", tryRefresh);
  renderTokenStatus();
  tokenTimer = setInterval(renderTokenStatus, 1000);

  /* ---------- 4. Base64URL: kodning er ikke kryptering ---------- */
  const b64Input = $("b64-input");
  const b64Out = $("b64-out");
  const b64Back = $("b64-back");

  function updateB64() {
    const text = b64Input.value;
    if (!text) {
      b64Out.textContent = "—";
      b64Back.textContent = "—";
      return;
    }
    const encoded = b64UrlEncodeText(text);
    b64Out.textContent = encoded;
    b64Back.textContent = b64UrlDecodeText(encoded);
  }

  b64Input.addEventListener("input", updateB64);
  b64Input.value = '{"sub":"1234","role":"Admin"}';
  updateB64();

  /* ---------- 5. Tamper-demo: manipulér payload, se signaturen fejle ---------- */
  const tamperArea = $("tamper-payload");
  const tamperBtn = $("tamper-verify-btn");
  const tamperReset = $("tamper-reset-btn");
  const tamperOut = $("tamper-out");
  const tamperState = { token: null };

  function loadTamper() {
    const t = sessionStorage.getItem("mercantec_access_token");
    tamperState.token = t || null;
    if (!t) {
      tamperArea.value = "";
      tamperArea.disabled = true;
      tamperBtn.disabled = true;
      tamperReset.disabled = true;
      tamperOut.innerHTML = 'Kræver login — <a href="index.html">log ind fra forsiden</a>, så du kan pille ved dit eget token.';
      return;
    }
    const payload = mercantecDecodeJwtPayload(t);
    const display = typeof mercantecRedactJwtPayloadForDisplay === "function" ? mercantecRedactJwtPayloadForDisplay(payload) : payload;
    tamperArea.value = JSON.stringify(display, null, 2);
    tamperArea.disabled = false;
    tamperBtn.disabled = false;
    tamperReset.disabled = false;
    tamperOut.textContent = "Redigér payloaden (giv dig fx selv rolle Admin) og tryk Verificér.";
  }

  async function verifyTampered() {
    if (!tamperState.token) return;
    let editedPayload;
    try {
      editedPayload = JSON.parse(tamperArea.value);
    } catch {
      tamperOut.innerHTML = '<span class="demo-text-err">Payload er ikke gyldig JSON.</span>';
      return;
    }
    tamperBtn.disabled = true;
    tamperOut.textContent = "Verificerer mod JWKS …";
    try {
      const [h, , sig] = tamperState.token.split(".");
      const originalPayloadJson = JSON.stringify(mercantecDecodeJwtPayload(tamperState.token));
      // Sammenlign mod den *rå* payload (maskeret e-mail i textarea tæller som ændring
      // hvis brugeren ikke har rørt noget — så vi normaliserer: uændret felt-sæt = original token).
      const redacted = typeof mercantecRedactJwtPayloadForDisplay === "function"
        ? JSON.stringify(mercantecRedactJwtPayloadForDisplay(JSON.parse(originalPayloadJson)))
        : originalPayloadJson;
      const isUntouched = JSON.stringify(editedPayload) === redacted;
      const candidate = isUntouched
        ? tamperState.token
        : h + "." + b64UrlEncodeText(JSON.stringify(editedPayload)) + "." + sig;

      const jose = await import("https://esm.sh/jose@5.9.6");
      const jwks = jose.createRemoteJWKSet(new URL(base + "/.well-known/jwks.json"));
      try {
        await jose.jwtVerify(candidate, jwks, { issuer: cfg.expectedIssuer, audience: cfg.expectedAudience });
        tamperOut.innerHTML =
          '<span class="demo-text-ok">Signatur OK</span> — payloaden er uændret, så serverens signatur passer stadig på header + payload.';
      } catch (e) {
        tamperOut.innerHTML = isUntouched
          ? '<span class="demo-text-err">Verifikation fejlede — også uden ændringer.</span> ' +
            "Tokenet er sikkert udløbet (<code>exp</code>) — brug refresh-demoen i afsnit 5 og prøv igen. " +
            "<small>(" + (e && e.message ? e.message : String(e)) + ")</small>"
          : '<span class="demo-text-err">Verifikation FEJLEDE</span> — ' +
            "du ændrede payloaden, men signaturen blev lavet over den <em>originale</em> payload med serverens private nøgle. " +
            "Uden den private nøgle kan du ikke lave en ny gyldig signatur. Præcis sådan opdager et API forfalskede tokens. " +
            "<small>(" + (e && e.message ? e.message : String(e)) + ")</small>";
      }
    } catch (e) {
      tamperOut.innerHTML =
        '<span class="demo-text-err">Kunne ikke verificere (netværk/CORS?):</span> ' +
        (e && e.message ? e.message : String(e));
    } finally {
      tamperBtn.disabled = false;
    }
  }

  tamperBtn.addEventListener("click", verifyTampered);
  tamperReset.addEventListener("click", loadTamper);
  loadTamper();

  /* ---------- 6. state/CSRF-simulator ---------- */
  const csrfExpected = $("csrf-expected");
  const csrfOut = $("csrf-out");
  let expectedState = randomVerifier(16);
  csrfExpected.textContent = expectedState;

  $("csrf-good").addEventListener("click", () => {
    csrfOut.innerHTML =
      "Callback ankom med <code>state=" + expectedState + "</code> → matcher det gemte → " +
      '<span class="demo-text-ok">flowet fortsætter</span>: koden byttes til tokens.';
  });
  $("csrf-bad").addEventListener("click", () => {
    const evil = randomVerifier(16);
    csrfOut.innerHTML =
      "Callback ankom med <code>state=" + evil + "</code> → matcher IKKE det gemte (<code>" + expectedState + "</code>) → " +
      '<span class="demo-text-err">afvist</span>. En angriber prøvede at plante sin egen kode i din session — appen rører den aldrig.';
  });
  $("csrf-new").addEventListener("click", () => {
    expectedState = randomVerifier(16);
    csrfExpected.textContent = expectedState;
    csrfOut.textContent = "Ny state genereret og “gemt i sessionStorage”. Prøv de to callbacks igen.";
  });

  /* ---------- 7. redirect_uri: eksakt match ---------- */
  const ruInput = $("ru-input");
  const ruOut = $("ru-out");
  const whitelist = [window.location.origin + "/callback.html"];
  $("ru-whitelist").textContent = whitelist[0];

  function checkRedirectUri() {
    const v = ruInput.value.trim();
    if (!v) {
      ruOut.textContent = "—";
      return;
    }
    if (whitelist.includes(v)) {
      ruOut.innerHTML = '<span class="demo-text-ok">Godkendt</span> — præcis match mod whitelisten.';
      return;
    }
    let hint = "";
    try {
      const url = new URL(v);
      const wl = new URL(whitelist[0]);
      if (url.protocol !== wl.protocol) hint = "Skemaet er anderledes (" + url.protocol + " vs " + wl.protocol + ") — http og https er to forskellige URI'er.";
      else if (url.host !== wl.host) hint = "Host/port er anderledes (" + url.host + " vs " + wl.host + ") — selv en anden port afvises.";
      else if (url.pathname !== wl.pathname) hint = "Stien er anderledes (" + url.pathname + " vs " + wl.pathname + ") — også en trailing slash eller stort/småt gør en forskel.";
      else if (url.search) hint = "Query-parametre (" + url.search + ") gør at strengen ikke matcher præcist.";
      else hint = "Strengen matcher ikke tegn-for-tegn.";
    } catch {
      hint = "Ikke en gyldig absolut URL.";
    }
    ruOut.innerHTML = '<span class="demo-text-err">Afvist</span> — ' + hint +
      " Uden eksakt match kunne en angriber få koden sendt til sit eget domæne.";
  }

  ruInput.addEventListener("input", checkRedirectUri);
  ruInput.value = whitelist[0] + "/";
  checkRedirectUri();
})();
