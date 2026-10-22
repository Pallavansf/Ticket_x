# Optional OIDC integration contract — provider-neutral

## Status and current authentication

OIDC/SSO is a **future optional add-on** and is not required for local acceptance. The current local authentication method is application-managed username/password at `POST /api/v1/auth/login`. Password hashes use ASP.NET Core Identity hashing in `PA_TICKETS_USER.S_PASSWORD_HASH`; a local signing key issues a short-lived bearer JWT. RBAC and department/team/client resource authorization remain database-backed. OIDC issuer/authority settings are only needed if the optional OIDC bearer scheme is configured.

The Angular client holds the authenticated session in memory, does not persist bearer tokens in localStorage/sessionStorage, attaches tokens to API requests, and clears them after HTTP 401. Client-side guards only provide navigation UX; backend authorization remains authoritative. OIDC-specific browser registration, PKCE flow, redirect/logout URIs, provider claim mapping, and discovery key operations described below remain unimplemented and deferred.

## Optional identity-provider capabilities

If OIDC is later enabled, the approved provider must support:

- OpenID Connect discovery over HTTPS;
- Authorization Code flow with PKCE for a public browser client;
- signed ID/access tokens with keys published through discovery and key rotation;
- configurable redirect and post-logout redirect URIs;
- requested scopes and an API audience/resource claim;
- predictable expiry, logout, and reauthentication behavior;
- claim configuration or a verified mapping that supplies the backend-required subject and any authorization claims.

Provider, tenant, issuer, client ID, registration owner, and environment-specific values are **TBD**. Their absence does not block regular local authentication.

## Client registration and URI decisions

An organization must approve one public SPA registration per environment or an equivalent controlled registration strategy. The following values are **TBD** and must be recorded in deployment configuration, never source code:

- issuer/discovery authority: **TBD**;
- SPA client/application ID: **TBD**;
- development redirect URI(s): **TBD**;
- test redirect URI(s): **TBD**;
- production HTTPS redirect URI(s): **TBD**;
- post-logout redirect URI(s): **TBD**;
- allowed web origins/CORS origins: **TBD**;
- API resource/audience: **TBD**, subject to matching backend `Auth:Audience`;
- scopes: **TBD**, with `openid` required for OIDC and only approved API scopes requested;
- consent, MFA, session lifetime, and reauthentication policy: **TBD**.

Redirect and logout URI matching must be exact, HTTPS in deployed environments, and limited to owned origins. Wildcards and arbitrary return URLs are not acceptable.

## Deferred browser flow and token handoff

Use Authorization Code + PKCE through an approved OIDC client library selected during implementation approval. The SPA should acquire an access token for the API, keep it in memory, and hand it to the existing boundary through `AuthService.setBearerToken()`. The current Angular service should remain the only token consumer used by the interceptor. No token should be placed in localStorage, sessionStorage, query strings, logs, or application telemetry.

The authorization response must validate state, nonce where applicable, PKCE verifier, issuer, audience/resource, token type, and expiry in the approved client library. Silent renewal or refresh-token use is **TBD** and requires security approval; if not approved, expiry should clear the in-memory session and route to login with a safe internal return path.

Logout must clear the Angular session, cancel pending protected navigation, and perform the provider’s approved logout flow when one is configured. The post-logout URI must be one of the registered exact URIs. A 401 from the API must clear memory and require reauthentication; a 403 must preserve the session and show access denial.

## Deferred backend validation contract

These settings apply only when the optional OIDC scheme is enabled; they are not required for local username/password authentication. The backend deployment must then set:

- `Auth:Authority` to the approved HTTPS OIDC authority/discovery issuer: **TBD**;
- `Auth:Issuer` to the exact issuer returned and expected from discovery: **TBD**;
- `Auth:Audience` to the approved API resource: **TBD**;
- production CORS origins to exact owned HTTPS SPA origins: **TBD**;
- trusted proxy addresses only when forwarded headers are enabled: **TBD**.

Issuer, audience, signature, algorithm, expiry, and lifetime validation must remain enabled. Discovery signing keys must be present at startup and rotate safely. The existing positive numeric `sub` requirement must be satisfied by provider claim design or an explicitly approved identity mapping. No opaque provider subject may be silently treated as a PA Tickets user ID.

Identity-provider roles/scopes/permissions are **TBD**. They must be mapped to the backend’s existing authorization service and policies through an approved claim mapping. Angular permission presentation may consume claims for UX, but it cannot grant API access.

## Environment requirements when OIDC is enabled

- Development: local HTTPS/API origin and SPA origin are **TBD**; use non-production provider registration only; never commit secrets. This is unnecessary for the regular local username/password flow.
- Test: isolated provider tenant/client or controlled test authority is **TBD**; local-auth tests do not require one.
- Production: HTTPS authority, exact HTTPS SPA/API origins, issuer/audience, signing-key discovery, proxy configuration, monitoring, key rotation, and incident response are **TBD** and must be approved before enabling OIDC.

Configuration should be injected through deployment environment/configuration systems. Client IDs are not secrets, but issuer, audience, redirect, and scope values still require review. Client secrets must not be shipped to the SPA.

## Architecture evaluation: bearer SPA versus BFF

### Existing in-memory bearer SPA

Advantages: fits the current interceptor and API contract, avoids introducing a server session component, preserves stateless API scaling, and keeps this phase small. In-memory storage avoids persistent browser token theft and naturally loses the session on refresh. It still exposes an access token to JavaScript and requires careful XSS/CSP/dependency controls.

### BFF with secure HttpOnly cookies

Advantages: keeps provider tokens and refresh credentials out of JavaScript, can centralize login/logout/renewal and CSRF controls, and can provide a stable browser session. Costs include a new backend authentication/session architecture, cookie and CSRF design, proxying or token exchange, operational session storage, scaling behavior, and additional security review. It is not compatible with the current “no backend architecture changes” boundary without explicit approval.

### Recommendation

Retain the current in-memory bearer boundary for the first approved OIDC integration because it matches the implemented client and frozen API. Re-evaluate a BFF as a separate security architecture decision if organizational risk review requires tokens to be inaccessible to JavaScript. Neither option is implemented or provider-selected here.

## Security review and deployment acceptance criteria

Before provider-specific implementation is approved, security and operations must approve:

1. Provider, tenant, registration owner, issuer, client ID, API resource, scopes, redirect/logout URIs, and claim mappings.
2. Numeric subject mapping to a valid PA Tickets user and behavior for unknown/deactivated users.
3. PKCE/state/nonce validation, token expiry and reauthentication behavior, logout, CSRF considerations, CSP, and dependency review.
4. HTTPS certificates, CORS origins, trusted proxies, discovery key rotation, clock synchronization, audit/monitoring, and incident response.
5. Controlled tests for missing/expired/invalid-signature/wrong-issuer/wrong-audience tokens and 401/403 behavior.
6. A successful protected API smoke test and a successful create/read/complete journey using an approved non-production account.

## Implementation boundary and status

This document is a provider-neutral contract only. No identity provider, client registration, issuer, audience, scope, redirect URI, token endpoint, refresh endpoint, BFF, backend policy, database schema, or Oracle configuration was added. OIDC end-to-end authentication and live protected API verification remain **BLOCKED** until the TBD decisions are approved and exercised.
