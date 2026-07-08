const c = window.__MERCANTEC_AUTH__;
const token = sessionStorage.getItem("mercantec_access_token");
const statusEl = document.getElementById("status");
const table = document.getElementById("users-table");
const mergeBar = document.getElementById("merge-bar");
const mergeBtn = document.getElementById("merge-btn");

if (mergeBtn) mergeBtn.addEventListener("click", () => mergeSelected());

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

function escapeAttr(s) {
  return String(s)
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

async function fetchDirectory() {
  const res = await fetch(c.authBaseUrl + "/api/admin/users-directory", {
    headers: { Authorization: "Bearer " + token },
  });
  const text = await res.text();
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    data = null;
  }
  return { res, text, data };
}

function wireTableDeletes(tbody) {
  tbody.addEventListener("click", async (ev) => {
    const btn = ev.target.closest("button[data-delete-id]");
    if (!btn) return;
    const id = btn.getAttribute("data-delete-id");
    const name = btn.getAttribute("data-delete-name") || "";
    await deleteUser(id, name);
  });
}

async function mergeSelected() {
  const surv = document.querySelector('input[name="demo-merge-survivor"]:checked');
  const donor = document.querySelector('input[name="demo-merge-donor"]:checked');
  if (!surv || !donor) {
    alert("Vælg både hovedkonto (bevar JWT sub) og konto der nedlægges.");
    return;
  }
  if (surv.value === donor.value) {
    alert("Vælg to forskellige brugere.");
    return;
  }
  if (!confirm("Samme juridiske person?\nOBS: Auth flytter login — andre databaser omkring bruger-GUID migreres IKKE automatisk.\nFortsæt?")) return;

  mergeBtn.disabled = true;
  try {
    const res = await fetch(c.authBaseUrl + "/api/admin/users/merge", {
      method: "POST",
      headers: {
        Authorization: "Bearer " + token,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        survivorUserId: surv.value,
        donorUserId: donor.value,
      }),
    });
    const text = await res.text();
    let data;
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }

    if (res.ok) {
      const w =
        Array.isArray(data.warnings) && data.warnings.length
          ? " " + data.warnings.join(" ")
          : "";
      statusEl.innerHTML =
        '<span style="font-weight:600;color:var(--demo-ok)">Sammenlægning lykkedes.</span> ' +
        escapeHtml(w).replace(/^\s/, "");
      await redraw();
      return;
    }

    statusEl.innerHTML =
      '<span class="demo-warn">Fejl ' + res.status + ":</span> " + escapeHtml(text.slice(0, 520));
  } catch (e) {
    statusEl.textContent = "Netværksfejl: " + e.message;
  } finally {
    mergeBtn.disabled = false;
  }
}

async function deleteUser(rowId, displayName) {
  if (
    !confirm(
      'Slette brugeren «' + displayName + "» permanent?\nAlle OAuth-tilknytninger og tokens fjernes (auth-databasen).",
    )
  )
    return;

  try {
    const res = await fetch(c.authBaseUrl + "/api/admin/users/" + encodeURIComponent(rowId), {
      method: "DELETE",
      headers: { Authorization: "Bearer " + token },
    });

    if (res.status === 204) {
      statusEl.innerHTML = '<span style="font-weight:600;color:var(--demo-ok)">Bruger slettet.</span>';
      await redraw();
      return;
    }

    const text = await res.text();
    statusEl.innerHTML =
      '<span class="demo-warn">Fejl ' + res.status + ":</span> " + escapeHtml(text.slice(0, 520));
  } catch (e) {
    statusEl.textContent = "Netværksfejl: " + e.message;
  }
}

function fillTable(data) {
  const tbody = document.createElement("tbody");
  wireTableDeletes(tbody);

  for (const row of data) {
    const id = row.id;
    const providers =
      row.linkedProviders && row.linkedProviders.length
        ? row.linkedProviders
            .map((p) => (typeof p === "string" ? p : p.provider || p.Provider || "—"))
            .join(", ")
        : "—";
    const mails =
      row.linkedEmails && row.linkedEmails.length
        ? row.linkedEmails
            .map((e) => mercantecMaskEmail(e.normalizedEmail) + " (" + e.kind + ")")
            .join(", ")
        : "—";
    const local = row.hasLocalLogin ? "Ja" : "Nej";
    const st = row.isDisabled
      ? '<span style="opacity:.85;color:var(--demo-muted)">Deaktiveret</span>'
      : '<span style="font-weight:600;color:var(--demo-ok)">Aktiv</span>';
    const tr = document.createElement("tr");
    tr.innerHTML =
      '<td style="text-align:center"><input type="radio" name="demo-merge-survivor" value="' +
      escapeHtml(id) +
      '" aria-label="Bevar sub · ' +
      escapeHtml(row.displayName) +
      '" /></td>' +
      '<td style="text-align:center"><input type="radio" name="demo-merge-donor" value="' +
      escapeHtml(id) +
      '" aria-label="Nedlæg · ' +
      escapeHtml(row.displayName) +
      '" /></td>' +
      "<td><strong>" +
      escapeHtml(row.displayName) +
      '</strong><br/><code style="font-size:.75rem;color:var(--demo-muted)" title="' +
      escapeHtml(id) +
      '">' +
      escapeHtml(String(id).slice(0, 8)) +
      "…</code></td>" +
      "<td class=\"demo-sensitive\">" +
      escapeHtml(mercantecMaskEmail(row.email ?? "—")) +
      "</td><td>" +
      escapeHtml(providers) +
      "</td><td class=\"demo-sensitive\">" +
      escapeHtml(mails) +
      "</td><td>" +
      local +
      "</td><td>" +
      st +
      '</td><td style="text-align:right"><button type="button" class="demo-delete-btn" data-delete-id="' +
      escapeHtml(id) +
      '" data-delete-name="' +
      escapeAttr(row.displayName) +
      '">Slet</button></td>';
    tbody.appendChild(tr);
  }

  const oldBody = table.querySelector("tbody");
  if (oldBody) oldBody.replaceWith(tbody);
  else table.appendChild(tbody);
}

async function redraw() {
  const { res, text, data } = await fetchDirectory();
  if (res.status === 403) {
    statusEl.innerHTML =
      '<span class="demo-warn">403 — dit JWT har ikke rolle Admin.</span> Konfigurer <code>Bootstrap:AdminEmail</code> eller tildel rolle.';
    mergeBar.hidden = true;
    return;
  }
  if (!res.ok || data === null || !Array.isArray(data)) {
    statusEl.textContent = "Fejl " + res.status + ": " + text.slice(0, 200);
    mergeBar.hidden = true;
    return;
  }
  statusEl.textContent = data.length + " bruger(e).";
  mergeBar.hidden = false;

  fillTable(data);
  table.hidden = false;
}

if (!token) {
  statusEl.textContent = "Log ind først og sørg for at have et access token i session.";
} else {
  (async () => {
    try {
      await redraw();
    } catch (e) {
      statusEl.textContent = "Netværksfejl: " + e.message;
    }
  })();
}
