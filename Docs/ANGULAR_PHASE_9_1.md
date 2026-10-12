# Phase 9.1 Angular foundation and login — verification closure

## 1. Angular foundation implementation

**Verified independently.** The client is a standalone strict Angular 21 application with:

- guarded login and application shell routes;
- reactive login validation, loading state, safe error rendering, password visibility, and same-origin return URL handling;
- in-memory bearer-token lifecycle with logout;
- HTTP authorization and correlation-ID interceptor with 401 session clearing;
- auth, guest, and permission guards;
- responsive shell, design tokens, and reusable status feedback panel.

No ticket, dashboard, reporting, PDF, Oracle, or backend database work was added in this phase.

## 2. Automated test verification

The repository uses npm and Angular CLI 21.2.24 with Angular 21.2.23. Angular’s installed `@angular/build` 21.2.24 declares Vitest `^4.0.8` as its compatible optional peer. The test setup now uses:

- `vitest` `^4.0.8` (resolved 4.0.8)
- `jsdom` `^27.0.0`
- `@angular/build:unit-test` with the Vitest runner

Tests cover login validation and backend integration errors, return URL rejection, auth token lifecycle and logout, interceptor bearer/correlation headers and 401 handling, and auth/guest/permission guards.

Command executed:

```text
npm test -- --no-watch --no-progress
```

Result: **4 test files passed, 10 tests passed, 0 failed, 0 skipped**.

## 3. Production build verification

Command executed:

```text
npm run build
```

Result: **passed; 0 warnings and 0 errors**.

No lint script is configured. Type checking is performed by the Angular production build; it passed. The npm install audit reported existing dependency advisories (13 total: 3 moderate, 6 high, 4 critical); no audit remediation or unrelated package upgrades were performed.

## 4. OIDC integration readiness

**Blocked pending approved identity-provider configuration.** The backend validates externally issued bearer/OIDC tokens but does not issue login tokens and has no `/login`, `/token`, or `/refresh` endpoint. The client preserves that boundary and does not add credentials, a fake endpoint, a hardcoded token, a test bypass, or token persistence in browser storage.

The remaining integration contract requires an approved identity provider, application/client registration, exact redirect URI(s), scopes and API audience, token acquisition and handoff into `AuthService.setBearerToken()`, logout behavior, and matching backend issuer/audience/signing-key validation. Those decisions and values are intentionally not selected in this phase.

## Final status

- Angular foundation: **VERIFIED**
- Automated tests: **VERIFIED (10/10)**
- Production build: **VERIFIED**
- End-to-end OIDC authentication: **BLOCKED by unavailable approved provider configuration and backend token issuer boundary**

Therefore the overall Phase 9.1 status remains **NOT VERIFIED** until the external OIDC integration contract is supplied and exercised.
