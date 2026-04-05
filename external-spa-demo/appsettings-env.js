/**
 * Flattener appsettings-lignende JSON til miljøvariabler som ASP.NET Core forstår
 * (nested keys med dobbelt underscore, fx Jwt__Issuer).
 */
function flattenAppsettingsToEnvPairs(obj, prefix) {
  const pairs = [];
  if (obj === null || typeof obj !== "object" || Array.isArray(obj)) {
    return pairs;
  }
  for (const [k, v] of Object.entries(obj)) {
    const segment = String(k).trim();
    if (!segment) continue;
    const path = prefix ? prefix + "__" + segment : segment;
    if (v === null || v === undefined) {
      pairs.push([path, ""]);
    } else if (typeof v === "object" && !Array.isArray(v)) {
      pairs.push(...flattenAppsettingsToEnvPairs(v, path));
    } else if (Array.isArray(v)) {
      v.forEach((item, i) => {
        const p = path + "__" + i;
        if (item === null || item === undefined) {
          pairs.push([p, ""]);
        } else if (typeof item === "object") {
          pairs.push(...flattenAppsettingsToEnvPairs(item, p));
        } else {
          pairs.push([p, stringifyPrimitive(item)]);
        }
      });
    } else {
      pairs.push([path, stringifyPrimitive(v)]);
    }
  }
  return pairs;
}

function stringifyPrimitive(v) {
  if (typeof v === "boolean" || typeof v === "number") return String(v);
  return String(v);
}

/** Sikker .env-værdi (citat ved behov). */
function escapeEnvValue(raw) {
  const s = raw;
  if (s === "") return "";
  const needsQuote = /[\s#"'\n\r\\$`]/.test(s) || s.includes("=");
  if (!needsQuote) return s;
  return (
    '"' +
    s
      .replace(/\\/g, "\\\\")
      .replace(/"/g, '\\"')
      .replace(/\n/g, "\\n")
      .replace(/\r/g, "\\r") +
    '"'
  );
}

function pairsToEnvFile(pairs) {
  const lines = [
    "# Genereret fra appsettings.json — tjek før commit; ingen hemmeligheder i git.",
    "# ASP.NET Core: nested nøgler som Section__SubKey (dobbelt underscore).",
    "",
  ];
  for (const [k, v] of pairs) {
    lines.push(k + "=" + escapeEnvValue(v));
  }
  return lines.join("\n") + "\n";
}

function convertAppsettingsJsonToEnv(jsonText) {
  const trimmed = jsonText.trim();
  if (!trimmed) return { ok: true, env: "", pairs: [], warning: "Tom indgang — intet at konvertere." };
  let data;
  try {
    data = JSON.parse(trimmed);
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : String(e) };
  }
  if (typeof data !== "object" || data === null || Array.isArray(data)) {
    return { ok: false, error: "Rod skal være et JSON-objekt { ... }, ikke et array." };
  }
  const pairs = flattenAppsettingsToEnvPairs(data, "");
  pairs.sort((a, b) => a[0].localeCompare(b[0]));
  return { ok: true, env: pairsToEnvFile(pairs), pairs, warning: null };
}
