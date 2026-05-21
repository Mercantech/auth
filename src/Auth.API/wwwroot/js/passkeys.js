window.MercantecPasskeys = {
  async register(optionsUrl, completeUrl, antiforgeryToken, friendlyName) {
    const optRes = await fetch(optionsUrl, {
      method: "POST",
      headers: { RequestVerificationToken: antiforgeryToken },
    });
    if (!optRes.ok) throw new Error("Kunne ikke hente passkey-options");
    const options = await optRes.json();
    options.challenge = base64UrlToBuffer(options.challenge);
    options.user.id = base64UrlToBuffer(options.user.id);
    if (options.excludeCredentials) {
      options.excludeCredentials = options.excludeCredentials.map((c) => ({
        ...c,
        id: base64UrlToBuffer(c.id),
      }));
    }
    const cred = await navigator.credentials.create({ publicKey: options });
    const attestation = credentialToJson(cred);
    const completeRes = await fetch(completeUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiforgeryToken,
      },
      body: JSON.stringify({ attestation, friendlyName }),
    });
    if (!completeRes.ok) throw new Error("Passkey-registrering fejlede");
  },

  async assertForMfa(optionsUrl, completeUrl, antiforgeryToken, returnUrl) {
    const optRes = await fetch(optionsUrl, {
      method: "POST",
      headers: { RequestVerificationToken: antiforgeryToken },
    });
    if (!optRes.ok) throw new Error("Kunne ikke hente passkey-options");
    const options = await optRes.json();
    prepareAssertionOptions(options);
    const cred = await navigator.credentials.get({ publicKey: options });
    const assertion = credentialToJson(cred);
    const completeRes = await fetch(completeUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiforgeryToken,
      },
      body: JSON.stringify({ assertion, returnUrl }),
      credentials: "same-origin",
    });
    if (!completeRes.ok) throw new Error("Passkey-bekræftelse fejlede");
    const body = await completeRes.json();
    if (body.redirect) {
      window.location.assign(body.redirect);
      return;
    }
    window.location.reload();
  },

  async loginPasswordless(optionsUrl, completeUrl, antiforgeryToken, returnUrl) {
    const optRes = await fetch(optionsUrl, {
      method: "POST",
      headers: { RequestVerificationToken: antiforgeryToken },
    });
    if (!optRes.ok) throw new Error("Kunne ikke starte passkey-login");
    const options = await optRes.json();
    prepareAssertionOptions(options);
    const cred = await navigator.credentials.get({ publicKey: options });
    const assertion = credentialToJson(cred);
    const res = await fetch(completeUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiforgeryToken,
      },
      body: JSON.stringify({ assertion, returnUrl }),
      redirect: "follow",
    });
    if (res.redirected) {
      window.location = res.url;
      return;
    }
    throw new Error("Passkey-login fejlede");
  },
};

function prepareAssertionOptions(options) {
  options.challenge = base64UrlToBuffer(options.challenge);
  if (options.allowCredentials) {
    options.allowCredentials = options.allowCredentials.map((c) => ({
      ...c,
      id: base64UrlToBuffer(c.id),
    }));
  }
}

function base64UrlToBuffer(base64url) {
  const pad = "=".repeat((4 - (base64url.length % 4)) % 4);
  const base64 = (base64url + pad).replace(/-/g, "+").replace(/_/g, "/");
  const raw = atob(base64);
  const buffer = new ArrayBuffer(raw.length);
  const view = new Uint8Array(buffer);
  for (let i = 0; i < raw.length; i++) view[i] = raw.charCodeAt(i);
  return buffer;
}

function bufferToBase64Url(buffer) {
  const bytes = new Uint8Array(buffer);
  let str = "";
  for (const b of bytes) str += String.fromCharCode(b);
  return btoa(str).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function credentialToJson(cred) {
  if (!cred) throw new Error("Ingen credential");
  const response = cred.response;
  const json = {
    id: cred.id,
    rawId: bufferToBase64Url(cred.rawId),
    type: cred.type,
    response: {
      clientDataJSON: bufferToBase64Url(response.clientDataJSON),
    },
    clientExtensionResults: cred.getClientExtensionResults?.() ?? {},
  };
  if (response.attestationObject) {
    json.response.attestationObject = bufferToBase64Url(response.attestationObject);
  }
  if (response.authenticatorData) {
    json.response.authenticatorData = bufferToBase64Url(response.authenticatorData);
  }
  if (response.signature) {
    json.response.signature = bufferToBase64Url(response.signature);
  }
  if (response.userHandle) {
    json.response.userHandle = bufferToBase64Url(response.userHandle);
  }
  return json;
}
