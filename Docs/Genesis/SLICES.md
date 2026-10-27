# PA_TICKETS Slice Plan (SLICES) — v4

**Purpose:** turn `FRD.md` v0.7 into small, independently verifiable slices that Codex can implement and validate, starting from what the repository already contains.  
**Status authority:** `Docs/PA_TICKETS_MASTER_ROADMAP.md` stays authoritative for stage status. This file defines work units and acceptance checks and claims no completion.  
**Prepared from:** file-level inspection of `Pallavansf/Ticket_x`, branch `workstream-i-lifecycle-authority`, commit `f080a33`, available owner-stated direction and working defaults in FRD §10.1. No build, test or database operation was run to prepare this plan. Codex must re-verify every repository finding before relying on it (SOP §2, §3).

---

## 0. How Codex uses this file

1. Read in order: `Docs/Genesis/readme.md` (or the repository's actual Genesis entry-point path), `Docs/Genesis/SOP.md`, `Docs/Genesis/FRD.md` (v0.7), this file, `Docs/PA_TICKETS_MASTER_ROADMAP.md`, `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md`, `Docs/reconciliation/PA_TICKETS_OWNER_DECISION_PACK.md`, then the source and tests for the chosen slice. Verify paths; do not assume a file exists merely because it is named here.
2. Pick **one slice**: the first in §4 "Recommended order" whose dependencies are met. Name its FR IDs, dependencies and acceptance criteria before editing (SOP §2).
3. Inspect existing code first. Most slices **extend** existing code (§1) and must not duplicate it.
4. Make the smallest useful change. Run focused tests first, then the relevant broader suite. Record exact commands and results.
5. Update the ledger (§9), then the roadmap and traceability, only from evidence.
6. Never pick a BLOCKED slice. Never repeat a documentation-only slice without new evidence (SOP §4).
7. **Respect decision authority.** FRD §10.1 distinguishes owner-stated direction from working defaults. Cross-check consequential decisions against the canonical owner decision pack. Implement explicit owner decisions as written. Use defaults only for safe, reversible work where appropriate; do not present them as owner approval. Formal operational authorization remains mandatory for gated database/provider operations.

**Slice types**

| Type | Meaning | Database access |
|---|---|---|
| `SRC` | Source change plus database-independent tests | None |
| `UI` | Angular source plus Vitest specs | None |
| `SCH` | PostgreSQL schema artifact written as a **review draft**; never executed | None |
| `DOC` | Governance or decision record, only when it carries new evidence | None |
| `PRV` | Provider-backed tests or catalog/schema work | Only after exact-target authorization |
| `E2E` | Browser → API → database acceptance | Only after exact-target authorization |

---

## 1. Repository findings (file-level, to be re-verified)

| Area | What the repository contains | Consequence |
|---|---|---|
| Solution | `src/PA.Tickets.{Api,Application,Domain,Infrastructure}` (.NET 8), `tests/PA.Tickets.Domain.Tests`, `client/` (Angular `^21`, Vitest), `tools/` | Keep Clean Architecture boundaries |
| Database | PostgreSQL is the only database (DD-06). The Oracle chain (`database/schema/001`, `database/migrations/002…032`) and the Oracle EF branch in `DependencyInjection.cs` still exist | **Do not touch, extend, test or delete Oracle files.** New schema work is PostgreSQL only |
| PostgreSQL sources | `tests/scripts/postgresql-foundational-ddl.sql` and overlapping `postgresql-*schema.sql` adapters; `database/migrations/postgresql/028...039`; 038 is an intentional gate; 039 is review-only | Root 014-016 use PostgreSQL syntax but lack numbered counterparts in the subdirectory; root 027 uses Oracle syntax and its mapped columns already exist in the baseline draft. Current review order remains baseline -> 028, 029 -> 033 -> 034 -> 035 -> 036 -> 037; 038 and 026 are excluded. 039 is a source-only ticket-type draft, not authorized for execution. |
| Mapped missing PostgreSQL DDL | Source inspection found Incident, Problem, Change, ChangeImplementation, and Relation definitions absent from the baseline build artifact and numbered PostgreSQL migrations; the test adapter contains definitions | S5-09 records the mapping inventory and guarded migration 039 review draft; no live catalog claim is made |
| E2E target | `pa_tickets_codex_e2e` / `pa_tickets_codex` was created by the owner (DD-07). Recorded: identity verified; 43 tables with unknown provenance; RBAC tables empty; **1 repository persistence test passed**; the lifecycle persistence test **failed closed** because `DatabaseAuthorizationFactsReader` queries `p.S_MODULE_CODE`, which migration 034 adds and which is not applied (SQLSTATE `42703`) | The next M1 unblock is a **schema build** under authorization, not a code fix. Slices 63/70 recorded NO-GO for formal approval fields; S5-01 closes that gap with the owner |
| Roles | Codes `USER`, `ADMIN`, `ADMIN_HEAD`, `SUPER_ADMIN`, `SPECTATOR` (`RoleAuthorizationPolicy.cs`; configurable RBAC in migrations 031–037; `CONFIGURABLE_RBAC_DESIGN.md`). Manager = `ADMIN`, Manager Admin = `ADMIN_HEAD` (DD-01) | No code rename. Add display labels and new permissions |
| Hierarchy | Team membership, `PA_TICKETS_USER_ROLE_SCOPE` (migration 035), Admin Head delegation (035), `N_MANAGER_USER_ID` (029, informational only) | Hierarchy comes from explicit effective-dated relations (DD-02) |
| Lifecycle | Transition matrix includes `WAITING` and `PENDING_APPROVAL`; migration 004; waiting-reason table | FR-026 builds on `WAITING` |
| Approvals | Migrations 011, 019, 032; `ApprovalsController`, `ApprovalRuleAdministrationController` | FR-025 extends them |
| Notifications | Baseline `PA_TICKETS_NOTIFICATION`, `PA_TICKETS_COMMUNICATION`; migrations 013, 021; `NotificationOrchestrator.cs`; ADR-011 (no provider adapters registered) | No inbox API or bell found. Extend the notification tables before adding new ones |
| Real-time / chat | No SignalR, no chat found | New |
| Announcements | Baseline `PA_TICKETS_ANNOUNCEMENT` and `_TARGET`; no controller | API and UI needed |
| Catalogue / workflow / SLA admin | Migrations 017–020; administration controllers; calendar, holiday and SLA-pause tables | FR-033/034/036 are `PARTIAL` |
| Identifiers | `S_TICKET_NO` = `{REQ\|INC\|PRB\|TSK\|CHG}-{000000}`; 16-character public ID | Kept (DD-04) |
| Audit / reporting | Hash-chained audit (migration 012), audit-package and security-event tables; KPI, dashboards, exports | FR-040 and FR-022/030 extend them |
| Policy routing | `WorkflowRouting:Rules` / `Triage` configuration; SLA escalation administration (020); an ADR forbids a second database-backed routing engine | DD-05: one engine, new ADR-020 |
| Acceptance checklist | `PA_TICKETS_API_E2E_ACCEPTANCE_MATRIX.md` records 8 PASS / 3 PARTIAL / 13 BLOCKED | M1 closes the blocked rows |
| Governance docs | `Docs/Genesis/*` are the v0.1 set. SOP §7.3 and the roadmap say Oracle is deferred, which now matches DD-06 | S0-03 is small |

---

## 2. Database style guide (binding; PostgreSQL only)

Keep the existing `PA_TICKETS` style, taken from `tests/scripts/postgresql-foundational-ddl.sql` and migrations 029, 035 and 037.

| Element | Rule |
|---|---|
| Location | New schema goes only in `database/migrations/postgresql/NNN_<name>.sql`, numbered above the highest existing number (038 today — verify). Never edit `tests/scripts/postgresql-*.sql`, root migrations, or an approved migration. Never use or modify 026 |
| Header | `-- PA_TICKETS <area> migration NNN (REVIEW DRAFT; do not execute without approval).` followed by purpose and prerequisites |
| Wrapper | `BEGIN; … COMMIT;`, `SET LOCAL search_path TO public, pg_catalog;`, and a guarded `DO $$ … RAISE EXCEPTION … $$` block that fails when a prerequisite relation is absent. Mutating drafts require the explicit approval setting used by migration 037 |
| Tables | `"PA_TICKETS_<NAME>"` (quoted upper case), `create table if not exists` |
| Key columns | `"N_<ENTITY>_ID" bigint primary key default nextval('public."PA_TICKETS_<NAME>_SEQ"'::regclass)`, one sequence per table (`create sequence if not exists "PA_TICKETS_<NAME>_SEQ"`); never `MAX(id)+1` (ADR-007) |
| Strings / numbers / dates | `"S_…" varchar(n)` or `text`; `"N_…" bigint` or `integer`; `"D_…" timestamptz` |
| Flags | `"N_ACTIVE"` / `"N_ENABLED"` as `boolean not null default true`, matching migrations 013, 029 and 035 |
| Audit columns | `"D_CREATED_AT" timestamptz not null default current_timestamp`, `"N_CREATED_BY" bigint not null`, `"D_UPDATED_AT"`, `"N_UPDATED_BY"` |
| Concurrency | `"N_ROW_VERSION" integer not null default 0` where the entity is updated concurrently |
| Constraints | `PK_PA_TICKETS_<ABBR>`, `FK_PA_TICKETS_<ABBR>_<REF>`, `UK_…`, `CK_…`, `IX_…` — abbreviate as the baseline does |
| Change style | Additive only: `create … if not exists`, `alter table … add column if not exists` with nullable or defaulted columns; no destructive change; no data inserts except reviewed seed rows in a separate, clearly marked block |
| Time | Store `timestamptz`; IST is the business zone for calendars and display only |
| Tenancy | None |
| History | The application never physically deletes audit, assignment, message, notification or approval history (DD-16). Use state columns (`N_ACTIVE`, `D_ARCHIVED_AT`, `D_DELETED_AT`) plus audit |
| Inventories | When objects are added, update `TicketsDbContext` / `ExecutionDbContext` mappings, `DatabaseProviderCapabilities.cs` (sequences), the deployment preflight script, and the provider-neutral tests where the pattern exists |
| Reuse first | Inspect before creating: `PA_TICKETS_NOTIFICATION`, `_ANNOUNCEMENT`, `_COMMENT`, `_ATTACHMENT`, `_WAITING_REASON`, `_USER_ROLE_SCOPE`, the delegation relation, approval tables, `_NOTIFICATION_RULE`, SLA escalation tables, effective-dated membership history (023) |

**Candidate objects** (confirm by inspection; not approved):

| Feature | Candidates |
|---|---|
| Assignment rules (FR-021) | `PA_TICKETS_ASSIGNMENT_RULE` (+ `_VERSION`): `S_RULE_CODE`, `S_ASSIGNER_ROLE_CODE`, `S_TARGET_RELATION`, `S_MODE`, `N_NOTIFY_TARGET_MANAGER`, `N_REQUIRE_ACCEPTANCE`, `N_ENABLED`, `D_VALID_FROM`, `D_VALID_TO`, `N_ROW_VERSION`; extend assignment history with nullable `S_ASSIGN_MODE` and `N_VIA_MANAGER_USER_ID` |
| Message Center | Extend `PA_TICKETS_NOTIFICATION` (nullable `D_READ_AT`, `D_ARCHIVED_AT`, `S_CATEGORY_CODE`, `S_SEVERITY`, `S_DEDUP_KEY`, `S_DEEP_LINK`); new `PA_TICKETS_NOTIFICATION_PREFERENCE` |
| Chat | `PA_TICKETS_CHAT_CONVERSATION`, `_CHAT_PARTICIPANT`, `_CHAT_MESSAGE`, `_CHAT_MESSAGE_VERSION`; attachments through the existing attachment tables |
| Feature switches | `PA_TICKETS_FEATURE_SETTING` (`S_FEATURE_CODE`, `S_SCOPE_TYPE`, `N_SCOPE_ID`, `N_ENABLED`, effective dates), unless migration 022 operational metadata fits |
| Policy engine | `PA_TICKETS_POLICY`, `_POLICY_VERSION`, `_POLICY_EXECUTION`, following the `_NOTIFICATION_RULE` pattern (013) |
| Waiting | Nullable `N_PAUSES_SLA` on the waiting-reason table if absent |
| Real-time | Reuse the durable job tables (`PA_TICKETS_BACKGROUND_JOB`) instead of an outbox table |

---

## 3. Role model (decided — implemented by S3-02, S3-06, S3-07)

| Code | Label | Fixed or configurable | Notes |
|---|---|---|---|
| `USER` | User | Fixed | Create and respond; own and assigned work |
| `ADMIN` | Manager | Fixed | Head of a team; existing department-scoped capabilities plus team assignment, delegation per rules, team monitoring, approver |
| `ADMIN_HEAD` | Manager Admin | Configurable | Head of Managers across multiple teams; assigns or delegates to Managers in the delegated group; sees grouped teams |
| `SUPER_ADMIN` | Super Admin | Fixed | Full control through the reviewed all-permission catalogue; fully audited; separation-of-duties still applies |
| `SPECTATOR` | Spectator | Configurable (visibility only) | Read-only everywhere; deny precedence on every mutation |

- **Fixed** means identity, core capabilities and protections are locked; additive grants are allowed inside the ceilings. **Configurable** means grants and visibility can be edited inside the ceilings. Spectator's read-only invariant is never configurable.
- **Relations:** team membership, Manager scope, and Manager Admin → Manager delegation, all explicit, effective-dated and audited. The reporting-manager field never confers authority.
- **Assignment authority** is configurable data (FR-021, rules R1 to R4), not hard-coded by role name.
- `ADMIN` and `ADMIN_HEAD` are mutually exclusive for the same period; `SUPER_ADMIN` is exclusive of other memberships.
- New permission codes fail closed until the reviewed seed migration and the allowlist include them. Candidate permission codes (align names with the module/action catalogue of migration 034): `ASSIGNMENT_RULE_ADMIN`, `TICKET_ASSIGN_TEAM`, `TICKET_ASSIGN_GROUP`, `TICKET_DELEGATE`, `PERFORMANCE_TEAM_VIEW`, `PERFORMANCE_GROUP_VIEW`, `CHAT_AUDIT_VIEW`, `FEATURE_ADMIN`, `POLICY_ADMIN`.

---

## 4. Milestones and execution priorities

| Milestone | Slices | Exit evidence |
|---|---|---|
| **M1 — Persistence foundation and core acceptance (current)** | S5-08, S5-09, S5-01, S5-03, S5-10, S2-04, S3-05, S2-05, S6-01 to S6-04 | PostgreSQL schema built on the authorized E2E target; lifecycle persistence and API route/role tests recorded; acceptance matrix rows closed or accepted by the owner |
| **M2 — Collaboration core** | S3-02, S3-06, S3-07, S3-09, S3-10, S2-06, S4-14, S4-06, S4-07, S4-08, S4-09, S4-12, S4-13, S6-05, S6-06 | Source, Angular, then provider and browser evidence |
| **M3 — Productivity and automation** | S4-10, S4-11, S4-15, S4-16, S4-17, S4-18, S4-19, S4-20 | As above |
| **M4 — Release and assurance** | S7-01 to S7-04; post-V1: S3-08 | Signed release checklist |

Source and Angular slices in M2/M3 can run **in parallel** with M1 because they need no database. Provider acceptance always waits for the M1 gates.

**Recommended order (take the first executable slice whose dependencies and safety gates are met).** Governance alignment is not a blanket prerequisite for safe implementation. Do not let a blocked database slice block independent source/UI work. Work on one slice at a time unless separate agents/worktrees and non-overlapping files are explicitly coordinated.

| # | Slice | Type | Why now |
|---|---|---|---|
| 1 | S5-08 Split baseline adapters into DDL-only and data stages | SCH | Safe review-draft work; preserve originals |
| 2 | S5-09 PostgreSQL counterparts for missing root migrations | SCH | Safe review-draft work; nothing executed |
| 3 | S4-09 Message Center Angular | UI | Independent UI slice; inspect existing app and contracts first |
| 4 | S4-11 Chat Angular | UI | Independent UI slice; use existing contracts or clearly mark missing API behavior |
| 5 | S2-06 Waiting with reason | SRC | Safe source/tests if contracts and existing implementation support it |
| 6 | S2-07 Identifier lookup and validation | SRC | Preserve existing formats |
| 7 | S3-02 Role labels, permissions and fixed/configurable classes | SRC + SCH | Separate source work from review-only seed/schema draft |
| 8 | S3-07 Hierarchy relations and assignment rules | SRC + SCH | Respect owner-decision dependencies; drafts only |
| 9 | S4-06 Feature switches | SRC + SCH + UI | Message Center and Chat backend dependencies |
| 10 | S4-07 Real-time hub, S4-08 Message Center backend, S3-10 Manager notification | SRC | After contracts and dependencies are verified |
| 11 | S4-14 Approvals, S4-12 Channels, S4-13 Announcements, S3-09 Rules admin UI | SRC / UI | After relevant dependencies |
| 12 | S0-03 / S1-01 / S1-04 governance and traceability updates | DOC | Only necessary edits, tied to new evidence; not a reason to delay safe coding |
| 13 | S5-01 → S5-03 → S5-10 | DOC / PRV | Requires complete operation-specific authorization; remains gated |
| 14 | M3 slices, then Stage 6 and 7 | — | Follow dependencies and evidence gates |

---
## 5. Slices by stage

Each slice lists **FRs · Type · Dependencies**, then objective, **reuse first**, acceptance, tests, status and the next executable action. Global restrictions (§8) apply to every slice. Use the test commands recorded in `tests/TESTING.md` (for example `npm.cmd test` from `client`).

### Stage 0 — Baseline and evidence integrity

#### S0-01 — Repository baseline
- **FRs:** NFR-002, NFR-007 · `DOC`
- **Status:** `PASS` for the documentation baseline only (roadmap). Re-check `git status` and `git diff` before each slice and preserve all pre-existing changes.

#### S0-02 — Evidence index and artifact provenance
- **FRs:** FR-016, NFR-002, NFR-007, NFR-010 · `DOC`
- **Acceptance:** each evidence item has a path and a stated scope limit; no migration history is inferred from object shape.
- **Status:** `PARTIAL`. Do not build an index without new evidence.

#### S0-03 — Align governance documents with FRD v0.7
- **FRs:** NFR-005, FR-023 · `DOC` · **Dependencies:** none for drafting; owner approval required before changing a canonical decision or policy.
- **Objective:** make only necessary consistency edits; do not make governance alignment a prerequisite for safe source/UI work:
  1. `Docs/Genesis/FRD.md` ← FRD v0.7 and `Docs/Genesis/SLICES.md` ← this file.
  2. Roadmap role wording: add "Manager = `ADMIN`, Manager Admin = `ADMIN_HEAD` (display labels)" and a pointer to FRD §4.
  3. SOP/readme: add a pointer to the database style guide (§2 of this file) and to the decision register (FRD §10.1).
  4. Record the E2E target `pa_tickets_codex_e2e` as owner-created (DD-07), identification only.
  5. The Oracle statements already match DD-06; do not change them.
- **Acceptance:** contradictions are removed; no owner decision or safety restriction is weakened; changes to canonical policy/decision records receive owner approval.
- **Status:** `NOT STARTED` in the source plan; these uploaded review drafts do not update the user's local repository.
- **Next executable action:** apply only the approved/document-consistency edits after inspecting the local worktree. Do not overwrite unrelated edits.

### Stage 1 — Traceability and inventory

#### S1-01 — Refresh the FR-to-code matrix
- **FRs:** all · `DOC` · **Status:** `PARTIAL` — the matrix exists and predates FR-019…040.

#### S1-02 — Reconcile contracts
- **FRs:** FR-001–FR-040, NFR-005, NFR-010 · `DOC` · **Dependencies:** S1-01
- **Remaining items to record (the rest were decided):** (a) FRD "waiting" wording versus `WAITING`/`PENDING_APPROVAL` (resolved by DD-12; confirm in code); (b) the ADR that forbids a second routing engine versus FR-027 (resolved by DD-05; write ADR-020); (c) the earlier Slice 63/70 NO-GO versus the owner's E2E credential (S5-01).
- **Status:** `PARTIAL`.

#### S1-03 — Select the highest-value unblocked source gap
- **FRs:** as selected · `SRC` · **Status:** pattern slice; use §4 order.

#### S1-04 — Extend traceability to FR-019…FR-040 and NFR-011…NFR-016
- **FRs:** FR-019–FR-040 · `DOC` · **Dependencies:** S1-01
- **Objective:** add rows to the **existing** matrix (no new file) using FRD v0.7 statuses and §1: source paths, tests, slice IDs, evidence layer, status, gap, next criterion.
- **Acceptance:** every new FR has a row; statuses come only from evidence; duplicates or absent code are flagged.
- **Status:** `NOT STARTED`.

#### S1-05 — Reconcile the decision register
- **FRs:** all · `DOC` · **Dependencies:** none
- **Objective:** reconcile FRD §10.1 (DD-01 to DD-17) with the canonical owner decision pack, marking each item **owner-stated**, **working default**, or **open**. Do not overwrite conflicting canonical decisions, fabricate signatures/dates, or treat this reconciliation as an operational authorization. One time only.
- **Status:** `NOT STARTED`.

### Stage 2 — Core ticket lifecycle and workflow

#### S2-01 — Lifecycle invariants and transition matrix
- **FRs:** FR-001, FR-003 · `SRC` · **Status:** `PARTIAL`
- **Reuse:** `PA_TICKETS_LIFECYCLE_TRANSITION_MATRIX.md`, `TicketLifecycleTests`, `TicketLifecycleConfiguredTransitionTests`.
- **Acceptance:** denied transitions leave state, events and save calls unchanged; allowed ones follow the matrix for every type.

#### S2-02 — Workflow cancellation and failure semantics
- **FRs:** FR-004 · `SRC` · **Status:** `PARTIAL`

#### S2-03 — Transaction boundary and event durability
- **FRs:** FR-005, FR-006 · `SRC` · **Status:** `PARTIAL` (source); provider part `BLOCKED`. Evidence: Slice 46, Slice 50.

#### S2-04 — Provider persistence and Incident atomicity
- **FRs:** FR-002, FR-005, FR-006 · `PRV` · **Dependencies:** S5-10
- **Objective:** rerun, under the existing identity guard, the lifecycle persistence test that failed on `S_MODULE_CODE`, then the Incident atomicity tests.
- **Status:** `BLOCKED` by S5-10.

#### S2-05 — API lifecycle acceptance
- **FRs:** FR-001–FR-006, FR-015 · `E2E` · **Dependencies:** S2-04 · **Status:** `BLOCKED`

#### S2-06 — Waiting with reason and waiting-on
- **FRs:** FR-026, FR-003, FR-011 · `SRC` (+ `SCH` for a missing column) · **Dependencies:** none (DD-12)
- **Reuse first:** `WAITING` state, `WaitAsync`/`ResumeAsync`, migration 004, the waiting-reason table, `PENDING_APPROVAL`, existing waiting/resume tests.
- **Objective:** make "set to waiting" require a reason from the waiting-reason catalogue and a waiting-on party (person, role, team, approval request, external); add a per-reason pauses-SLA flag (`N_PAUSES_SLA`, default on for waiting on requester or approval); expose waiting age; link an open approval request to the correct state. Do not add a second pending state.
- **Acceptance:** waiting without a reason is rejected and leaves state, events and saves unchanged; resume follows the matrix; waiting age is queryable; the SLA clock honors the flag.
- **Tests:** extend the waiting/resume tests; add negative tests for a missing reason and an unknown waiting-on.
- **Status:** `PARTIAL`. **Next:** inspect the `WaitAsync` signature, the API contract and the waiting-reason table, then list the exact gaps.

#### S2-07 — Identifier lookup and validation
- **FRs:** FR-035 · `SRC` · **Dependencies:** none (DD-04)
- **Reuse first:** `S_TICKET_NO` generation, the ticket-number sequence, the 16-character public ID and its unique index (migration 010).
- **Objective:** add lookup by ticket number and by public ID where missing; validate formats; test uniqueness and widening beyond 999999. **Do not change formats.**
- **Acceptance:** per FRD FR-035.
- **Status:** `PARTIAL` - added exact ticket-number and public-ID lookup at `/api/v1/tickets/lookup/{identifier}` with FR-035 public-ID alphabet validation and the existing per-ticket access check. Nineteen focused tests cover valid and malformed values, service-level access denial, unknown identifiers, exact lookup selection, ticket-number widening, hosted authentication, and standard 400/403/404 error responses. PostgreSQL query translation, deployed uniqueness, and persisted generation remain unverified.

#### S2-08 — Incident, problem, change and task specifics
- **FRs:** FR-039 · `SRC` · **Dependencies:** S2-01
- **Reuse first:** migrations 014–016 (Oracle-numbered sources), `ProblemsController`, `ChangesController`, Incident/Problem services.
- **Objective:** verify incident↔problem links, change impact assessment and implementation tracking against the FRD; fix the smallest gap.
- **Status:** `PARTIAL`. Provider part follows S5-09 and S5-10.

### Stage 3 — Identity, roles, hierarchy and assignment

#### S3-01 — Authentication contract
- **FRs:** FR-007, FR-015 · `SRC` · **Status:** `PARTIAL` (`HostedAuthenticationAuthorizationTests`).

#### S3-02 — Role labels, permissions and fixed/configurable classes
- **FRs:** FR-008, FR-020 · `SRC` + `SCH` · **Dependencies:** none (DD-01)
- **Reuse first:** `RoleAuthorizationPolicy.cs`, `SpectatorMutationPolicy.cs`, migrations 031–037, `CONFIGURABLE_RBAC_DESIGN.md`, the role-administration page.
- **Objective:** (1) configurable display labels for `ADMIN` and `ADMIN_HEAD` (reuse an existing name column or add a nullable one in a draft migration); (2) the new permission codes in §3 added to the allowlist and the reviewed seed draft; (3) role classes: fixed roles keep identity and core capabilities, configurable roles are editable inside ceilings; (4) default grants per FRD §4.4; (5) no code rename and no behavior change to existing authorization.
- **Acceptance:** unknown permission codes fail closed; fixed-role core grants cannot be removed; configurable roles can be edited inside ceilings; Spectator deny precedence still holds; role changes are audited; existing authorization tests still pass.
- **Status:** `PARTIAL` (system roles, ceilings and the Spectator invariant exist).

#### S3-03 — Effective grants, dates and revocation
- **FRs:** FR-008, FR-009 · `SRC` · **Status:** `PARTIAL`; provider race tests `BLOCKED`.

#### S3-04 — Scope and delegation
- **FRs:** FR-009 · `SRC` · **Status:** `PARTIAL` (`HostedDelegationAuthorizationTests`, `AdminHeadDelegationPolicyTests`); provider lineage `BLOCKED`.

#### S3-05 — Persisted authorization provider acceptance
- **FRs:** FR-007–FR-009 · `PRV` · **Dependencies:** S5-10 · **Status:** `BLOCKED`.

#### S3-06 — Fixed and configurable role invariants
- **FRs:** FR-020, FR-008 · `SRC` · **Dependencies:** S3-02
- **Objective:** enforce the §3 classes in the application policy and the role-administration API, with audit.
- **Acceptance:** negative and positive tests per class; Spectator mutation stays denied after configuration changes.
- **Status:** `PARTIAL`.

#### S3-07 — Hierarchy relations and assignment-authority rules
- **FRs:** FR-021, FR-009 · `SRC` + `SCH` · **Dependencies:** S3-02 (DD-02, DD-03)
- **Reuse first:** `ScopeAuthorizationPolicy.cs`, `AdminHeadDelegationPolicy.cs`, effective-dated membership history (023), `PA_TICKETS_USER_ROLE_SCOPE` and the delegation relation (035), assignment history and the `assign` endpoint (010).
- **Objective:** (1) model team → Manager scope and Manager Admin → Manager group from the existing relations; (2) add configurable assignment rules (seed R1 to R4 from FRD FR-021) with versioning and effective dates; (3) add `ASSIGN` and `DELEGATE` modes to assignment history with the assigner, target and notified Manager; (4) evaluate rules server-side on every assignment; (5) hard limits: active non-Spectator assignee, no scope widening, idempotent, audited.
- **Acceptance:** per FRD FR-021, including positive and negative tests for each seeded rule, a disabled rule, revoked or expired relations, and membership change during assignment.
- **Status:** `PARTIAL`. **Next:** inspect `assign` and the delegation policy, then write the rule model and its failing tests.

#### S3-08 — Single sign-on and multifactor authentication
- **FRs:** FR-037 · `SRC` · **Status:** `NOT STARTED`. **Post-V1 (M4).** Do not start before M3 (DD-15).

#### S3-09 — Assignment-rules administration screen
- **FRs:** FR-021, FR-028 · `UI` · **Dependencies:** S3-07 API contract
- **Objective:** Super Admin screen to view, create, edit, enable/disable and version assignment rules; show seeded defaults; preview who a rule lets assign to whom.
- **Acceptance:** Vitest specs for list, edit, validation errors, permission-denied and empty states; no business logic duplicated in the client.
- **Status:** `NOT STARTED`.

#### S3-10 — Manager notification on delegated or cross-team assignment
- **FRs:** FR-021, FR-031 · `SRC` · **Dependencies:** S3-07, S4-08
- **Objective:** when a rule has the notify flag, create a Message Center notification for the target user's Manager after the assignment commits.
- **Acceptance:** notification exists after commit, none after rollback; no duplicate on retry; the Manager sees only what scope allows.
- **Status:** `NOT STARTED`.

### Stage 4 — Supporting capabilities

#### S4-01 — Attachment authorization and integrity
- **FRs:** FR-010 · `SRC` · **Status:** `PARTIAL`; storage policy items remain open (FRD §10.2).

#### S4-02 — SLA and escalation behavior
- **FRs:** FR-011 · `SRC` · **Status:** `PARTIAL`.

#### S4-03 — Communications, notifications and retry
- **FRs:** FR-012 · `SRC` · **Status:** `PARTIAL`. See S4-12.

#### S4-04 — Reports, exports and schedules
- **FRs:** FR-013 · `SRC` · **Status:** `PARTIAL`. Migration 026 stays excluded.

#### S4-05 — Angular screen and API journey coverage
- **FRs:** FR-014, FR-015 · `UI` · **Status:** `PARTIAL`.

#### S4-06 — Module and feature switches
- **FRs:** FR-019, FR-028 · `SRC` + `SCH` + `UI` · **Dependencies:** none (DD-10)
- **Reuse first:** the RBAC module/action catalogue, `OperationalMetadataAdministrationController` and migration 022, the Angular admin area.
- **Objective:** switches for Message Center, Chat, chat attachments, Announcements and each channel, with global and department levels. Disabled features are hidden, their jobs stop and the API returns one consistent error, and the data is kept for Super Admin and auditors.
- **Acceptance:** state persists across restart; disabled-feature calls are denied; UI hides the feature; changes are authorized and audited; missing state fails closed.
- **Status:** `PARTIAL` — persisted switch repository/API, CONFIG_ADMIN authorization, hash-chain audit integration, Angular administration and caller availability UI gating are implemented; existing communication enqueue/retry/delivery paths are gated. PostgreSQL persistence and audit durability are unverified; Message Center, Chat, chat-attachment and announcement backend consumers remain absent. Migration 022 is Oracle syntax and remains untouched; PostgreSQL migration 040 is a review draft only.

#### S4-07 — Real-time hub (SignalR)
- **FRs:** FR-024, FR-005, FR-006 · `SRC` + `UI` · **Dependencies:** none (DD-13)
- **Reuse first:** hosted authorization test patterns (local Kestrel host), the durable job boundary (ADR-015/018), JWT bearer setup.
- **Objective:** an authenticated hub, single instance and in-process, behind a publisher abstraction. It pushes minimal events (id, type, change kind, version) to scope-derived groups after commit, using the durable job boundary. REST stays the only write path; only ephemeral typing/presence signals come from clients. Add `@microsoft/signalr` to the client as a **new dependency**: pin the version and justify it in the result (SOP §5). Confirm the server hub needs no extra package.
- **Acceptance:** unauthenticated connection rejected; out-of-scope subscription denied; Spectator cannot mutate; no event for a rolled-back commit; duplicate and out-of-order events handled by version; revoked access ends the subscription; tokens never logged.
- **Tests:** hosted tests for connect, subscribe and denial; unit tests for the publisher.
- **Status:** `PARTIAL` — authenticated `/hubs/updates` negotiation, self and authorization-checked ticket groups, a minimal ticket-event publisher abstraction, an Angular SignalR connection-state wrapper, and a shell status indicator are implemented. Three hosted/policy .NET tests and nine Angular service/shell tests pass. No mutation methods are exposed. Event production is not wired to committed domain events or the durable worker; role/scope revocation propagation and the `REALTIME` feature switch are not available in S4-06, so delivery remains disabled/unverified pending those contracts.

#### S4-08 — Message Center backend
- **FRs:** FR-031, FR-012, FR-024 · `SRC` + `SCH` · **Dependencies:** S4-06 (DD-08)
- **Reuse first:** `PA_TICKETS_NOTIFICATION`, `NotificationOrchestrator.cs`, migrations 013 and 021, ADR-011 (InApp contract).
- **Objective:** per-recipient inbox with read/unread/archived state, category, severity, deep link and dedup key; list, filter, search, mark read, mark all read, archive, unread count; per-user preferences per category and channel with admin-mandatory categories (default list in FRD FR-031); opening an item re-checks authorization.
- **Acceptance:** per FRD FR-031.
- **Status:** `NOT STARTED`.

#### S4-09 — Message Center Angular
- **FRs:** FR-031 · `UI` · **Dependencies:** none for unit work
- **Reuse first:** `client/src/app/layout/shell.*`, `core/http`, existing admin pages for style.
- **Objective:** top-bar bell with unread badge, dropdown panel, history page with filters and search, preferences page, toast for live items, connection-state indicator.
- **Acceptance:** Vitest specs for loading, empty, error, read, archive, filter and preference states; works with real-time off.
- **Status:** `PARTIAL` - top-bar bell, unread badge, dropdown, search/read-state filtering, read/archive actions, preference controls, and explicit unavailable/live-off states are implemented behind a frontend client port. Six component specs pass. The inbox API adapter, full history and preferences pages, and live toast/push contract remain absent; no backend contract was invented.

#### S4-10 — Chat backend
- **FRs:** FR-032, FR-010, FR-024 · `SRC` + `SCH` · **Dependencies:** S4-06, S4-07 (DD-09)
- **Reuse first:** `PA_TICKETS_COMMENT` (formal comments stay separate), attachment tables and storage contracts.
- **Objective:** work-item chat and direct chat; messages sent by REST and pushed by the hub; edit versions; soft delete; read receipts; mentions notify; attachments through the existing pipeline; `CHAT_AUDIT_VIEW` for audited Super Admin access.
- **Acceptance:** per FRD FR-032.
- **Status:** `NOT STARTED`.

#### S4-11 — Chat Angular
- **FRs:** FR-032 · `UI` · **Dependencies:** none for unit work
- **Objective:** work-item chat panel, direct-message drawer, attachment and mention pickers, typing indicator, message states.
- **Acceptance:** Vitest specs for send, receive, edit, delete tombstone, attachment errors, disabled-feature and offline states.
- **Status:** `PARTIAL` — work-item and direct-message shells render explicit unavailable states, keep unsupported actions disabled, and respect Chat availability. Drawer open/close now includes Escape handling and focus placement/return. REST message operations, history, edits/deletion, attachment handling, mentions, typing/presence and offline delivery behavior remain unavailable without approved Chat and real-time contracts; no behavior is simulated.

#### S4-12 — Notification channels, preferences and delivery states
- **FRs:** FR-012, FR-031 · `SRC` · **Dependencies:** S4-08
- **Objective:** route InApp through the orchestrator; honor preferences; model queued, attempted, accepted and delivered where providers support it; keep external content minimal. **No fake provider success** (ADR-011).
- **Acceptance:** each state is observable and audited; transient versus permanent failures are classified; retries are idempotent.
- **Status:** `PARTIAL`.

#### S4-13 — Announcements and operational notices
- **FRs:** FR-038 · `SRC` + `UI` · **Dependencies:** S4-08
- **Reuse first:** `PA_TICKETS_ANNOUNCEMENT` and `_TARGET`.
- **Objective:** admin API and screen for content, targeting, schedule, expiry and optional acknowledgement; delivery through the Message Center.
- **Acceptance:** per FRD FR-038.
- **Status:** `NOT STARTED`.

#### S4-14 — Approval extensions
- **FRs:** FR-025, FR-026 · `SRC` · **Dependencies:** S2-06 (DD-11)
- **Reuse first:** migrations 011, 019, 032; `ApprovalsController`; `ApprovalRuleAdministrationController`; independent-approval rules. Approval expiry is evaluated at decision time today, so timeouts and reminders use the durable job boundary, not timers.
- **Objective:** approver types (named, requester's Manager, assignee's Manager, another team's Manager, group, ad hoc), approval versus acknowledgement, patterns (single, sequential, parallel-any, parallel-all), ticket-scoped approver access for the request's lifetime, separation of duties, timeouts with reminders then escalation to the approver's Manager, delegation.
- **Acceptance:** per FRD FR-025.
- **Status:** `PARTIAL`.

#### S4-15 — Policy engine
- **FRs:** FR-027, NFR-013 · `SRC` + `SCH` · **Dependencies:** S4-14 (DD-05)
- **Reuse first:** `ConfigurationWorkflowRouter`, `WorkflowRouting:Rules`, the typed condition model (ADR-014), SLA escalation administration (020).
- **Objective:** write **ADR-020** (one database-backed, versioned engine; the existing routing configuration is the seed and fallback; never two engines for one decision); implement default conditions and actions from FRD FR-027, dry-run, deterministic precedence, loop protection and fallback queue. Never evaluate administrator-supplied code.
- **Acceptance:** per FRD FR-027.
- **Status:** `PARTIAL`.

#### S4-16 — Cross-department work
- **FRs:** FR-029 · `SRC` · **Dependencies:** S3-07, S4-15
- **Acceptance:** per FRD FR-029; each side sees only what scope allows.
- **Status:** `NOT STARTED`.

#### S4-17 — Progress, workload and dashboards
- **FRs:** FR-030, FR-022 · `SRC` + `UI` · **Dependencies:** S3-07 (DD-14)
- **Reuse first:** `DashboardsController`, `KpisController`, `KPI_ENGINE.md`, `DASHBOARD_API.md`, `client/src/app/features/dashboard`.
- **Objective:** own, team, grouped-team and organization views; open, overdue, stalled, waiting-by-party and workload views; default metrics from FRD DD-14; manager actions within scope, audited.
- **Acceptance:** scope matrix tests; counts reconcile with persisted data; disabled modules respected.
- **Status:** `PARTIAL`.

#### S4-18 — Business calendars and SLA pause
- **FRs:** FR-036, FR-011 · `SRC` · **Dependencies:** S2-06
- **Reuse first:** calendar, holiday and SLA-pause tables; `SlaAdministrationController`; ADR-013.
- **Acceptance:** due time across working hours and holidays; pause/resume per the waiting-reason flag; IST behavior; breach and escalation transitions.
- **Status:** `PARTIAL`.

#### S4-19 — Catalogue, intake and workflow-definition verification
- **FRs:** FR-033, FR-034 · `SRC` + `UI` · **Dependencies:** S1-04
- **Reuse first:** migrations 017–019, catalogue and workflow administration, `client/src/app/features/tickets/create`, ADR-008/014/019.
- **Objective:** compare against the FRD; fix the smallest gap (dynamic-field rendering from the backend catalogue, on-behalf-of submission recording requester and submitter, workflow version behavior for running instances).
- **Status:** `PARTIAL`.

#### S4-20 — Audit, history and evidence export extension
- **FRs:** FR-040, FR-006 · `SRC` · **Dependencies:** S4-08, S4-10 (DD-16)
- **Reuse first:** hash-chained audit (012), `AUDIT_HASH_DESIGN.md`, audit-package and security-event tables.
- **Acceptance:** notification and chat history are included; audit records stay append-only; exports are authorized and audited.
- **Status:** `PARTIAL`.

### Stage 5 — PostgreSQL readiness (M1)

#### S5-01 — E2E target authorization packet
- **FRs:** FR-016, NFR-002, NFR-010 · `DOC` · **Dependencies:** S5-08, S5-09
- **Objective:** the owner created the E2E credential and it produced partial results (DD-07). Slices 63/70 recorded NO-GO because the formal approval fields were never supplied. Codex prepares **one** authorization packet from the exact operations produced by S5-08 and S5-09. The **owner** fills and signs it. Codex never fills it in.
- **Packet fields (all required):** target host/port/database/role; operator and independent identity evidence; exact SQL files in order with SHA-256 of the exact bytes; expected effects per file; permitted test names; generated-write and cleanup boundary; validity window and expiry; revocation; backup or recreate-from-scratch recovery procedure for the disposable E2E database; evidence custody; uncertain-outcome handling.
- **Acceptance:** the owner-signed packet exists and names the exact scope. Without it the slice stays `BLOCKED`.
- **Status:** `BLOCKED` (waiting for the packet to be signed). **Do not repeat a report without new approval or evidence.**

#### S5-02 — Immutable migration artifact verification
- **FRs:** FR-016, NFR-007, NFR-010 · `DOC`
- **Known evidence:** root migration 014 historical blob SHA-256 `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3` was reproduced.
- **Status:** `PASS` for the historical blob hash only. It is not execution authorization.

#### S5-03 — Fresh read-only catalog fingerprint
- **FRs:** FR-016, NFR-001, NFR-007, NFR-010 · `PRV` · **Dependencies:** S5-01
- **Objective:** with the signed packet, run the guarded read-only fingerprint of the E2E target and compare it with the expected baseline.
- **Status:** `BLOCKED`.

#### S5-04 — Historical root migration 014 execution
- **Retired by DD-06.** Root 014 is not a PostgreSQL artifact. Incident, problem and change foundations are delivered as PostgreSQL drafts in S5-09 and applied by S5-10. Do not execute root 014.

#### S5-05 — Provider test-write authorization and cleanup boundary
- **FRs:** FR-002, FR-005, FR-006, NFR-003, NFR-010 · `DOC` · **Dependencies:** S5-01
- **Objective:** extend the signed packet with the exact provider test names, their unique-identifier cleanup, and uncertain-outcome handling.
- **Status:** `BLOCKED`.

#### S5-06 — Retired
- **Retired by DD-06.** Oracle is out of scope. Do not work this slice.

#### S5-07 — Schema review packet for all new objects
- **FRs:** FR-016, FR-019–FR-032 · `SCH` + `DOC` · **Dependencies:** the `SCH` drafts from S3-02, S3-07, S4-06, S4-08, S4-10, S4-15
- **Objective:** assemble all review-draft migrations into one numbered packet with the SHA-256 of the exact bytes, mapped to the build order. Not executed.
- **Acceptance:** exact bytes and hashes recorded; no ledger row inferred; no reference to migration 026.
- **Status:** `NOT STARTED`.

#### S5-08 — Split baseline adapters into DDL-only and data stages
- **FRs:** FR-016, NFR-007 · `SCH` · **Dependencies:** none
- **Reuse first:** `tests/scripts/postgresql-foundational-ddl.sql`, `postgresql-schema.sql`, `postgresql-slice1-schema.sql` (data-bearing, overlapping 028/029), `Docs/PA_TICKETS_RBAC_MIGRATION_REPAIR_DECISIONS.md`.
- **Objective:** produce **new** review-draft files that separate pure DDL from data, assign each overlapping column to exactly one owner (baseline or 028/029), and keep the original scripts unchanged. Do not run `provision-postgresql-schema.ps1`.
- **Acceptance:** DDL-only and data-only artifacts exist with hashes; an object-by-object table shows each object's owning script; no behavior is inferred from object shape.
- **Status:** `PARTIAL` - source-only review drafts and hashes are recorded in `Docs/reconciliation/proposed/postgresql/S5-08_BASELINE_SPLIT_REVIEW.md`. The original adapters remain unchanged; the artifacts are not syntax- or provider-validated.

#### S5-09 — PostgreSQL counterparts for root migrations that have none
- **FRs:** FR-002, FR-016, FR-039 · `SCH` · **Dependencies:** none
- **Objective:** inventory objects the source maps (`TicketsDbContext`, `ExecutionDbContext`, raw SQL) that have no PostgreSQL DDL, starting with `PA_TICKETS_INCIDENT` and the root 014–016 and 027 foundations; write review drafts in the PostgreSQL style (§2). Migration 026 stays excluded.
- **Acceptance:** an inventory of "mapped but missing" objects; a draft for each with prerequisites guarded; hashes recorded; nothing executed.
- **Status:** `PARTIAL` - five mapped ticket-type relations and a guarded 039 review draft are recorded in `Docs/reconciliation/proposed/postgresql/S5-09_MAPPED_SCHEMA_GAPS_REVIEW.md`. Root 014-016 are already PostgreSQL syntax despite their root location; 027 is Oracle syntax, while its mapped ticket columns are present in the baseline draft. Communication schema reconciliation, raw-SQL inventory, and provider compatibility remain unverified.

#### S5-10 — Build the PostgreSQL schema on the E2E target
- **FRs:** FR-016, NFR-003, NFR-010 · `PRV` · **Dependencies:** S5-01 signed, S5-03 PASS, S5-08, S5-09
- **Objective:** under the signed packet, apply the approved artifacts in the reviewed order (baseline DDL → 028, 029 → 033 → 034 → 035 → 036 → 037, plus the S5-09 drafts), with pre/post catalog checks, then seed only the owner-approved roles and permission catalogue. 038 and 026 are excluded.
- **Acceptance:** each file's effect matches its expectation; the permission-metadata column exists; the failing lifecycle test's cause is gone; no ledger row is invented; evidence is retained.
- **Status:** `BLOCKED`.

### Stage 6 — Persistence and end-to-end acceptance

#### S6-01 — Real-provider persistence matrix
- **FRs:** FR-001–FR-013, FR-016 · `PRV` · **Dependencies:** S5-10, S2-04, S3-05 · **Status:** `BLOCKED`.

#### S6-02 — Browser → API → database journeys
- **FRs:** FR-014, FR-015, FR-017 · `E2E` · **Dependencies:** S6-01 · **Status:** `BLOCKED`. Close the rows of `PA_TICKETS_API_E2E_ACCEPTANCE_MATRIX.md` (8 PASS / 3 PARTIAL / 13 BLOCKED today).

#### S6-03 — Concurrency, revocation and recovery
- **FRs:** FR-005, FR-006, FR-008, FR-009, FR-017 · `E2E` · **Dependencies:** S6-01 · **Status:** `BLOCKED`.

#### S6-04 — E2E cleanup and evidence audit
- **FRs:** FR-017, NFR-002, NFR-007, NFR-010 · `E2E` · **Status:** `BLOCKED`.

#### S6-05 — Real-time, Message Center and Chat acceptance
- **FRs:** FR-024, FR-031, FR-032 · `E2E` · **Dependencies:** S6-01, S4-07 to S4-11
- **Acceptance:** live delivery, revocation ends delivery, history persists and reloads, disabled features hide and keep history. Multi-instance delivery applies only after a backplane is introduced.
- **Status:** `BLOCKED`.

#### S6-06 — Hierarchy, assignment rules, approvals, policies and cross-department acceptance
- **FRs:** FR-021, FR-025–FR-029 · `E2E` · **Dependencies:** S6-01, S3-07, S3-10, S4-14 to S4-16 · **Status:** `BLOCKED`.

### Stage 7 — Release readiness

#### S7-01 — Operational and security release checklist
- **FRs:** FR-018, NFR-001–NFR-016 · `DOC` · **Status:** `NOT VERIFIED`.

#### S7-02 — Release and post-release verification
- **FRs:** FR-018 · **Dependencies:** S7-01 PASS and separate release approval · **Status:** `NOT VERIFIED`. No release is implied.

#### S7-03 — Standards alignment evidence
- **FRs:** NFR-014 · `DOC` · **Objective:** the compliance owner (the owner until another is named, DD-17) verifies and completes FRD Appendix C with evidence links. **No certification or compliance claim.**
- **Status:** `NOT STARTED`.

#### S7-04 — Backup, restore and continuity evidence
- **FRs:** NFR-016, FR-018 · `DOC` · **Status:** `NOT VERIFIED` (RPO/RTO open; scope includes attachments, chat and notification history).

---

## 6. Decision handling (from FRD §10.1)

Codex implements explicit owner-stated decisions as written after checking the canonical decision pack. Working defaults guide only safe, reversible work and do not constitute formal approval. Open questions may block the affected feature or operation, but must not unnecessarily block independent work.

| Decision | Governs |
|---|---|
| DD-01 Role codes, labels, fixed/configurable | S3-02, S3-06 |
| DD-02, DD-03 Relations and assignment rules | S3-07, S3-09, S3-10, S4-16 |
| DD-04 Identifiers | S2-07 |
| DD-05 One policy engine | S4-15 |
| DD-06 PostgreSQL only | S5-04, S5-06 retired; every `SCH` slice |
| DD-07 E2E target | S5-01, S5-03, S5-10 |
| DD-08 Message Center and announcements | S4-08, S4-12, S4-13 |
| DD-09 Chat | S4-10, S4-11 |
| DD-10 Feature switches | S4-06 |
| DD-11 Approvals | S4-14 |
| DD-12 Waiting | S2-06, S4-18 |
| DD-13 Real-time | S4-07 |
| DD-14 Performance metrics | S4-17 |
| DD-15 SSO/MFA post-V1 | S3-08 |
| DD-16 No physical deletion | S4-20, every `SCH` slice |
| DD-17 Standards | S7-03 |

---

## 7. Slice template

For any new slice use: **ID / title · FR IDs · Type · Stage and dependencies · Objective · Reuse first · Allowed changes / prohibited effects · Acceptance criteria · Test commands and expected evidence · Actual result / status · Next executable action.**

---

## 8. Cross-slice gates and permanent restrictions

- **Source → provider:** exact-target authorization, target guard and verified schema preconditions.
- **Provider → browser E2E:** provider acceptance and API contract evidence.
- **Schema change:** signed packet naming exact artifact bytes and hashes, target, operator, scope, window, recovery and evidence custody. New migrations are review drafts only.
- **Migration history:** never inferred from object shape; unknown stays `UNKNOWN`.
- **Real-time:** an event is published only after the producing transaction commits. The hub never writes business data.
- **Roles:** no role-code rename. New permission codes fail closed until the reviewed seed includes them.
- **Oracle:** no Oracle work of any kind (DD-06).
- **Release:** an approved Stage 7 package and operational evidence.
- **Always:** never access `pa_tickets_test` or `pa_tickets_h_verify`; never use `pa_tickets_test_user`; never run `tests/scripts/provision-postgresql-schema.ps1`; never modify `.env.test.local` or `.vscode/mcp.json`; never execute or alter migration 026; no database connection, SQL, migration, seed, cleanup or privilege change without exact-target authorization; no credentials in any output; no commit, push, reset, clean, stash or branch change unless explicitly requested; preserve all pre-existing worktree changes.

---

## 9. Slice completion ledger

Codex maintains this table. Do not invent dates; fill `Updated UTC` only when evidence is recorded. Initial values come from file-level inspection, not test runs. The ledger in this planning document is a starting snapshot, not a substitute for the Master Roadmap or fresh verification.

| Slice | FRs | Status | Evidence | Blocker | Next action | Updated UTC |
|---|---|---|---|---|---|---|
| S0-01 | NFR-002, 007 | PASS (docs only) | Roadmap | — | Re-check worktree | |
| S0-02 | FR-016, NFR-010 | PARTIAL | Reports in `Docs/` | — | Only with new evidence | |
| S0-03 | NFR-005, FR-023 | NOT STARTED | §1 | Owner approval | Prepare diffs | |
| S1-01 | all | PARTIAL | Traceability doc | — | See S1-04 | |
| S1-02 | FR-001–040 | PARTIAL | §1 | — | Record three remaining items | |
| S1-03 | as selected | PARTIAL | — | — | Use §4 order | |
| S1-04 | FR-019–040 | NOT STARTED | FRD v0.7 | — | Add rows to existing matrix | |
| S1-05 | all | NOT STARTED | FRD §10.1 | — | Append DD-01…17 to decision pack | |
| S2-01 | FR-001, 003 | PARTIAL | Matrix, lifecycle tests | Provider | Inspect first | |
| S2-02 | FR-004 | PARTIAL | Stage 2 result | Provider | — | |
| S2-03 | FR-005, 006 | PARTIAL | Slices 46, 50 | Provider | — | |
| S2-04 | FR-002, 005, 006 | BLOCKED | Bring-up: 1 pass, 1 fail (`S_MODULE_CODE`) | S5-10 | Rerun after schema build | |
| S2-05 | FR-001–006, 015 | BLOCKED | — | S2-04 | — | |
| S2-06 | FR-026 | PARTIAL | `WAITING`, migration 004 | — | Inspect `WaitAsync` | |
| S2-07 | FR-035 | PARTIAL | Exact lookup, FR-035 alphabet validation, 19 focused tests including hosted auth/error contract | PostgreSQL/deployed uniqueness and generation evidence | Verify under authorized provider acceptance | |
| S2-08 | FR-039 | PARTIAL | Root 014–016 sources | S5-09 for provider | Gap list | |
| S3-01 | FR-007, 015 | PARTIAL | Hosted auth tests | Provider | — | |
| S3-02 | FR-008, 020 | PARTIAL | Role policy, RBAC drafts | — | Labels, permissions, classes | |
| S3-03 | FR-008, 009 | PARTIAL | Grant tests | Provider races | — | |
| S3-04 | FR-009 | PARTIAL | Delegation tests | Provider | — | |
| S3-05 | FR-007–009 | BLOCKED | — | S5-10 | — | |
| S3-06 | FR-020, 008 | PARTIAL | Role admin UI | S3-02 | After S3-02 | |
| S3-07 | FR-021, 009 | PARTIAL | Assign endpoint, scopes, delegation | S3-02 | Rule model and tests | |
| S3-08 | FR-037 | NOT STARTED | OIDC contract | Post-V1 | Do not start | |
| S3-09 | FR-021, 028 | NOT STARTED | — | S3-07 | — | |
| S3-10 | FR-021, 031 | NOT STARTED | — | S3-07, S4-08 | — | |
| S4-01 | FR-010 | PARTIAL | Attachment tests | Policy open | — | |
| S4-02 | FR-011 | PARTIAL | SLA source | Provider | — | |
| S4-03 | FR-012 | PARTIAL | Orchestrator | Provider | See S4-12 | |
| S4-04 | FR-013 | PARTIAL | Reporting source | Provider | — | |
| S4-05 | FR-014, 015 | PARTIAL | Vitest suite | Browser E2E | — | |
| S4-06 | FR-019, 028 | PARTIAL | Switch API/UI, audited updates, communication channel gate; focused source tests | PostgreSQL persistence, missing Chat/Message Center/announcement consumers | 040 review draft only; no DB access | |
| S4-07 | FR-024 | PARTIAL | Authenticated hub negotiation; own-user and fail-closed permission-checked ticket subscription; minimal publisher abstraction; Angular reconnect/state service and shell indicator; 3 .NET and 9 Angular focused tests | No committed-event producer/durable scheduler path; no REALTIME switch; connection revocation propagation and browser event delivery unverified | Coordinate real-time switch and event/revocation contracts, then wire post-commit durable publication and run hosted/browser acceptance | |
| S4-08 | FR-031 | NOT STARTED | Notification table | S4-06 | Inspect notification code | |
| S4-09 | FR-031 | PARTIAL | Bell/dropdown, unread count, read/archive/filter/preferences controls; 6 component specs | Inbox API, full history/preferences pages, live push/toast | Implement only against the S4-08 contract when available | |
| S4-10 | FR-032 | NOT STARTED | None | S4-06, S4-07 | — | |
| S4-11 | FR-032 | PARTIAL | Work-item and direct-message unavailable UI; switch-hidden and API-unavailable states; disabled unsupported actions; Escape close and focus return; focused Angular specs | Contract-backed send/receive/edit/delete, attachment errors, mentions, persistence, realtime and offline delivery | Safe UI work only until approved Chat contracts exist |
| S4-12 | FR-012, 031 | PARTIAL | Orchestrator | S4-08 | After S4-08 | |
| S4-13 | FR-038 | NOT STARTED | Tables only | S4-08 | — | |
| S4-14 | FR-025 | PARTIAL | Migrations 011/019/032 | S2-06 | — | |
| S4-15 | FR-027 | PARTIAL | Routing config | S4-14 | ADR-020 first | |
| S4-16 | FR-029 | NOT STARTED | — | S3-07, S4-15 | — | |
| S4-17 | FR-030, 022 | PARTIAL | Dashboards, KPI | S3-07 | — | |
| S4-18 | FR-036 | PARTIAL | SLA admin, calendars | S2-06 | — | |
| S4-19 | FR-033, 034 | PARTIAL | Migrations 017–019 | — | Gap analysis | |
| S4-20 | FR-040 | PARTIAL | Audit chain | S4-08, S4-10 | — | |
| S5-01 | FR-016 | BLOCKED | Slices 63/70; bring-up | Owner-signed packet | Prepare packet | |
| S5-02 | FR-016 | PASS (hash only) | Slice evidence | — | — | |
| S5-03 | FR-016 | BLOCKED | — | S5-01 | — | |
| S5-04 | — | RETIRED | DD-06 | — | Do not work | |
| S5-05 | FR-002, 005, 006 | BLOCKED | — | S5-01 | — | |
| S5-06 | — | RETIRED | DD-06 | — | Do not work | |
| S5-07 | FR-016, 019–032 | NOT STARTED | — | `SCH` drafts | After drafts exist | |
| S5-08 | FR-016 | PARTIAL | DDL/data review drafts, ownership matrix, hashes | No SQL execution; syntax/provider validation absent | Review S5-08_BASELINE_SPLIT_REVIEW.md | |
| S5-09 | FR-002, 016, 039 | PARTIAL | Five mapped ticket-type gaps, guarded 039 draft | Communication/raw-SQL/provider review remains | Review S5-09_MAPPED_SCHEMA_GAPS_REVIEW.md | |
| S5-10 | FR-016 | BLOCKED | — | S5-01, 03, 08, 09 | — | |
| S6-01…S6-06 | various | BLOCKED | — | S5-10 | — | |
| S7-01, 02, 04 | FR-018, NFR | NOT VERIFIED | — | — | — | |
| S7-03 | NFR-014 | NOT STARTED | FRD Appendix C | — | — | |
