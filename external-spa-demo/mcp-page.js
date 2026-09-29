(() => {
  const cfg = window.MERCANTEC_AUTH_DEMO || {};
  const base = (cfg.authBaseUrl || "https://auth.mercantec.tech").replace(/\/+$/, "");

  const mcpJson = `{
  "mcpServers": {
    "mercantec-auth": {
      "command": "node",
      "args": ["<sti-til-repo>/mcp/build/index.js"],
      "env": {
        "AUTH_BASE_URL": "${base}",
        "AUTH_MCP_API_KEY": "\${env:AUTH_MCP_API_KEY}"
      }
    }
  }
}`;

  const pre = document.getElementById("mcp-json-pre");
  if (pre) pre.textContent = mcpJson;

  const copyBtn = document.getElementById("btn-copy-json");
  const copyStatus = document.getElementById("copy-json-status");
  copyBtn?.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(mcpJson);
      if (copyStatus) copyStatus.textContent = "Kopieret.";
    } catch {
      if (copyStatus) copyStatus.textContent = "Kunne ikke kopiere — markér teksten manuelt.";
    }
  });

  const liveBtn = document.getElementById("btn-live");
  const liveStatus = document.getElementById("live-status");
  const livePre = document.getElementById("live-pre");

  liveBtn?.addEventListener("click", async () => {
    if (liveStatus) liveStatus.textContent = "Henter…";
    if (livePre) {
      livePre.hidden = true;
      livePre.textContent = "";
    }
    try {
      const [healthRes, manifestRes] = await Promise.all([
        fetch(`${base}/health`),
        fetch(`${base}/.well-known/mercantec-auth.json`),
      ]);
      const health = await healthRes.json();
      const manifest = await manifestRes.json();
      const out = {
        health,
        mcp: manifest.mcp ?? null,
        issuer: manifest.issuer,
        audience: manifest.jwt?.audience_must_equal,
        note: "Admin-tools kræver x-api-key — prøv dem fra Cursor, ikke herfra.",
      };
      if (livePre) {
        livePre.hidden = false;
        livePre.textContent = JSON.stringify(out, null, 2);
      }
      if (liveStatus) {
        liveStatus.textContent = healthRes.ok && manifestRes.ok
          ? "OK — discovery virker."
          : `HTTP health=${healthRes.status} manifest=${manifestRes.status}`;
      }
    } catch (err) {
      if (liveStatus) liveStatus.textContent = "Fejl ved fetch.";
      if (livePre) {
        livePre.hidden = false;
        livePre.textContent = String(err);
      }
    }
  });
})();
