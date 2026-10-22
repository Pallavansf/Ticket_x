# PA_TICKETS RBAC Migration Gate Resolution

**Branch:** `workstream-i-lifecycle-authority`  
**Target evidence:** `pa_tickets_codex_e2e` only  
**Purpose:** reconcile the migration-030 blocker without inventing authorization data

## Exact 34-code requirement and provenance

`database/migrations/postgresql/030_five_role_rbac.sql` requires exactly 34
active permission rows whose `S_PERMISSION_CODE` values are:

```text
APPROVAL_ADMIN, APPROVAL_PUBLISH, APPROVAL_READ, AUDIT_EXPORT, AUDITOR_VIEW,
CATALOGUE_PUBLISH, CATEGORY_ADMIN, CATEGORY_READ, CLIENT_ADMIN,
COMMUNICATION_ADMIN, COMMUNICATION_PUBLISH, COMMUNICATION_READ,
COMMUNICATION_RETIRE, CONFIG_ADMIN, NOTIFICATION_ADMIN, NOTIFICATION_PUBLISH,
NOTIFICATION_READ, NOTIFICATION_RETIRE, REQUEST_TYPE_ADMIN, REQUEST_TYPE_READ,
ROLE_ADMIN, SERVICE_ADMIN, SERVICE_READ, SLA_ADMIN, SLA_PUBLISH, SLA_READ,
SLA_RETIRE, TICKET_ADMIN, TICKET_READ, TICKET_UPDATE, USER_ADMIN,
WORKFLOW_ADMIN, WORKFLOW_PUBLISH, WORKFLOW_READ
```

The same list appears in the reviewed-role table in
`Docs/PA_TICKETS_FIVE_ROLE_RBAC_REVIEW.md` and is described in
`Docs/PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md` as the **current proposal's**
34-code Super Admin allowlist. Those documents explicitly say the fixed-
allowlist migration is superseded and must not be approved or executed.
The configurable design also says its larger action catalogue is a proposal
requiring controller/service parity review and a revised migration. Therefore
the 34 strings are traceable documentation, but not an approved seed artifact
or an authoritative executable permission catalogue.

## Conflicts with current source

The current source contains permission identifiers outside the 34-code list:

| Code | Source evidence | Consequence |
|---|---|---|
| `ROLE_PERMISSION_ADMIN` | `src/PA.Tickets.Infrastructure/Foundations/AdministrationService.cs` role-permission operations | The fixed list cannot authorize current permission-mapping administration. |
| `ROLE_SCOPE_ADMIN` | `AdministrationService.cs` role-scope operations | The fixed list cannot authorize current scope administration. |
| `AUDIT_ACTIVITY` | `src/PA.Tickets.Infrastructure/Reporting/AuditorReportingService.cs` report definitions | Report permission values are not equivalent to report codes; a catalogue parity decision is still required. |

The configurable design proposes many additional operation-level codes (for
example `TICKET_CREATE`, `TICKET_TRANSITION`, `ROLE_PERMISSION_MANAGE`, and
workflow/approval instance actions) and explicitly distinguishes them from
legacy aliases. This is a second, incompatible catalogue unless an owner
approves the mapping and migration revision.

## Target catalog evidence

On the approved target, read-only catalog inspection after foundational DDL and
migrations 028–029 showed 71 public objects, `PA_TICKETS_ROLE` and
`PA_TICKETS_PERMISSION` tables, no migration ledger/delegation/proposal tables,
and zero permission rows. Migration 030's transaction rolled back at its
explicit reviewed-catalog check. No role, permission, grant, or historical row
was fabricated.

## Safe executable sequence

The only source-supported sequence currently evidenced is:

1. Foundational PostgreSQL DDL (already committed on the approved target).
2. PostgreSQL migrations 028 and 029 (already committed).
3. A **new owner-approved replacement** for superseded 030 that defines the
   authoritative permission catalogue, canonical role/permission identities,
   initial grants, and compatibility with current call sites.
4. The approved dependent migrations in their documented order: 031, 032,
   033, 034, 035, 036, and 037, subject to each script's preflight and owner
   gate. Migration 038 remains blocked by the unresolved audit-actor contract.
5. Migration 026 remains a separate reporting authorization chain and is not
   part of this sequence.

No existing file authorizes treating 030 as that replacement. Executing 031–037
before the replacement and its owner approvals would create a schema without a
settled permission/role contract and would violate the scripts' own review
gates. The application/API and Angular E2E stages therefore remain blocked.

## Minimum decisions required

- Security/RBAC owner: authoritative permission catalogue, canonical aliases,
  operation-level mappings, built-in role identities, and initial grants.
- Authorization/data owner: effective-date and unknown-history treatment for
  memberships, grants, client/scope, and delegation.
- Release/database owner: approved replacement migration and ordering after
  already-applied 028–029, plus migration-ledger representation without
  fabricated history.
- Audit/compliance owner: actor-type/nullability/FK and chain-compatibility
  contract required before migration 038 can be reconsidered.
- Separate migration authority: migration 026 target/hash/lock/recovery and
  execution approval.

Until these decisions are recorded as actual owner approvals, no further
schema migration or authorization seed is safe. The repository's source-only
build and provider-independent tests remain valid evidence but do not establish
PostgreSQL RBAC or end-to-end readiness.
