/**
 * ai-prompt.js — genererer en kopierbar AI-prompt der forklarer præcis
 * hvordan man integrerer mod Mercantec Auth. Bygger på window.__MERCANTEC_AUTH__
 * så URL'er/issuer/audience altid matcher miljøet demoen peger på.
 */
(function () {
  const cfg = window.__MERCANTEC_AUTH__;
  const base = cfg.authBaseUrl.replace(/\/+$/, "");

  const shared = {
    intro: [
      "Du er en erfaren udvikler-AI. Jeg skal integrere min applikation op imod \u201cMercantec Auth\u201d — en central OAuth 2.0 / OIDC authorization server. Følg instruktionerne herunder PRÆCIST. Gæt ikke på endpoints eller parametre: alt du behøver står her eller i det maskinlæsbare manifest.",
      "",
      "== AUTORITATIVE KILDER (hent dem selv hvis du kan) ==",
      "- Integrations-manifest (JSON, læs den først): " + base + "/.well-known/mercantec-auth.json",
      "- JWKS (offentlige RSA-nøgler til JWT-verifikation): " + base + "/.well-known/jwks.json",
      "- OIDC discovery: " + base + "/.well-known/openid-configuration",
      "",
      "== FASTE VÆRDIER FOR DETTE MILJØ ==",
      "- Auth base-URL: " + base,
      "- JWT issuer (iss skal matche præcist): " + cfg.expectedIssuer,
      "- JWT audience (aud skal matche præcist): " + cfg.expectedAudience,
      "- JWT-algoritme: RS256 (asymmetrisk; valider ALTID mod JWKS, aldrig med delt secret)",
      "- client_id i denne demo: " + cfg.clientId + " (i produktion får du udleveret et rigtigt client_id)",
    ].join("\n"),

    flow: [
      "== OAUTH 2.0-FLOWET (authorization code + PKCE S256) ==",
      "Serveren understøtter KUN authorization code flow med PKCE (S256) og refresh tokens. Ingen implicit flow, ingen password grant.",
      "",
      "Trin 1 — Generér PKCE-par før login:",
      "  - code_verifier: kryptografisk tilfældig streng, 43-128 tegn (RFC 7636).",
      "  - code_challenge = BASE64URL(SHA256(code_verifier)) uden '='-padding.",
      "  - Generér også en tilfældig state-værdi (CSRF-beskyttelse).",
      "  - Gem code_verifier og state midlertidigt (browser: sessionStorage).",
      "",
      "Trin 2 — Send brugeren (fuld browser-navigation, IKKE fetch) til:",
      "  GET " + base + "/oauth/authorize",
      "    ?response_type=code",
      "    &client_id={dit client_id}",
      "    &redirect_uri={din callback-URL — skal matche whitelisten EKSAKT}",
      "    &state={din state}",
      "    &code_challenge={code_challenge}",
      "    &code_challenge_method=S256",
      "",
      "Trin 3 — Brugeren logger ind på auth-hosten (password, passkey, Google, Microsoft, GitHub, Discord …). Har brugeren allerede en fuld session-cookie på auth-domænet, springes login-UI over.",
      "",
      "Trin 4 — Auth-serveren redirecter til din redirect_uri med ?code=...&state=...",
      "  - Tjek at state matcher den du gemte. Ellers: afvis.",
      "",
      "Trin 5 — Byt koden til tokens:",
      "  POST " + base + "/oauth/token",
      "  Content-Type: application/x-www-form-urlencoded",
      "  Body: grant_type=authorization_code, code={code}, redirect_uri={samme som i authorize},",
      "        client_id={dit client_id}, code_verifier={din originale code_verifier}",
      "  (Confidential/backend-klienter sender også client_secret i body'en.)",
      "",
      "  Svar (JSON): access_token (RS256-JWT), refresh_token (roterende opakt token),",
      "  token_type=Bearer, expires_in (sekunder). Ved Microsoft-login kan svaret desuden",
      "  indeholde microsoft_access_token + microsoft_expires_in til Microsoft Graph.",
      "",
      "Trin 6 — Refresh når access token er ved at udløbe (eller ved 401):",
      "  POST " + base + "/oauth/token",
      "  Body: grant_type=refresh_token, refresh_token={seneste refresh_token}, client_id={dit client_id}",
      "  VIGTIGT: refresh_token roterer — gem det NYE refresh_token fra hvert svar og smid det gamle væk.",
    ].join("\n"),

    jwt: [
      "== JWT-VALIDERING (skal ske på alle beskyttede API-kald) ==",
      "1. Hent JWKS fra " + base + "/.well-known/jwks.json (cache gerne, respektér kid ved nøglerotation).",
      "2. Verificér RS256-signaturen med den JWK hvis kid matcher JWT-headerens kid.",
      "3. Kræv: iss == \"" + cfg.expectedIssuer + "\", aud == \"" + cfg.expectedAudience + "\", exp ikke udløbet.",
      "4. Først DEREFTER må du stole på claims.",
      "",
      "Vigtige claims: sub (stabilt bruger-GUID — samme uanset login-metode), name, email (hvis den findes),",
      "role (gentaget claim pr. rolle, brug til autorisation), login_method (fx password, passkey, google,",
      "microsoft-work, github), amr (kun i id_token ved openid-scope: pwd/otp/webauthn), iss, aud, exp, iat.",
    ].join("\n"),

    logout: [
      "== LOGOUT ==",
      "Login sætter en session-cookie på auth-domænet, så næste /oauth/authorize kan auto-logge brugeren ind.",
      "Korrekt logout i en SPA: 1) ryd lokale tokens, 2) fuld browser-navigation til:",
      "  GET " + base + "/signout?returnUrl={encodeURIComponent(din side)}",
      "returnUrl må være en relativ sti eller en absolut URL hvis origin er whitelistet i Cors:SpaOrigins.",
    ].join("\n"),

    registration: [
      "== KLIENTREGISTRERING OG CORS (skal aftales med platform-administrator) ==",
      "- Din app skal registreres som klient (ClientApps) med ALLE redirect-URI'er (dev + produktion) på eksakt match.",
      "- Offentlige klienter (SPA/native): IsPublic=true, ingen client_secret, PKCE påkrævet.",
      "- Fortrolige klienter (backend): client_secret (BCrypt-hashet i DB), sendes i token-kald.",
      "- Browser-baserede apps: din origin skal tilføjes i Cors:SpaOrigins, ellers blokeres fetch til /oauth/token.",
      "Kontakt: mags@mercantec.dk",
    ].join("\n"),
  };

  const scenarios = {
    spa: {
      label: "SPA / frontend",
      task: [
        "== DIN OPGAVE ==",
        "Implementér login i min browser-baserede app (SPA — vanilla JS, React, Vue, Angular eller lignende):",
        "1. En \u201cLog ind\u201d-knap der genererer PKCE-par + state og navigerer til /oauth/authorize (trin 1-2 ovenfor).",
        "2. En callback-side/route der validerer state og bytter code til tokens (trin 4-5).",
        "3. Token-opbevaring: brug sessionStorage (eller memory) — IKKE localStorage. Nøglenavne er valgfri.",
        "4. En fetch-wrapper der sætter 'Authorization: Bearer {access_token}' på kald til mit eget API,",
        "   og som ved 401/udløb prøver refresh-flowet (trin 6) én gang før den giver op.",
        "5. Logout som beskrevet under LOGOUT.",
        "6. Vis brugerens navn/e-mail/roller ved at Base64URL-dekode JWT-payload (kun til visning —",
        "   rigtig validering hører hjemme på backend).",
        "Brug Web Crypto API (crypto.subtle.digest) til SHA-256 i PKCE. Ingen tunge auth-biblioteker nødvendige.",
      ].join("\n"),
    },
    backend: {
      label: "Backend-API der validerer JWT",
      task: [
        "== DIN OPGAVE ==",
        "Implementér JWT-beskyttelse af mit backend-API (fortæl mig hvis du mangler at vide hvilket sprog/framework jeg bruger — fx ASP.NET Core, Node/Express, Python/FastAPI, Go):",
        "1. Middleware/filter der læser 'Authorization: Bearer {jwt}' og validerer som beskrevet under JWT-VALIDERING.",
        "2. Brug et standardbibliotek med JWKS-understøttelse (fx Microsoft.AspNetCore.Authentication.JwtBearer",
        "   med Authority/MetadataAddress, jose/jwks-rsa i Node, PyJWT + PyJWKClient i Python).",
        "3. Rollebaseret autorisation ud fra 'role'-claimen (kan optræde flere gange — én pr. rolle).",
        "   Bemærk til .NET: role mappes til ClaimTypes.Role.",
        "4. Returnér 401 ved manglende/ugyldig token og 403 ved manglende rolle.",
        "5. Backend skal IKKE selv lave login-flow — den modtager kun Bearer-tokens udstedt af auth-serveren.",
        "Cache JWKS med fornuftig TTL og genindlæs ved ukendt kid (nøglerotation).",
      ].join("\n"),
    },
    fullstack: {
      label: "Fuld stack",
      task: [
        "== DIN OPGAVE ==",
        "Implementér BÅDE frontend-login og backend-beskyttelse:",
        "",
        "Frontend (SPA):",
        "1. \u201cLog ind\u201d-knap: generér PKCE-par + state, navigér til /oauth/authorize (trin 1-2).",
        "2. Callback-route: validér state, byt code til tokens (trin 4-5), gem i sessionStorage/memory (ikke localStorage).",
        "3. Fetch-wrapper med 'Authorization: Bearer {access_token}' og automatisk refresh (trin 6) ved 401/udløb.",
        "4. Logout som beskrevet under LOGOUT.",
        "",
        "Backend (API):",
        "5. JWT-middleware der validerer signatur mod JWKS + iss/aud/exp som beskrevet under JWT-VALIDERING.",
        "6. Rollebaseret autorisation via 'role'-claimen; 401 ved ugyldig token, 403 ved manglende rolle.",
        "7. Brug 'sub' (stabilt bruger-GUID) som bruger-nøgle i min egen database — aldrig e-mail.",
        "",
        "Frontend og backend deler IKKE hemmeligheder: frontenden er en offentlig PKCE-klient, backenden validerer kun.",
      ].join("\n"),
    },
  };

  function buildPrompt(scenarioId) {
    const s = scenarios[scenarioId];
    return [
      shared.intro,
      "",
      s.task,
      "",
      shared.flow,
      "",
      shared.jwt,
      "",
      shared.logout,
      "",
      shared.registration,
      "",
      "== ARBEJDSFORM ==",
      "- Spørg mig om sprog/framework og min callback-URL hvis det ikke fremgår af mit projekt.",
      "- Skriv komplet, kørbar kode — ingen pseudokode.",
      "- Hardcod aldrig tokens eller secrets i kildekoden; brug konfiguration/miljøvariabler.",
      "- Verificér dine antagelser mod manifestet på " + base + "/.well-known/mercantec-auth.json før du afviger fra denne prompt.",
    ].join("\n");
  }

  // --- UI-opsætning ---
  const promptPre = document.getElementById("prompt-pre");
  const copyBtn = document.getElementById("btn-copy");
  const scenarioWrap = document.getElementById("scenario-picker");
  let currentScenario = "spa";

  function render() {
    promptPre.textContent = buildPrompt(currentScenario);
    for (const btn of scenarioWrap.querySelectorAll("button[data-scenario]")) {
      btn.classList.toggle("scenario-active", btn.dataset.scenario === currentScenario);
    }
  }

  for (const [id, s] of Object.entries(scenarios)) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "btn btn-secondary scenario-btn";
    btn.dataset.scenario = id;
    btn.textContent = s.label;
    btn.addEventListener("click", () => {
      currentScenario = id;
      render();
    });
    scenarioWrap.appendChild(btn);
  }

  async function copyPrompt() {
    const text = promptPre.textContent;
    let ok = false;
    try {
      await navigator.clipboard.writeText(text);
      ok = true;
    } catch {
      // Fallback til ældre browsere / ikke-sikre origins
      const ta = document.createElement("textarea");
      ta.value = text;
      ta.style.position = "fixed";
      ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      try {
        ok = document.execCommand("copy");
      } catch {
        ok = false;
      }
      ta.remove();
    }
    const original = "Kopiér prompt";
    copyBtn.textContent = ok ? "Kopieret!" : "Kunne ikke kopiere — markér selv teksten";
    copyBtn.classList.toggle("btn-copied", ok);
    setTimeout(() => {
      copyBtn.textContent = original;
      copyBtn.classList.remove("btn-copied");
    }, 2200);
  }

  copyBtn.addEventListener("click", copyPrompt);

  // --- Live manifest ---
  const manifestBtn = document.getElementById("btn-manifest");
  const manifestPre = document.getElementById("manifest-pre");
  const manifestStatus = document.getElementById("manifest-status");
  const manifestUrl = base + "/.well-known/mercantec-auth.json";
  document.getElementById("manifest-url").textContent = manifestUrl;

  manifestBtn.addEventListener("click", async () => {
    manifestStatus.textContent = "Henter …";
    try {
      const res = await fetch(manifestUrl);
      const json = await res.json();
      manifestPre.hidden = false;
      manifestPre.textContent = JSON.stringify(json, null, 2);
      manifestStatus.textContent = "Hentet live fra auth-serveren (" + res.status + "). Det her er præcis hvad en AI-agent også kan læse.";
    } catch (e) {
      manifestStatus.innerHTML = '<span class="demo-text-err">Kunne ikke hente manifestet (CORS eller server nede?): ' +
        (e && e.message ? e.message : String(e)) + "</span>";
    }
  });

  render();
})();
