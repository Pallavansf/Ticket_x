# Phase 2 status

## Database hardening: PARTIAL
Additive migration `002_hardening.sql` creates per-table sequences, lifecycle projection columns, parallel workflow step instances/history, and a unique current-attachment index. `003_system_actor.sql` seeds the technical actor. Scripts are not Oracle-executed in this environment.

## Oracle persistence: PARTIAL
`Oracle.EntityFrameworkCore` 8.23.40 is configured for Oracle 19c compatibility. EF mappings, sequence identity generation, and transactional repository code compile. No Oracle 19c target/credentials are available.

## Ticket vertical slice: PARTIAL
Create, classify, route, approve, execute, evidence, work completion, resolve, and close are implemented through domain/application/API layers. End-to-end database execution is pending Oracle validation.

## Authorization/SOD: PARTIAL
JWT authentication is the API boundary; actor mapping and domain requester/creator/executor approval restrictions are implemented. Identity-provider and scope-policy integration remain pending.

## Audit/Event: PARTIAL
Lifecycle events and hash-chained audit rows are written by the repository in the same transaction. Oracle execution and integrity verification tests remain pending.

## Tests and builds
- .NET build: PASS, 0 warnings/errors.
- Domain tests: PASS, 4/4.
- Angular build: PASS.
- API host/health smoke check: PASS (`/health`, `/api/v1/system/status`).
- Oracle validation: NOT AVAILABLE.

## Next recommended phase
Connect a non-production Oracle 19c schema, run baseline + migrations + preflight, then add Oracle-backed integration tests before expanding UI/catalogue/reporting.
