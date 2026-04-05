/**
 * Fælles config — ret authBaseUrl hvis API kører et andet sted.
 * issuer/audience skal matche Jwt i appsettings (validering af signatur).
 */
window.__MERCANTEC_AUTH__ = {
  authBaseUrl: "http://localhost:8080",
  clientId: "demo",
  expectedIssuer: "https://auth.mercantec.tech",
  expectedAudience: "mercantec-apps",
};
