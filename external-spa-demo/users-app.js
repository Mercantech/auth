const c = window.__MERCANTEC_AUTH__;
const token = sessionStorage.getItem("mercantec_access_token");
const status = document.getElementById("status");
const table = document.getElementById("users-table");

if (!token) {
  status.textContent = "Log ind først og sørg for at have et access token i session.";
} else {
  (async () => {
    try {
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
      if (res.status === 403) {
        status.innerHTML =
          '<span class="demo-warn">403 — dit JWT har ikke rolle Admin.</span> Sæt <code>Bootstrap:AdminEmail</code> til din e-mail og opret bruger igen, eller tildel Admin i databasen.';
        return;
      }
      if (!res.ok) {
        status.textContent = "Fejl " + res.status + ": " + text.slice(0, 200);
        return;
      }
      status.textContent = data.length + " bruger(e).";
      const tbody = table.querySelector("tbody");
      tbody.innerHTML = "";
      for (const row of data) {
        const providers = row.linkedProviders && row.linkedProviders.length ? row.linkedProviders.join(", ") : "—";
        const mails =
          row.linkedEmails && row.linkedEmails.length
            ? row.linkedEmails.map((e) => e.normalizedEmail + " (" + e.kind + ")").join(", ")
            : "—";
        const local = row.hasLocalLogin ? "Ja" : "Nej";
        const tr = document.createElement("tr");
        tr.innerHTML =
          "<td>" +
          escapeHtml(row.displayName) +
          "</td><td>" +
          escapeHtml(row.email ?? "—") +
          "</td><td>" +
          escapeHtml(providers) +
          "</td><td>" +
          escapeHtml(mails) +
          "</td><td>" +
          local +
          "</td>";
        tbody.appendChild(tr);
      }
      table.hidden = false;
    } catch (e) {
      status.textContent = "Netværksfejl: " + e.message;
    }
  })();
}

function escapeHtml(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}
