# API versioning contract

## Current contract

Business endpoints are exposed under `/api/v1/...`. Existing controller routes were audited and already use this prefix; the API now also publishes explicit `1.0` metadata through `Asp.Versioning.Mvc`.

Swagger exposes the `v1` document at `/swagger/v1/swagger.json` and lists the `/api/v1/...` paths. `/api/v2/...` is not mapped and returns 404. Unversioned business paths such as `/api/tickets` are not aliases and return 404.

## Compatibility decisions

- Regular username/password login is exposed as `POST /api/v1/auth/login`; it returns a short-lived bearer token. Password setting/reset is `POST /api/v1/admin/users/{id}/password` and requires `USER_ADMIN`. Optional OIDC bearer validation remains supported as an add-on; it is not required for local acceptance.
- No public signed-file endpoint exists. Attachment and evidence routes remain authenticated `/api/v1` business routes.
- `/health`, `/health/live`, and `/health/ready` remain infrastructure endpoints and are intentionally outside API versioning.
- The Angular ticket client already calls `/api/v1/tickets`; no client route migration was required.
- No DTO, domain, persistence, migration, or Oracle changes are part of this foundation. A future v2 must be added deliberately with separate version metadata and compatibility review.
