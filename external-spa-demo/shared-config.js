/**
 * Fælles config — ret authBaseUrl hvis API kører et andet sted.
 * issuer/audience skal matche Jwt i appsettings (validering af signatur).
 */
window.__MERCANTEC_AUTH__ = {
  authBaseUrl: "https://auth.mercantec.tech",
  clientId: "demo",
  expectedIssuer: "https://auth.mercantec.tech",
  expectedAudience: "mercantec-apps",
};
