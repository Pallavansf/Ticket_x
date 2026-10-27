# PA_TICKETS Functional Requirements Document (FRD)

**Project:** PA_TICKETS — Enterprise Ticketing System  
**Repository:** `G:\WSS\aw\Tickets`  
**Branch baseline:** `workstream-i-lifecycle-authority`  
**Stack:** Angular standalone frontend (Angular 21 per the project intent document; verify against the repository), .NET 8 Clean Architecture backend, SignalR real-time layer, PostgreSQL (the only supported database)  
**Version:** 0.7 — governance and execution alignment; decision authority clarified; acceleration priorities and evidence limitations clarified (see §12)  
**Status:** Working requirements baseline derived from the Master Roadmap, Requirement-to-Code Traceability, owner-stated direction and the Project Intention and Complete Feature Scope document. It is not proof that every default is owner-approved, every repository decision is formally recorded, or every feature is implemented.

## 1. Purpose

Define the functional scope and acceptance expectations for PA_TICKETS in one place so each implementation slice can be validated against explicit requirements. This FRD consolidates requirements currently supported by repository roadmap and traceability evidence. Where source records are incomplete or decisions remain pending, the requirement is marked **TBD / owner decision required** rather than guessed.

**Origin tags.** Each requirement from FR-019 onward states its origin: *Owner* (direction given in conversation), *Intent* (the Project Intention and Complete Feature Scope document), or *Proposed* (suggested for completeness; the owner must accept or reject it). FR-001 to FR-018 are the existing baseline. Proposed items that are not in the baseline are collected in Appendix B. An origin tag is provenance, not by itself a formal approval record.

## 2. Product objective

Deliver a secure, usable, auditable enterprise ticketing application with real Angular-to-API-to-PostgreSQL behavior. The application supports five work-item types, lifecycle and workflow processing, configurable modules, roles and authorization, a department/manager hierarchy with team-performance visibility, secure real-time live updates via SignalR, evidence/attachments, SLA and communications, reporting/export/scheduling, and operationally safe persistence and release.

PostgreSQL is the only supported database and the integration-validation path (DD-06). Oracle is out of scope for now; if it is ever needed, the PostgreSQL scripts will be translated, and only one database is active in a deployment (FR-023).

### 2.1 Purpose and usage context
PA_TICKETS is a ticketing and task-monitoring application for use inside a department or across departments of an organization. Its purpose is to improve productivity and performance by letting people raise, assign, track and complete tickets and tasks; letting managers monitor progress and workload and manage work in detail; and routing work and decisions through policy-driven assignment, escalation and approval.

It must be fully configurable so the same product can serve different contexts, for example CRM, software development (SDE) and an organization's internal service/ticketing tool. Context-specific entities (for example CRM customers/accounts) beyond the existing client/resource scope are not defined in this FRD and are TBD (§10).

### 2.2 Intent, objectives and principles
PA_TICKETS is intended as a standalone, enterprise-grade internal work-management and ticketing platform for **one organization** (not a multi-tenant SaaS product; no `TENANT_ID` in the baseline). It turns informal requests, incidents, operational work, problems and changes into structured, accountable, measurable and auditable workflows. It is not a CRUD application where users create tickets and change statuses: it controls who can raise work, who owns it, how it progresses, which approvals are required, how deadlines are measured, how people communicate, and how every significant action can be verified later.

Core objectives:
1. **Structured intake** — every official request or issue has a type, category, owner, priority, status and traceable lifecycle (FR-033).
2. **Controlled execution** — work moves across departments and people through sequential, parallel and conditional steps, with reassignment, approval and escalation (FR-034, FR-025, FR-027).
3. **Accountability and auditability** — record who did what, when, why and under which authorization; preserve work and assignment history (FR-040).
4. **Measurable operations** — SLAs, escalation rules, KPIs and reports reveal delays, workload and bottlenecks (FR-011, FR-036, FR-022, FR-030).
5. **Configurable, maintainable platform** — administrators manage catalogues, workflows, SLA policies, permissions, modules and features without hard-coding each variation (FR-019, FR-028).
6. **Connected people** — an in-app Message Center and saved Chat keep people informed and coordinated, in addition to email, WhatsApp and push (FR-031, FR-032).

Design principles:
- The backend is the authority. The frontend renders the backend's catalogue, permitted actions, workflow state and configuration; it must not hard-code a second version of business logic. Hiding a button is a user-experience measure, never authorization.
- A status change alone is not evidence that a workflow transition was valid; transitions must satisfy the rules and be recorded consistently.
- Preserve history instead of overwriting it.
- Configurable by default, fail closed when configuration or authorization data is missing.
- Standards-aligned by design (NFR-014), without claiming certification.

### 2.3 Delivery milestones
Source-only and Angular work in a later milestone may proceed in parallel with an earlier one when its dependencies are independent. Provider-backed acceptance always follows the authorization gates in `SOP.md` and §7.

| Milestone | Scope | Exit evidence |
|---|---|---|
| **M1 — Persistence foundation and core acceptance** (current) | The PostgreSQL schema built on the authorized E2E target from the approved baseline and migrations; seeded roles and permission catalogue; lifecycle persistence and API route/role acceptance for the existing capabilities (FR-001 to FR-018) | Provider-backed tests recorded; the API/E2E acceptance checklist closed or explicitly accepted by the owner |
| **M2 — Collaboration core** | Hierarchy and assignment rules (FR-021), Waiting and approvals (FR-026, FR-025), feature switches (FR-019), real-time hub (FR-024), Message Center (FR-031), channels and preferences (FR-012), announcements (FR-038) | Source and Angular tests, then provider and browser acceptance |
| **M3 — Productivity and automation** | Chat (FR-032), policy engine (FR-027), dashboards and performance (FR-022, FR-030), cross-department work (FR-029), calendars and SLA (FR-036), catalogue and workflow verification (FR-033, FR-034), audit extension (FR-040) | As above |
| **M4 — Release and assurance** | Release readiness (FR-018), standards evidence (NFR-014), continuity (NFR-016) | Signed release checklist. Post-V1: SSO and MFA (FR-037) and the Appendix B items |

## 3. Scope

### 3.1 In scope
- Work-item types: `REQUEST`, `INCIDENT`, `PROBLEM`, `TASK`, `CHANGE`.
- Ticket creation, reading, updating, assignment, lifecycle transitions and workflow execution.
- Durable state transitions, concurrency handling, audit events and transaction/recovery behavior.
- Authentication and the five default system roles `USER`, `ADMIN`, `ADMIN_HEAD`, `SUPER_ADMIN`, `SPECTATOR` (§4), plus administrator-defined custom roles.
- Configurable modules that can be enabled/disabled and configured per deployment (FR-019).
- Role and permission administration (FR-020).
- Department/manager hierarchy, ticket assignment within scope and team-performance visibility (FR-021, FR-022).
- Real-time live updates to the Angular UI through an authenticated SignalR hub (FR-024).
- Approval and acknowledgement requests to other people, other departments' Managers, the requester's or assignee's Manager, or any designated person (FR-025).
- Pending state with a mandatory reason and waiting-on party (FR-026).
- Policy-based auto-assignment, escalation and approval routing (FR-027).
- A configuration framework and use-case profiles for CRM, software development and internal ticketing (FR-028).
- Cross-department work (FR-029).
- Progress tracking, workload monitoring and personal/team dashboards (FR-030).
- In-app Message Center with persistent notification history and push-style live alerts (FR-031).
- Saved Chat with attachments (FR-032).
- Service catalogue and request intake with dynamic fields (FR-033).
- Workflow definitions with versioning, and cross-department handoffs (FR-034).
- Work-item identifiers: internal key, public ID and human-readable ticket number (FR-035).
- Business calendars and SLA pause/resume (FR-036).
- Single sign-on and TOTP multifactor authentication (FR-037).
- Announcements and operational notices (FR-038).
- Incident, problem and change management specifics (FR-039).
- Audit, history preservation and evidence export (FR-040).
- Screen-based enable/disable of modules and features (FR-019).
- Alignment with recognized standards (NFR-014).
- PostgreSQL as the single supported database (FR-023).
- Configurable permissions, effective grants, client/resource scope and delegated administration.
- Attachments/evidence, ownership, secure download and storage isolation.
- SLA evaluation, escalation, notifications, communications and retry/persistence behavior.
- Reporting, exports, artifact download, schedules and background jobs.
- Angular UI, API contracts, validation/error behavior and operational logging.
- PostgreSQL schema reconciliation, migrations, persistence tests and full E2E acceptance, subject to separate operational authorization.
- Evidence-backed stage status and release readiness.

### 3.2 Out of scope or explicitly constrained
- Oracle: out of scope for now (DD-06). Existing Oracle files stay untouched and unmaintained; no new Oracle artifacts are produced.
- Migration 026: separate approval chain; do not execute or modify it under the current plan.
- Any schema/data/permission operation against a database not explicitly approved for that operation.
- Inferring migration history from existing object shape.
- Declaring provider, browser, or release readiness from source code or fake/in-memory tests alone.
- Using the real-time channel as a command/write path or as a system of record; the database and REST API remain authoritative (FR-024).
- Multi-tenancy; the baseline serves one internal organization.
- Any claim of ISO or other certification or compliance; the product is designed to support alignment only (NFR-014).

## 4. Actors and authorization model

### 4.1 Actors
- User.
- Manager — the head of a team (role code `ADMIN`).
- Manager Admin — the head of Managers across multiple teams (role code `ADMIN_HEAD`).
- Super Admin.
- Spectator (read-only executive/oversight viewer).
- Background worker/service actor where supported by the existing source contract.
- Database/release authority and least-privilege database operator for operational work.

### 4.2 Default system roles (v0.6)
**Naming (DD-01).** Role **codes** are the repository's: `USER`, `ADMIN`, `ADMIN_HEAD`, `SUPER_ADMIN`, `SPECTATOR`. "Manager" and "Manager Admin" are configurable **display labels** for `ADMIN` and `ADMIN_HEAD`. No code is renamed. Earlier FRD versions (v0.2 to v0.5) used MANAGER and MANAGER_ADMIN as if they were codes; read those as the display labels.

| Role code | Display label | Purpose | Defined capabilities | Visibility scope | Configurability |
|---|---|---|---|---|---|
| `USER` | User | Ordinary requester/worker | Create tickets; respond to tickets/tasks; view own work and progress | Own and assigned work items (team data off by default, configurable) | **Fixed** |
| `ADMIN` | Manager | Head of a team | Existing department-scoped capabilities (ticket read/update, user and role administration inside the department, `USER`/`SPECTATOR` membership); assign to self and team members; assign or delegate across teams per the assignment rules (FR-021); monitor the team's tickets and progress; view team performance; act as approver | Own team(s)/department | **Fixed** |
| `ADMIN_HEAD` | Manager Admin | Head of Managers across multiple teams | `ADMIN` capabilities plus: assign or delegate tasks to Managers in the delegated group and to users in their teams; view the performance of the grouped teams; manage delegated Managers (existing delegation) | Teams of delegated Managers | Configurable |
| `SUPER_ADMIN` | Super Admin | Full control | Manage, check or change anything: configuration, modules, roles, permissions, rules, users, tickets and data | Organization-wide | **Fixed** |
| `SPECTATOR` | Spectator | Executive/oversight viewer (for example a CEO) | Read-only view of everything inside its scope; no export by default | Organization-wide read-only by default | Configurable (visibility only) |

Hierarchy: `ADMIN_HEAD` → delegated Managers (`ADMIN`, effective-dated delegation) → each Manager's team(s) of `USER`s (team membership). `SUPER_ADMIN` and `SPECTATOR` sit outside the hierarchy.

### 4.3 Authorization rules
- The five default roles are **system roles**: seeded and not deletable. **Fixed** (`USER`, `ADMIN`, `SUPER_ADMIN`) means the role's identity, core capabilities and protections are locked: it cannot be deleted, deactivated, re-coded or stripped of its core capabilities. Super Admin may still add permissions inside the grant ceilings. **Configurable** defaults (`ADMIN_HEAD`, `SPECTATOR`) can have grants and visibility edited by Super Admin inside the ceilings. Every change is audited. Custom roles remain available (FR-020).
- Authorization is permission-driven. Code checks permissions and scope, not role names (NFR-011). Missing or ambiguous authorization data fails closed, and permission codes added later fail closed until a reviewed seed migration includes them.
- `SPECTATOR`: any effective active Spectator membership denies every mutation request even if another role grants write permission. This read-only invariant holds even when Spectator visibility is configured.
- `SUPER_ADMIN` has full control, realized as the reviewed all-permission catalogue (not a hidden bypass). Every action is audited. Separation-of-duties checks still apply; for example an approval decision requires being a designated approver, never the role name alone.
- `ADMIN` and `ADMIN_HEAD` are mutually exclusive for the same effective period (`ADMIN_HEAD` already includes `ADMIN` capabilities). `SUPER_ADMIN` is exclusive of other effective role memberships.
- **Relations (DD-02)** are explicit, effective-dated and audited: team membership; Manager scope to a team/department; and Manager Admin → Manager delegation (the existing delegation relation). The reporting-manager field (`N_MANAGER_USER_ID`) is informational only and never confers authority.
- **Assignment authority** is governed by configurable rules (FR-021), not by role name.

### 4.4 Default capability matrix
`Y` = allowed by default; `N` = not allowed by default; `Rule` = governed by the configurable assignment rules (FR-021); `Audited` = only through a dedicated audited permission. Every cell is a **seeded default** that Super Admin can change inside the ceilings, except fixed-role core capabilities and the Spectator invariant.

| Capability | USER | ADMIN (Manager) | ADMIN_HEAD (Manager Admin) | SUPER_ADMIN | SPECTATOR |
|---|---|---|---|---|---|
| Create ticket | Y | Y | Y | Y | N |
| Respond to ticket/task | Y | Y | Y | Y | N |
| Assign to self | N | Y | Y | Y | N |
| Assign to users in own team | N | Y | Y | Y | N |
| Assign or delegate to a Manager in the group | N | Rule | Y | Y | N |
| Assign or delegate to users of another Manager (notifies that Manager) | N | Rule | Rule | Y | N |
| View own/assigned tickets | Y | Y | Y | Y | Y |
| View team tickets | N | Y | Y | Y | Y |
| View team performance | N | Y | Y | Y | Y |
| View grouped teams' performance | N | N | Y | Y | Y |
| View all tickets/performance org-wide | N | N | N | Y | Y |
| Be designated approver/acknowledger (FR-025) | Y | Y | Y | Y | N |
| Author assignment rules (FR-021) | N | N | N | Y | N |
| Author policies (FR-027) | N | N | N | Y | N |
| Receive in-app notifications (FR-031) | Y | Y | Y | Y | Y |
| Send chat messages (FR-032) | Y | Y | Y | Y | N |
| Read other people's chat content (FR-032) | N | N | N | Audited | N |
| Enable/disable features and channels (FR-019) | N | N | N | Y | N |
| Change any ticket/data | N | N | N | Y | N |
| Configure modules, roles, permissions | N | N | N | Y | N |

## 5. Functional requirements

IDs are stable references for the slice plan. Statuses describe the current evidence, not a promise that implementation is complete.

### Requirement index

Statuses for FR-019 to FR-040 come from file-level repository inspection (v0.6), not from test runs.

| ID | Title | Origin | Status |
|---|---|---|---|
| FR-001 | Work-item types | Existing | PARTIAL |
| FR-002 | Ticket identity and persistence | Existing | PARTIAL |
| FR-003 | Lifecycle transition rules | Existing | PARTIAL |
| FR-004 | Workflow execution | Existing | PARTIAL |
| FR-005 | Transactional atomicity and recovery | Existing | BLOCKED |
| FR-006 | Auditability | Existing | PARTIAL |
| FR-007 | Authentication | Existing | PARTIAL |
| FR-008 | Configurable RBAC and permissions | Existing | PARTIAL |
| FR-009 | Scope and delegated administration | Existing | PARTIAL |
| FR-010 | Attachments and evidence | Existing | PARTIAL |
| FR-011 | SLA and escalation | Existing | PARTIAL |
| FR-012 | Communications and notifications | Existing | PARTIAL |
| FR-013 | Reporting, exports and schedules | Existing | PARTIAL |
| FR-014 | Angular user experience | Existing | PARTIAL |
| FR-015 | API contract and error handling | Existing | PARTIAL |
| FR-016 | PostgreSQL schema and migrations | Existing | BLOCKED |
| FR-017 | End-to-end acceptance | Existing | BLOCKED |
| FR-018 | Release readiness | Existing | NOT VERIFIED |
| FR-019 | Configurable modules | Owner | NOT STARTED |
| FR-020 | Role and permission administration | Owner | PARTIAL |
| FR-021 | Organization hierarchy, assignment authority and delegation | Owner | PARTIAL |
| FR-022 | Team performance visibility | Owner | PARTIAL |
| FR-023 | Single database (PostgreSQL) | Owner | PARTIAL |
| FR-024 | Real-time live updates (SignalR) | Owner | PARTIAL |
| FR-025 | Approval and acknowledgement requests | Owner | PARTIAL |
| FR-026 | Pending state with reason | Owner | PARTIAL |
| FR-027 | Policy engine: auto-assignment, escalation and approval routing | Owner | PARTIAL |
| FR-028 | Configuration framework and use-case profiles | Owner | PARTIAL |
| FR-029 | Cross-department work | Owner | NOT STARTED |
| FR-030 | Progress tracking, workload monitoring and dashboards | Owner | PARTIAL |
| FR-031 | In-app Message Center | Owner + Proposed | NOT STARTED |
| FR-032 | Chat | Owner + Proposed | NOT STARTED |
| FR-033 | Service catalogue and request intake | Intent | PARTIAL |
| FR-034 | Workflow definitions, versioning and handoffs | Intent | PARTIAL |
| FR-035 | Work-item identifiers | Intent | PARTIAL |
| FR-036 | Business calendars and SLA pause/resume | Intent | PARTIAL |
| FR-037 | Single sign-on and multifactor authentication | Intent | NOT STARTED |
| FR-038 | Announcements and operational notices | Intent | NOT STARTED |
| FR-039 | Incident, problem and change management | Intent | PARTIAL |
| FR-040 | Audit, history preservation and evidence export | Intent | PARTIAL |

### Work-item type definitions

| Type | Meaning |
|---|---|
| `REQUEST` | A user asks for a service, action, access, information or other business deliverable. |
| `INCIDENT` | An interruption, failure or operational issue that is recorded, assigned, investigated and resolved. |
| `PROBLEM` | Investigation of an underlying or recurring cause, including relationships to incidents and documented findings. |
| `TASK` | A discrete unit of work that is assigned, tracked, completed and linked to a larger item or workflow. |
| `CHANGE` | A controlled change with workflow, impact assessment, approval and implementation tracking as configured by policy. |

### FR-001 — Work-item types
The system shall support `REQUEST`, `INCIDENT`, `PROBLEM`, `TASK`, and `CHANGE` as distinct work-item types using the existing domain/API contracts. The types share a common work-item foundation while allowing type-specific fields, validation, workflows and business rules (FR-033, FR-034, FR-039).
- Acceptance: each type can be represented and retrieved through the appropriate contract; subtype-specific fields/records are validated; tests cover supported and invalid type combinations.
- Current evidence: source-level domain/application services exist.
- Status: **PARTIAL** — provider and full API/browser acceptance remain incomplete.

### FR-002 — Ticket identity and persistence
The system shall persist tickets and related records using the established identifier and mapping conventions without changing externally observable behavior without approval.
- Acceptance: create, commit, dispose/reload in a fresh scope, and verify the same persisted record and required related evidence.
- Status: **PARTIAL** — an isolated repository persistence proof has passed previously; full lifecycle/provider acceptance remains incomplete.

### FR-003 — Lifecycle transition rules
The system shall enforce valid lifecycle transitions and reject invalid terminal-state changes without changing status, emitting invalid domain events, or saving invalid mutations.
- Acceptance: transition matrix covers allowed and denied transitions for all applicable types; rejected operations leave aggregate state/events/persistence unchanged.
- Status: **PARTIAL** — source-level regressions exist; provider-backed lifecycle coverage remains incomplete.

### FR-004 — Workflow execution
The system shall execute configured workflow steps durably, preserve execution state, and correctly handle cancellation, failure and retry behavior.
- Acceptance: workflow success, failure, cancellation and in-flight cancellation are tested; cancelled steps are not misclassified as failures; persisted state matches the result.
- Status: **PARTIAL** — source-level tests exist; durable provider evidence is incomplete.

### FR-005 — Transactional atomicity and recovery
Ticket, subtype, lifecycle, audit and workflow changes that are defined as one operation shall commit atomically. Uncertain commit outcomes shall be reconciled rather than blindly retried.
- Acceptance: provider failure injection proves rollback/atomicity; audit and subtype rows do not become inconsistent; uncertain outcomes are classified and reconciled without automatic retry.
- Status: **BLOCKED** for provider acceptance pending operational authorization and schema readiness.

### FR-006 — Auditability
The system shall record relevant actor, action, timestamps, lifecycle changes and audit evidence according to the existing audit contract. Audit failures must not silently produce a success claim.
- Acceptance: persisted audit event/chain evidence matches the operation; failures and rollback behavior are tested.
- Status: **PARTIAL** — provider durability and full audit-chain evidence remain incomplete; provider acceptance is BLOCKED (FR-005).

### FR-007 — Authentication
The system shall expose authenticated API behavior through the existing authentication contracts and handle invalid, missing or expired credentials consistently.
- Acceptance: positive and negative authentication tests, claim validation, and API error contract tests.
- Status: **PARTIAL** — persisted identity/session and end-to-end behavior remain to be verified.

### FR-008 — Configurable RBAC and permissions
Authorization shall be based on the approved configurable permission catalogue, effective grants, scope and role semantics; missing or ambiguous authorization data shall fail closed. Roles and module availability are administrator-configurable (FR-019, FR-020); the five default roles are system roles seeded with owner-approved default grants (§4).
- Acceptance: positive/negative HTTP matrix; persisted authorization facts; no implicit privilege from missing records; role/permission canonicalization and duplicate handling follow owner decisions.
- Status: **PARTIAL** — owner decisions and provider-backed authorization evidence remain incomplete; provider acceptance is BLOCKED.

### FR-009 — Scope and delegated administration
The system shall enforce approved organization/department/team/client/resource scope and effective-dated delegation. Revocation and expiry must be respected. Manager scope is the managed department/team and Manager Admin scope is the teams of grouped Managers (FR-021).
- Acceptance: in-scope access succeeds; out-of-scope access is denied; expiry/revocation races are tested; delegation records have traceable authorship and lifecycle.
- Status: **PARTIAL** — provider-backed lineage and race tests remain incomplete.

### FR-010 — Attachments and evidence
The system shall enforce attachment ownership and authorization before access, provide secure downloads, and preserve storage confidentiality/integrity.
- Attachments are linked to work items (and, where enabled, to chat conversations — FR-032), with metadata, versioning, SHA-256 integrity hashes and preserved attachment/evidence history.
- Desired delivery controls (discussed, not confirmed as implemented): authorized upload/download, signed download URLs, and rate/download restrictions for protected files.
- Storage policy — retention, maximum size, allowed file types, malware scanning, encryption and the production storage provider — is TBD and must be validated against the final security requirements. An upload endpoint alone does not satisfy it.
- Acceptance: unauthorized callers cannot read metadata or content; persisted relationships and object hashes are verified; storage ACL and TOCTOU cases are tested.
- Status: **PARTIAL** — source safeguards exist; provider/storage isolation evidence remains incomplete.

### FR-011 — SLA and escalation
The system shall evaluate configured SLA policies and escalate work items according to approved rules. SLA targets may vary by service, category, priority or other approved criteria and use business calendars (FR-036). The exact calculation, pause, reset and escalation rules are TBD; they must be explicit and tested, not guessed.
- Acceptance: policy boundary, due-time, escalation and persistence/worker behavior tests; time zone and retry semantics are explicit.
- Status: **PARTIAL** — source behavior exists; provider/worker acceptance remains incomplete.

### FR-012 — Communications and notifications
The system shall support the documented communication/notification channels and durable dispatch/retry behavior.
- **Channels:** in-app Message Center (FR-031), email, WhatsApp and push notifications. A pending-communication queue and provider-adapter contracts were discussed.
- **Delivery states:** where the provider supports them, distinguish queued, attempted, accepted by the provider and confirmed delivered; record provider references and keep every communication traceable to its work item.
- **Reliability controls (desired, not claimed as implemented):** idempotency, transient-versus-permanent failure classification, throttling, retry policies, dead-letter handling and provider reconciliation.
- Acceptance: queued, sent, transient failure, permanent failure, retry and terminal failure behaviors are observable and auditable; duplicate delivery is controlled according to the approved contract.
- Status: **PARTIAL** — source seams exist; provider/worker persistence evidence remains incomplete.

### FR-013 — Reporting, exports and schedules
The system shall authorize reports, generate export artifacts, verify artifact integrity, provide authorized download, and support scheduled/background execution where configured. Reports are scoped by user permissions and built from consistent persisted data. Report definitions and KPI formulas are TBD and must not be inferred from the current UI. Scheduled management summaries are in scope as requirements are finalized. Audit and evidence exports are covered by FR-040.
- Acceptance: authorization precedes data/artifact access; completed artifact hash matches persisted metadata; expired/incomplete artifacts are rejected; schedule/worker behavior is tested.
- Status: **PARTIAL** — a source-level export download fix and regression test are documented; provider worker/storage behavior remains incomplete.

### FR-014 — Angular user experience
The Angular standalone UI shall expose routes, screens, forms, validation, ticket actions and API integration consistent with backend contracts.
- **Design direction:** a modern, polished enterprise interface with a Microsoft Learn-inspired visual language, considered spacing and typography, restrained gradients, clear hierarchy and responsive layouts.
- **Core screens:** work queues, lists with filters, detail pages and action panels; forms that render the fields configured for the selected service or type (FR-033); workflow steps and approval actions that mirror backend state; role-appropriate navigation; dashboards (FR-030); a top-bar Message Center (FR-031) and Chat (FR-032).
- **States:** clear loading, validation, empty and error states.
- The UI consumes the backend's actual catalogue, permitted actions, workflow state and configuration and does not duplicate business logic.
- Acceptance: focused component/service specs plus browser-to-API journeys for key roles and lifecycles; loading, empty, success, validation and error states are verified.
- Status: **PARTIAL** — Angular source/specs exist; full browser E2E remains unverified.

### FR-015 — API contract and error handling
The API shall validate inputs, enforce authorization, return consistent error responses, and avoid leaking secrets or sensitive data through errors/logs.
- Acceptance: request validation, authorization denial, conflict/concurrency, not-found, unexpected error and secret-redaction tests.
- Status: **PARTIAL** — source tests exist; complete operational telemetry and API/browser acceptance remain incomplete.

### FR-016 — PostgreSQL schema and migrations
PostgreSQL schema changes shall be based on an approved baseline, exact artifact provenance, target identity verification, and an operation-specific execution/recovery plan.
- Acceptance: exact migration bytes/hash are verified; current target and catalog are read-only inspected first; expected/observed definitions are reconciled; transaction outcome and post-apply catalog evidence are retained.
- Status: **BLOCKED** — authorization and migration history remain unresolved. Migration 026 is excluded.

### FR-017 — End-to-end acceptance
The product shall be validated through real Angular → API → PostgreSQL flows across roles, work-item types, scopes, attachments, reports, audit and failure paths.
- Acceptance: persisted fixtures, HTTP/browser matrices, concurrency/revocation tests, recovery/cleanup evidence and no protected-target access.
- Status: **BLOCKED** by provider readiness and unresolved authorization gates.

### FR-018 — Release readiness
Release shall require operational runbooks, backup/restore evidence, migration/recovery plan, security review, monitoring/support plan, approvals and post-release verification.
- Acceptance: completed release checklist backed by real evidence.
- Status: **NOT VERIFIED**.

### FR-019 — Configurable modules
The system shall be composed of modules that an authorized administrator can enable, disable and configure without code changes.
- Candidate module catalogue (final list TBD): Tickets/Work items, Workflow, SLA & Escalation, Communications/Notifications, Attachments/Evidence, Reporting/Exports/Schedules, Team Performance, Progress & Dashboards, Approvals, Policies/Automation, Real-time Updates, Message Center, Chat, Announcements, Service Catalogue, SLA & Calendars, Audit, Administration (users, roles, permissions).
- A disabled module's UI navigation, API endpoints and background jobs are inactive and return a consistent error; its permissions are not effective.
- **Screen-based feature switches:** authorized administrators enable or disable features from an admin screen, for example the Message Center, Chat, attachments in chat, Announcements, and each notification channel (in-app, email, WhatsApp, push). Defaults and overrides can apply at global, department and role scope (default levels: global and department; role-level overrides are deferred — DD-10). Disabling a feature hides its UI, stops its jobs and denies its API calls, and **preserves existing data and history**; by default users no longer see a disabled feature, and its data is retained and available to Super Admin and auditors (DD-10).
- Module enable/disable and configuration changes are authorized by permission and audited. Which modules cannot be disabled (for example Tickets, Administration, Audit) is TBD.
- Acceptance: module state is persisted and survives restart; disabled-module API calls are denied; UI hides unavailable modules; per-role module visibility is enforced server-side; changes are audited; fail closed when module state is missing.
- Status: **PARTIAL** — feature-switch catalog, global/department resolution, CONFIG_ADMIN-protected API and Angular administration exist in source; database persistence, restart behavior, audit durability and full consumer enforcement remain unverified or incomplete.

### FR-020 — Role and permission administration
The system shall let authorized administrators (default: Super Admin) create, rename, clone, deactivate and assign custom roles, and map permissions per module to roles.
- The five default system roles cannot be deleted. Fixed roles (`USER`, `ADMIN`, `SUPER_ADMIN`) cannot be edited; configurable defaults (`ADMIN_HEAD`, `SPECTATOR`) can be changed by Super Admin only, with audit.
- Role assignment to users is effective-dated where required by FR-009 and respects revocation.
- Acceptance: positive/negative HTTP matrix; duplicate/case-alias handling follows owner decision; no privilege escalation by editing own role unless explicitly authorized; all changes audited; permission changes take effect per the revocation semantics in FR-008.
- Status: **PARTIAL** — custom-role administration and the role editor exist in source; fixed/configurable role classes are not modeled (repository inspection; not provider-verified).

### FR-021 — Organization hierarchy, assignment authority and delegation
The system shall model teams, their Managers, team members and the grouping of Managers under a Manager Admin, and shall enforce **configurable assignment-authority rules** that decide who may assign or delegate work to whom.
- Origin: Owner (hierarchy, assign and delegate, notify the Manager, configurable); seeded defaults marked Default (DD-03).
- **Relations (DD-02):** explicit, effective-dated and auditable: team membership; Manager scope to a team/department; Manager Admin → Manager delegation (the existing delegation relation). The reporting-manager field never confers authority.
- **Modes:** *explicit assign* names a specific user. *Delegate* hands the task to another Manager, who then assigns within their own team, and responsibility moves with it. Both are recorded in assignment history with mode, assigner, target and the Manager who was notified.
- **Seeded default rules** (configuration data, not code):
  - R1: a Manager Admin may assign or delegate to a Manager in the delegated group, and assign to users in that group's teams.
  - R2: a Manager may assign to self and to users in their own team(s), and reassign within the team.
  - R3: a Manager may explicitly assign or delegate to a user (or Manager) in another team of the same Manager Admin group, for example a sub-manager's user. The target user's Manager is **always notified**; this flag is configurable.
  - R4: a User has no assignment authority.
- **Rule attributes (all configurable):** assigner role or permission; relation to the target (self, own-team user, group Manager, group-team user, any); mode (assign or delegate); notify the target's Manager; require that Manager's acceptance (default off); enabled; effective dates; version. Super Admin administers rules; delegating that authority is itself a permission.
- **Hard limits (not configurable):** the assignee must be an active, non-Spectator user; a rule never widens the assigner's reach beyond the specific ticket it grants; every assignment is authorized on the server, idempotent and audited; Spectator deny precedence applies.
- **Notification:** the target's Manager receives a Message Center notice (FR-031), created after commit.
- Acceptance: each default rule has positive and negative tests; out-of-scope, revoked and expired relations are denied; a disabled rule denies; the Manager notification and assignment history (mode, via) are produced; rule changes are audited and honor version and effective dates; membership change during assignment is tested.
- Status: **PARTIAL** — assignment history and scoped reassignment exist (migration 010, `POST /api/v1/work-items/{publicId}/assign`) and delegation exists for administration (migration 035); configurable assignment rules and delegate mode do not (repository inspection; not test-verified).

### FR-022 — Team performance visibility
The system shall provide performance views scoped by role: a Manager sees their own team; a Manager Admin sees the teams of their grouped Managers (per-Manager breakdown and aggregate); Super Admin and Spectator see organization-wide.
- Authorization and scope are checked before any performance data is returned; views are read-only.
- Default metrics (DD-14; periods day, week and month; targets are configurable): tickets created/assigned/resolved, open backlog, SLA compliance, average resolution time, reopen rate, pending count and pending aging, approval turnaround time, workload per person, escalation count.
- Acceptance: scope matrix tests (in-scope returns, out-of-scope denied); figures reconcile with persisted tickets; export follows FR-013.
- Status: **PARTIAL** — dashboards, the KPI engine and reports exist in source; team/group scoping and the added metrics are not verified.

### FR-023 — Single database (PostgreSQL)
PostgreSQL is the only supported database (DD-06). Oracle is out of scope for now; if it is ever needed, the PostgreSQL scripts will be translated, and only one database is active in a deployment.
- Origin: Owner.
- New schema artifacts are PostgreSQL migrations only. Existing Oracle files and the Oracle configuration branch stay in the repository untouched and unmaintained: not extended, not tested, not deleted. No Oracle evidence is claimed.
- Provider-specific SQL stays inside Infrastructure so a future translation remains possible; domain and application layers stay database-neutral.
- Acceptance: all provider-backed evidence is PostgreSQL evidence; no new Oracle artifact is added; provider-specific SQL is confined to Infrastructure (review, plus tests where patterns exist).
- Status: **PARTIAL** — the PostgreSQL path is wired; one repository persistence test passed on the authorized E2E target and the lifecycle persistence test failed closed on a missing permission-metadata column (see §7).

### FR-024 — Real-time live updates (SignalR)
The system shall push live updates to authenticated Angular clients through an ASP.NET Core SignalR hub, so ticket lists, ticket detail views, assignments, notifications, SLA alerts and performance dashboards refresh without manual reload. The real-time channel is a one-way notification path; it is not a system of record and not a command channel.

**Design rules**
- **Server-to-client push only.** State changes are made through the REST API. Hub methods from the client are limited to subscribe, unsubscribe, acknowledge and, when Chat is enabled, ephemeral typing/presence signals that are not persisted; no hub method mutates or persists business data. Chat messages are sent through the REST API and delivered by push (FR-032).
- **Commit-then-publish.** Events are emitted only after the producing transaction has committed. No event is emitted for a rolled-back or uncertain commit (FR-005, FR-006). The publishing mechanism (for example a transactional outbox) is TBD; any schema addition follows §7 authorization and is per provider (FR-023). This FRD does not authorize any migration.
- **Minimal payload.** Events carry identifiers, entity type, change kind, version/row-version and timestamp. Clients fetch full data through the authorized REST API. No secrets, credentials or attachment content are pushed.
- **Authorization at connect and at subscribe.** The hub requires authentication. Subscriptions are limited to scope-derived groups: own user, ticket the caller may read, department/team (Manager), grouped Managers' teams (Manager Admin), organization-wide (Super Admin, Spectator). Missing or ambiguous authorization data fails closed.
- **Revocation propagation.** Role, grant, scope, delegation, module or account changes update or remove affected group memberships and terminate affected connections within a bound that is TBD (FR-008, FR-009, FR-019). Expired tokens end the connection or force re-authentication.
- **Delivery semantics.** At-least-once and best-effort ordering per entity by version. Clients treat events as hints, de-duplicate by version, and re-sync through REST after reconnect. A missed event must never leave the UI permanently stale.
- **Spectator** receives read-only events only; **Manager Admin** and **Manager** receive only events inside their scope.

**Candidate event catalogue** (final list and payload contract TBD): ticket created, updated, status changed, assigned/reassigned, commented, attachment added; approval requested/decided; ticket set to or released from pending; SLA warning/breach/escalation; in-app notification (FR-031); chat message (FR-032); announcement (FR-038); team-performance refresh; permission/module change affecting the current session.

**Enterprise operational requirements**
- Scale-out (DD-13): start with a single API instance and an in-process hub. Keep the publisher behind an abstraction so a backplane or managed SignalR service can be added when more than one instance is needed. Publishing reuses the durable job boundary instead of a new outbox table.
- Transport: WebSockets preferred, with a fallback policy that is TBD; secure transport (WSS/TLS) only; allowed origins restricted.
- Token handling: tokens used for the WebSocket handshake are never written to logs; query strings are redacted (NFR-006).
- Protection: per-user connection limits, message rate limits and maximum payload size (values TBD).
- Observability: metrics for active connections, publish-to-delivery latency, reconnects and failures; structured logs without secrets; health checks that report real-time status.
- Graceful degradation: if the real-time channel is unavailable or the module is disabled (FR-019), the UI keeps working with manual refresh or polling and shows a connection-status indicator.
- Angular: a standalone service wraps the SignalR client with automatic reconnect and backoff, cleans up subscriptions on destroy, shows connection state, and updates view state without losing unsaved user input.

- Acceptance:
  - Unauthenticated connection is rejected; out-of-scope subscription is denied; Spectator cannot trigger mutations through the hub.
  - No event is published for a rolled-back or failed commit; events appear only after commit.
  - Revoking a role, scope, delegation or account stops further events to the affected connections within the approved bound.
  - Duplicate, out-of-order and missed events are handled by version checks and REST re-sync.
  - Once a backplane is introduced, two or more API instances deliver an event to a client connected to another instance.
  - Disabled module or unavailable hub degrades gracefully in the UI.
  - Logs contain no tokens or secrets.
  - Real-time evidence is labelled by layer: unit, integration/provider, or browser E2E.
- Status: **PARTIAL** — an authenticated hub endpoint, authorization-checked ticket subscription source, and Angular connection-state client exist. REALTIME is now present in the generic source catalog and PostgreSQL review-draft constraint; persistence remains unverified, so missing or unavailable configuration continues to deny hub use. The publisher is scoped to match IFeatureSwitchService, but no production publish call site exists. Post-commit event production/durable scheduling, active-session revocation, and browser event delivery remain unimplemented or unverified.

### FR-025 — Approval and acknowledgement requests
A work item may require approval or acknowledgement from one or more people before it proceeds.
- **Approver types:** a named person; the requester's Manager; the assignee's Manager; a Manager of another department; any eligible member of a role/group; or any designated person (ad hoc). `SPECTATOR` cannot be an approver because of its read-only rule.
- **Kinds:** approval (approve/reject) and acknowledgement (acknowledge only). Default (DD-11): a rejection requires a comment.
- **Patterns:** single approver, sequential chain, and parallel (all/any/quorum). Defaults (DD-11): single, sequential, parallel-any and parallel-all.
- **Triggers:** requested manually by an authorized actor, or automatically by policy (FR-027).
- **Effect on the item:** while a request is open the item is put into Pending with the reason and waiting-on party (FR-026). On approval it resumes as defined; a rejection returns the item to the assignee (DD-11).
- **Access:** an approver, including one from another department, gets scoped, time-bound access only to what is needed to decide on that item, and the access ends when the request closes (FR-009).
- **Separation of duties:** a requester cannot approve their own request unless a policy explicitly allows it (default: never).
- **Timeouts and delegation:** due dates, reminders and timeout behavior (escalate, reassign to the approver's Manager, or delegate) follow policy; approver delegation is effective-dated and respects revocation.
- **Notification and audit:** approvers are notified in-app and in real time (FR-012, FR-024); every request, decision, actor, time and comment is audited.
- Acceptance: each approver type and pattern works; unauthorized decisions are denied; scoped access is limited and expires; the item pauses and resumes correctly; timeout and delegation behavior is tested; audit evidence matches.
- Status: **PARTIAL** — durable approval instances, approval rules and independent approval exist in source; approver types, scoped approver access and timeouts are not verified.

### FR-026 — Pending state with reason
A work item can be moved to a Pending state, instead of being closed, with a mandatory reason and the party or condition it is waiting on.
- The reason is required (free text and/or configurable reason code). Waiting-on can be a person, role, department, an approval request or an external party.
- Pending items are tracked, aged and visible to managers (FR-030).
- Exit paths: manual resume, an approval decision, or a policy timer. Resume returns to the previous status unless otherwise defined.
- Default (DD-12): the SLA clock pauses when the waiting reason is flagged as pausing (configurable per reason; default on for waiting on the requester or on an approval).
- The authoritative lifecycle transition matrix still governs (FR-003). Whether Pending already exists as a status or sub-state, and which transitions apply per work-item type, must be reconciled in S1-02. No transition rule changes without owner decision (NFR-005).
- Acceptance: Pending without a reason is rejected; invalid transitions are denied; state, events and audit stay consistent; pending age is visible in dashboards; SLA behavior follows the decision.
- Status: **PARTIAL** — the `WAITING` state, waiting/resume and a waiting-reason table exist; mandatory reason and waiting-on enforcement are not verified.

### FR-027 — Policy engine: auto-assignment, escalation and approval routing
Authorized administrators shall configure policies that decide automatically when and to whom to auto-assign, escalate, or request approval.
- Origin: Owner (policy-based auto-assign, escalate and approve); defaults marked Default (DD-05).
- **Single engine (DD-05):** one decision engine, backed by the database, versioned, and built on the existing typed condition model. The existing `WorkflowRouting:Rules` and `Triage` configuration is the seed and fallback until migrated. Two engines never evaluate the same decision. A new ADR (ADR-020) records this and supersedes the earlier "no second database-backed routing engine" boundary.
- **Not the same as FR-021:** assignment-authority rules decide who **may** assign; policies decide who **will be** auto-assigned.
- **Policy shape:** conditions + actions + scope + effective dates.
- **Default conditions:** work-item type, priority, service/category, department/team, client, requester, age, SLA state, waiting duration, workload.
- **Default actions:** auto-assign (team queue, round-robin, least-loaded); escalate to a Manager, Manager Admin or named person with levels and timeouts; require approval/acknowledgement (FR-025); notify; set Waiting (FR-026). Administrator-supplied code is never evaluated.
- **Governance:** versioned, effective-dated, enable/disable-able, audited. A dry-run is available before activation. Precedence is deterministic: specificity, then priority, then version. Loop and storm protection applies (default maximum escalation depth 3; rate limits).
- **Fail-safe:** when no policy matches or the target is unavailable, the item falls back to a defined queue or Manager and is never silently dropped.
- **Explainability:** each automated decision records the policy version and the inputs that caused it (NFR-013).
- **Authorization:** automated actions run as a system/service actor and must not bypass authorization or scope rules (FR-008, FR-009). Timers use the durable job boundary, never in-memory timers.
- Relationship to FR-011: this extends SLA and escalation. Reuse the existing SLA/escalation code.
- Acceptance: tests per action type, precedence, no-match fallback, loop protection, time boundaries and worker persistence; audit records the policy version.
- Status: **PARTIAL** — routing rules, SLA escalation administration and the typed condition model exist in source; the policy-engine layer does not (repository inspection; not test-verified).

### FR-028 — Configuration framework and use-case profiles
The product shall be fully configurable by authorized administrators without code changes: departments/teams, categories/queues, priorities, custom fields per work-item type, forms, workflow/approval templates, SLA, policies (FR-027), dashboards, notification templates, modules (FR-019) and roles (FR-020).
- **Use-case profiles:** configuration packs as starting points for typical contexts (CRM, software development, internal service desk). Their content is TBD and is not hard-coded.
- **Change control:** configuration is versioned and audited. How live configuration changes affect existing items is TBD; they must not silently alter existing items. Configuration import/export is TBD.
- **Bounds:** the lifecycle invariants (FR-003) and the five work-item types (FR-001) stay fixed unless the owner approves a change. Whether admins may define new statuses or types is TBD.
- **Authorization:** configuration is permission-gated per area. Whether configuration is global or per department is TBD. Super Admin has all.
- Acceptance: configuration round trip; invalid configuration is rejected; missing configuration fails closed; changes are audited; existing items are unaffected.
- Status: **PARTIAL** — catalogue, workflow, approval, SLA, communication, operational-metadata administration and feature-switch source/API/UI exist; PostgreSQL persistence and audit durability remain unverified, and use-case profiles remain undefined.

### FR-029 — Cross-department work
Tickets and tasks can be raised for, assigned to, or routed to another department, and approvals can be requested from other departments.
- The requester can target another department or queue; that department's Manager (or a policy) triages and assigns within the department (FR-021).
- Visibility: each side sees only what scope allows; no broad cross-department access by default (FR-009). Cross-department visibility rules are TBD.
- Cross-department work requires a permission or policy. Whether the requester's Manager must approve before submission is policy-driven (FR-027).
- The requester is notified of progress. Which team's performance metrics count the item is TBD (FR-022).
- Acceptance: cross-department create/route succeeds when permitted and is denied otherwise; scope matrix tests for both sides; audit evidence; performance attribution per decision.
- Status: **NOT STARTED**.

### FR-030 — Progress tracking, workload monitoring and dashboards
The system shall support monitoring and detailed management of work through progress updates, due dates, sub-tasks/checklists, an activity timeline and dashboards.
- **Progress:** status plus a progress indicator (percentage or checklist completion; representation TBD), sub-tasks/checklist, due dates, and a timeline of updates, comments and assignments.
- **Dashboards:** a User sees their own open, pending, overdue and in-progress work; a Manager sees each subordinate's items and workload; a Manager Admin sees the grouped Managers' teams; Spectator sees everything read-only. Whether Users also see team-level data is TBD (§4.4).
- **Manager actions (within scope):** reassign, reprioritize, change due date, send a reminder, request an update, set or resume Pending (FR-026), request approval (FR-025). Each is audited.
- **Views:** open, overdue, stalled (no update in N business days; N is configurable, default 3), pending by waiting-on, and workload per person.
- Effort/time tracking is outside the current milestones (Appendix B).
- Dashboards refresh live through FR-024.
- This covers operational monitoring; FR-022 covers performance analytics. Both should reuse the same queries.
- Acceptance: scope matrix tests; counts reconcile with persisted data; manager actions are authorized and audited; disabled modules are respected.
- Status: **PARTIAL** — dashboard and KPI source exists; stalled, waiting and workload views are not verified.

### FR-031 — In-app Message Center
The system shall provide a Message Center inside the application, in the top bar, where changes relevant to a user are shown and notified live, like push notifications, with a saved notification history.
- Origin: Owner (message center, push-style alerts, saved history, configurable); detail marked Proposed.
- **Entry point:** a top-bar bell with an unread count opens a panel of recent notifications; a full history page offers filters (type, work item, read state, date) and search.
- **Sources (candidate list, TBD):** assignment/reassignment, status change, comment, approval requested/decided (FR-025), Pending set/released (FR-026), SLA warning/breach/escalation (FR-011), mention, announcement (FR-038), chat message (FR-032), and role/module changes affecting the user.
- **Live delivery:** an online user sees new items immediately as a push-style toast/banner through FR-024; an offline user sees them at next login. Browser push is an optional later channel and mobile push is deferred (DD-08).
- **History:** every notification is stored per recipient with timestamp, source work item, deep link and state (unread, read, archived). A user archiving or dismissing an item hides it from their view but does not destroy the retained record. Default (DD-08, DD-16): notification history is kept indefinitely; archiving only hides it.
- **Actions:** mark read/unread, mark all read, open the linked item, archive. Unread counts stay consistent across tabs and devices.
- **Preferences:** per user, per category and per channel (in-app, email, WhatsApp, push). Administrators set defaults and **mandatory categories** that users cannot disable (default list: approval requests, assignments to me, assignments of my team members by another Manager, SLA breach and escalation). Quiet hours and digests are deferred.
- **Quality:** repeated changes to the same item can be grouped; retries must not create duplicate notifications (idempotency).
- **Security:** only the recipient can read a notification. Payloads are minimal and carry no secrets. Opening a notification re-checks authorization and fails closed if access has been lost (FR-009).
- **Configurable:** a feature switch controls it (FR-019). Disabling stops new in-app notifications and hides the UI; history is preserved.
- Acceptance: a notification is created only after the producing transaction commits; it appears live and again after reload; read state persists; preferences are honored; mandatory categories cannot be disabled; an unauthorized deep link is denied; a disabled feature is hidden; counts stay consistent; no duplicates after retry.
- Status: **NOT STARTED** — notification tables and orchestration exist; no inbox API or UI found.

### FR-032 — Chat
The system shall provide chat inside the application for quick conversation around work, with all conversations and attachments saved.
- Origin: Owner (chat, saved history, attachments, configurable); detail marked Proposed.
- **Chat versus comments:** comments are part of the formal work-item record; chat is conversational. A chat message becomes a comment or evidence only by explicit action (TBD).
- **Conversation types:** work-item chat (participants such as requester, assignee, manager and approvers), direct 1:1 chat, and group/team channels. Baseline (DD-09): work-item chat and direct chat; group channels are deferred.
- **Persistence:** every message is stored with sender, timestamp, conversation and work-item link. Edits keep the earlier versions. Deletion is a soft-delete that preserves the retained record per policy. Read receipts and search are supported. Typing and presence indicators are optional and ephemeral.
- **Real-time:** messages are sent through the REST API (authorization, validation, persistence) and delivered by push through FR-024.
- **Attachments:** files attach to messages through the FR-010 pipeline (authorization, SHA-256 integrity, size and type limits, scanning). In a work-item chat they can be linked to the work item as supporting evidence when configured (TBD).
- **Mentions:** an @mention creates a Message Center notification (FR-031).
- **Access:** only participants can read or post. `SPECTATOR` is read-only and cannot post. Default (DD-09): no one outside the conversation can read chat content; Super Admin may do so only through a dedicated audited permission (`CHAT_AUDIT_VIEW`) with a recorded reason. A user who loses access to a work item loses access to its chat going forward.
- **Limits:** message size, rate and attachment limits are configurable; safe defaults are chosen at implementation and recorded.
- **Configurable:** feature switch per scope (FR-019). Disabling preserves history.
- Acceptance: messages persist and reload; non-participants are denied; Spectator cannot post; edit/delete history is retained; attachments are authorized and hash-verified; delivery works across two API instances; mentions notify; a disabled feature is hidden and history is retained.
- Status: **NOT STARTED**.

### FR-033 — Service catalogue and request intake
The system shall provide a configurable service → category → request-type hierarchy and a structured intake form for raising each type of work.
- Origin: Intent.
- Dynamic/custom fields can be configured per service or work-item type, with type-specific required fields and validation enforced server-side.
- Priority and status management; relationships and links between related work items.
- Internal submission by authorized users, including OPS or other designated intake users raising work on behalf of someone (the requester and the submitter are both recorded).
- New services, categories and types can be added by configuration without rebuilding the application.
- The frontend renders forms from the backend catalogue (FR-014).
- Acceptance: a new service, category or type added by configuration is available without a deploy; required fields are enforced on the server; on-behalf-of submission records both identities; invalid combinations are rejected; links are validated.
- Status: **PARTIAL** — catalogue, sub-category and request-type administration exist in source; intake-form rendering and on-behalf-of submission are not verified.

### FR-034 — Workflow definitions, versioning and handoffs
The system shall support configurable workflow definitions with versioning, including sequential, parallel and conditional execution, approval steps, assignment/reassignment and handoffs between departments.
- Origin: Intent.
- Transition validation prevents users from bypassing required steps. A status update alone is not accepted as evidence of a valid transition (FR-003).
- Workflow execution records are persisted and inspectable (FR-004). Approval steps and decision capture use FR-025; department handoffs use FR-029.
- Running instances stay on the version they started with unless explicitly migrated (TBD).
- Closure and completion rules follow the approved lifecycle specification.
- Acceptance: sequential, parallel and conditional definitions execute correctly; a new version does not alter running instances; invalid or bypassing transitions are denied; execution history is persisted and inspectable.
- Status: **PARTIAL** — workflow versions, parallel/conditional execution and publication exist in source; provider acceptance is BLOCKED.

### FR-035 — Work-item identifiers
The system shall keep three distinct identifiers for a work item and shall not silently replace one with another.
- Origin: Intent; decided by the owner's delegation ("use a decisive one", DD-04).
- **Internal key:** the numeric primary key from the ticket sequence. It is never the business identifier.
- **Human-readable ticket number:** stays `{REQ|INC|PRB|TSK|CHG}-{sequence}` with at least six digits (for example `REQ-000123`), unique, never reused, stable after creation, and widening naturally beyond six digits. The earlier idea of a short uppercase code excluding `0` and `O` is retired for ticket numbers.
- **Public work-item ID:** 16 characters from the alphabet `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (no `0`, `O`, `1`, `I`), unique, used by the public API. The ambiguity rule is satisfied here.
- Acceptance: generation formats are unchanged; lookup works by ticket number and by public ID; uniqueness and format validation hold; numbers beyond 999999 do not collide.
- Status: **PARTIAL** — generation and uniqueness exist in source; lookup and validation tests are unverified (repository inspection).

### FR-036 — Business calendars and SLA pause/resume
The system shall support business calendars, working hours and holidays, and SLA pause/resume where the workflow permits it.
- Origin: Intent.
- Time zone: IST is the intended business time zone (NFR-009).
- SLA rules are administrator-configured, never hard-coded, and may vary by service, category, priority or other approved criteria. Deadlines, breaches and response/resolution metrics are tracked.
- Waiting (FR-026) pauses the SLA clock only when its reason is flagged as pausing (DD-12). Calculation, pause, reset and escalation thresholds must be explicitly defined and tested.
- Acceptance: due-time calculation across working hours and holidays; pause/resume boundaries; breach and escalation transitions; time-zone behavior; persistence and worker behavior.
- Status: **PARTIAL** — calendar, holiday and SLA-pause tables and SLA administration exist; calculation rules are not verified.

### FR-037 — Single sign-on and multifactor authentication
The system shall support single sign-on (planned) and configurable TOTP-based multifactor authentication (discussed), in addition to the current JWT-based API authentication (FR-007).
- Origin: Intent.
- SSO protocol and identity provider, MFA enforcement per role or user group, enrollment, recovery and administrator reset are TBD. MFA and SSO changes are audited; secrets are never logged; failures fail closed.
- Acceptance: SSO login maps to the correct local identity and roles; MFA is enforced where configured; recovery and reset follow policy; negative paths (invalid code, replay, expired session) are tested.
- Status: **NOT STARTED** — post-V1 (milestone M4). The repository documents OIDC as a future optional add-on; local password authentication is the current method.

### FR-038 — Announcements and operational notices
The system shall let authorized administrators publish announcements and operational notices to users, delivered through the Message Center (FR-031) and other enabled channels.
- Origin: Intent.
- Administrative management of notice content and distribution is audited.
- Default (DD-08): targeting uses the existing target types (department, team, user, client) or everyone; scheduled publish and expiry; optional acknowledgement per notice.
- Acceptance: targeting is enforced; scheduled and expired notices behave correctly; acknowledgement is recorded where required; audit evidence exists.
- Status: **NOT STARTED** — baseline announcement tables exist; no API or UI found.

### FR-039 — Incident, problem and change management
The system shall support the type-specific management of incidents, problems, changes and tasks.
- Origin: Intent.
- **Incident:** creation, assignment, lifecycle and resolution, created atomically with its underlying ticket and history (FR-005).
- **Problem:** investigation of underlying causes, relationships to incidents and documented findings.
- **Change:** controlled workflow, impact assessment, approval and implementation tracking.
- **Task:** linkable to a larger item or workflow.
- Related work is linked rather than duplicating context across records. Type-specific fields and mandatory transitions are governed by the approved workflow definitions (FR-034) and the transition matrix (FR-003).
- Acceptance: each type's lifecycle and links are tested; incident atomicity is proven on the provider (FR-005); change approval and implementation tracking follow policy.
- Status: **PARTIAL** — incident, problem and change foundations (migrations 014 to 016) and controllers exist in source; provider acceptance for Incident is BLOCKED.

### FR-040 — Audit, history preservation and evidence export
The system shall preserve complete, attributable history and support audit and evidence export.
- Origin: Intent.
- **Recorded history:** work-item lifecycle, assignment history, workflow execution and transition records, approval and decision history, security events, configuration-change audit, attachment integrity evidence, and KPI/event records. Notification and chat history are included in scope subject to NFR-015.
- **Preservation:** relevant records are retained rather than silently deleted. The scope, exceptions and retention periods of the no-delete policy are TBD.
- **Integrity:** audit records are append-only. The boundaries of Super Admin's full control over audit records are TBD (§10).
- **Export:** audit packages and evidence exports are authorized, scoped and themselves audited.
- Relationship to FR-006: this extends the existing audit contract across the new modules.
- Acceptance: each listed history type is persisted and queryable by authorized users; audit records cannot be altered through the product; exports are authorized, scoped and audited; retention behavior matches the approved policy.
- Status: **PARTIAL** — hash-chained audit, security-event and audit-package tables exist; extension to messages and chat is not started.

## 6. Non-functional requirements

### NFR-001 Security by default
Fail closed on missing/ambiguous authorization facts. Enforce least privilege, scope, ownership, secure attachment access, secret redaction and protected-target restrictions.

### NFR-002 Auditability
Important changes and operational decisions must have attributable evidence. Do not fabricate audit entries, approvals, migration ledger rows or historical facts.

### NFR-003 Data integrity
Use transactions and concurrency controls where required. Do not silently retry uncertain commits. Preserve consistency among ticket, subtype, workflow and audit records.

### NFR-004 Maintainability
Retain .NET 8 Clean Architecture boundaries and Angular standalone structure. Prefer narrow, tested changes; avoid unnecessary rewrites and duplicate implementations.

### NFR-005 Compatibility
Preserve existing business logic and observable output unless a requirement explicitly authorizes behavior change. Make API/DB contract changes explicit and traceable.

### NFR-006 Observability and privacy
Provide useful diagnostics without logging passwords, tokens, secrets or unnecessary sensitive payloads.

### NFR-007 Reproducibility
Tests, migrations and acceptance evidence must identify exact commands, target class, artifact/hash and outcome, without exposing credentials.

### NFR-008 Performance
Avoid unnecessary database round trips, repeated materialization and avoidable work, but only optimize after establishing output/behavior equivalence with regression tests. No performance target has been formally supplied in the source baseline; quantify targets before claiming performance acceptance.

### NFR-009 Time and localization
Time-zone, timestamp, date-effective and expiry behavior must be explicit and consistently tested. Exact business rules remain owner-approved contract items where not specified.

### NFR-010 Recovery
Unknown migration history or uncertain transaction outcomes remain `UNKNOWN` until reconciled with trustworthy evidence. Fail closed; do not infer history from object shape.

### NFR-011 Configurability
Modules, roles, permissions and scope relationships are data-driven. Avoid hard-coded role names or module checks in business logic; new modules/roles/permissions must be addable without code changes where practical.

### NFR-012 Scalability and availability
The API tier shall be stateless so it can scale horizontally, and the real-time layer shall support multi-instance delivery (FR-024). Availability, concurrent-connection and publish-to-delivery latency targets are TBD; per NFR-008, no performance or capacity claim is made until targets and comparable measurements exist.

### NFR-013 Explainability of automation
Every automated assignment, escalation or approval-routing decision must be traceable to the policy version and the inputs that triggered it, and must be visible in the audit trail.

### NFR-014 Standards alignment
The product shall be designed to support an organization's alignment with recognized standards. Candidate standards for the owner to confirm: ISO/IEC 27001 (information security management) and ISO/IEC 20000-1 (IT service management); others are TBD. This is a design intent only. No certification or compliance is claimed, and none can be claimed without independent verification. Appendix C maps indicative themes to requirements; the security/compliance owner verifies and completes the control mapping.

### NFR-015 Confidentiality of communications and personal data
Notifications, chat messages and their attachments are treated as confidential. Data is encrypted in transit (encryption at rest is TBD). Access is limited to participants and recipients. Administrator or auditor access to message content is allowed only through an explicit, audited process whose rules are TBD. External channels (email, WhatsApp, push) carry minimal content, with no sensitive ticket data unless the owner approves it. Retention and legal-hold rules are TBD.

### NFR-016 Business continuity (Proposed)
Backups and restores cover the database and stored files, including attachments, chat and notification history. Recovery point and recovery time objectives are TBD. Restore is tested and evidenced as part of release readiness (FR-018).

## 7. Data, database and migration constraints

- **Database policy (DD-06):** PostgreSQL is the only supported database and the integration-validation provider. New schema artifacts are PostgreSQL migrations in `database/migrations/postgresql/` (next free number above 038; verify). Existing Oracle files stay untouched and unmaintained. If Oracle is ever needed, the PostgreSQL scripts are translated and only one database is active at a time.
- Never access `pa_tickets_test` or `pa_tickets_h_verify`; never use `pa_tickets_test_user`.
- Never run `tests/scripts/provision-postgresql-schema.ps1`.
- Do not modify `.env.test.local` or `.vscode/mcp.json`.
- Do not execute or modify migration 026; it has a separate approval chain.
- No database connection, SQL, migration, schema/data change, seed, cleanup, sequence or privilege change without explicit authorization for the exact target and operation.
- Never infer applied migration history from schema shape. Never fabricate a migration ledger row.
- Migration 014's reviewed historical Git-blob bytes have recorded SHA-256 `08E227BBF540BA3ECA41979A0EB2842A21605373497749A80D568D41A3EB20C3`; the working-copy bytes previously differed. Use only the exact approved artifact bytes for any separately authorized operation.

- **Naming and conventions (intent document):** database objects use the `PA_TICKETS` naming family; the main work-item table is `PA_TICKETS`; supporting objects follow `PA_TICKETS_*`. Fields use `S_` for strings, `N_` for numbers and `D_` for dates/timestamps. IST is the intended business time zone. The platform serves one internal organization and has no `TENANT_ID`.
- **E2E target (DD-07):** the owner states that a dedicated E2E credential was created and produced partial results. The target is `localhost:5432` / `pa_tickets_codex_e2e` / role `pa_tickets_codex`. Recorded results: target identity verified; read-only catalog found 43 tables with unknown provenance and empty RBAC tables; one repository persistence test passed; the lifecycle persistence test failed closed because the permission query references the absent `S_MODULE_CODE` column (the permission-metadata migration is not applied). Further operations need explicit written authorization with exact SQL, expected effects, recovery and cleanup boundary (SOP §7.2). No credentials are recorded.
- **PostgreSQL schema sources:** the PostgreSQL foundation lives in `tests/scripts/postgresql-*.sql` (data-bearing adapters that need a DDL/data split) plus `database/migrations/postgresql/028` to `038`. The reviewed build order is: approved baseline → 028 and 029 → ledger and RBAC 033 → 034 → 035 → 036 → 037. Drafts 030 to 032 are superseded; root 027 has no PostgreSQL counterpart and needs separate review; 038 stays blocked; migration 026 is excluded.
- **Conceptual data model:** the originally discussed model covers users and access control, catalogue configuration, custom fields, work-item relationships, workflow versions and transitions, approvals, business calendars, SLA pauses, comments, attachments, incident/problem/change records, notifications, KPIs, audit events, configuration-change audit, indexes and seed data. This version adds chat, notification history and preferences, and feature settings. Not every object is approved or deployed; constraints, foreign keys, identity generation, immutability, indexing and migration history require verification.

## 8. Status vocabulary

- `PASS`: required behavior and appropriate evidence exist at the stated layer.
- `PARTIAL`: some behavior/evidence exists, but acceptance is incomplete.
- `BLOCKED`: required dependency or authorization prevents the next validation.
- `NOT STARTED`: work has not begun.
- `NOT VERIFIED`: no sufficient evidence supports a conclusion.
- `UNKNOWN`: the underlying state cannot safely be inferred.

A source-level PASS is not a provider/browser/release PASS. State the layer for every claim.

## 9. Requirements traceability

Every FR must map to:
1. source path(s);
2. test(s);
3. slice ID(s);
4. current status;
5. evidence reference;
6. known gap and next acceptance criterion.

Use `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md` as an existing evidence input and update it only from verified source/test results.

## 10. Decisions and open questions

### 10.1 Decision and working-assumption register (v0.7)
**Owner** = direction explicitly stated by the owner in the available project context. **Default** = a proposed working assumption introduced to make safe, reversible progress; it is not an owner approval. A default may guide isolated, reversible source work only when it does not affect security, externally visible contracts, destructive behavior, production data or gated database operations. Before relying on a default for a consequential or hard-to-reverse behavior, record the owner decision. Changing an implemented default requires a traceable change and regression tests.

| ID | Decision / working assumption | Provenance / authority |
|---|---|---|
| DD-01 | Role codes stay `USER`, `ADMIN`, `ADMIN_HEAD`, `SUPER_ADMIN`, `SPECTATOR`. Manager and Manager Admin are display labels for `ADMIN` and `ADMIN_HEAD`. Fixed: `USER`, `ADMIN`, `SUPER_ADMIN` (core locked, additive grants allowed in ceilings). Configurable: `ADMIN_HEAD`, `SPECTATOR` (visibility only for Spectator) | Role-code direction: owner-stated; detailed fixed/configurable semantics: working default; confirm against canonical decision pack |
| DD-02 | Hierarchy comes from explicit, effective-dated relations (team membership, Manager scope, Manager Admin delegation). The reporting-manager field is informational only | Hierarchy: owner-stated; source-of-truth detail: working default; confirm canonical decision pack |
| DD-03 | Assignment and delegation authority is configurable data; the Manager of a cross-team assignee is notified; seeded rules R1 to R4 (FR-021) | Assignment configurable: owner-stated; seeded rules R1–R4: working default |
| DD-04 | Ticket number stays `{TYPE}-{sequence, min 6 digits}`; public ID stays 16 characters; internal key stays numeric (FR-035) | Owner-stated direction in FRD; verify canonical decision record |
| DD-05 | One policy engine on the existing typed model, with ADR-020; separate from assignment rules (FR-027) | Working default; not formal owner approval |
| DD-06 | PostgreSQL is the only database; Oracle is out of scope; a future Oracle move is a translation of the PostgreSQL scripts | Owner-stated direction; formal record may still be required for gated operations |
| DD-07 | E2E target `pa_tickets_codex_e2e` / `pa_tickets_codex` was created by the owner and produced partial results; further operations need explicit authorization | Owner-stated target identification only; not operational authorization |
| DD-08 | Message Center: history kept indefinitely; mandatory categories as listed in FR-031; browser push later; mobile push, quiet hours and digests deferred; announcements target existing target types with schedule, expiry and optional acknowledgement | Working default; not formal owner approval |
| DD-09 | Chat: work-item and direct chat only; participants-only reading; Super Admin only through the audited `CHAT_AUDIT_VIEW` permission; edits keep versions; deletion is a soft delete | Working default; not formal owner approval |
| DD-10 | Feature switches: global and department levels; disabled features are hidden and data is kept | Working default; not formal owner approval |
| DD-11 | Approvals: single, sequential, parallel-any and parallel-all; rejection needs a comment and returns the item to the assignee; requester never approves own request; timeouts remind then escalate to the approver's Manager; the approver gets ticket-scoped access for the request's lifetime | Working default; not formal owner approval |
| DD-12 | Waiting reuses `WAITING`; a reason from the waiting-reason catalogue and a waiting-on party are mandatory; each reason carries a pauses-SLA flag | Working default; not formal owner approval |
| DD-13 | Real-time starts single-instance with an abstraction for a later backplane; publishing reuses the durable job boundary | Working default; not formal owner approval |
| DD-14 | Default performance metrics: created, resolved, open backlog, overdue, SLA compliance, average resolution time, reopen rate, waiting count and age, approval turnaround, workload per person; periods day, week, month | Working default; not formal owner approval |
| DD-15 | SSO and TOTP MFA are post-V1 (milestone M4) | Working default; not formal owner approval |
| DD-16 | The application never physically deletes tickets, history, messages or notifications; purge and legal hold are deferred | Working default; not formal owner approval |
| DD-17 | Candidate standards are ISO/IEC 27001 and ISO/IEC 20000-1 (no certification claim); the owner is the compliance owner until another is named | Working default; not formal owner approval |

### 10.2 Open questions and approval boundaries
- The owner's review of the seeded default grants and rules in §4.4 and FR-021. Until confirmed, treat them as working defaults, not formal owner-approved policy.
- Client-scoped authorization relationship semantics (repository decision D06) and the other repository decisions D01 to D11, which stay authoritative in the owner decision pack.
- Whether Manager Admin may author assignment rules for their own group.
- Numeric limits (connections, message rate, payload and message size), set at implementation and recorded.
- Attachment storage policy: maximum size, allowed types, scanning, encryption and retention (FR-010).
- Encryption at rest, RPO and RTO.
- Final report and KPI formulas and access restrictions.
- Retention periods beyond the "kept indefinitely" default, and purge/legal-hold rules.
- Content limits for email, WhatsApp and push messages.
- Formal, attributable approval records for consequential decisions and for every provider/database operation where SOP/SLICES require them. This item can block provider/database acceptance even while independent source/UI work continues.

## 11. Source basis and limitations

This FRD consolidates the repository's Master Roadmap and Requirement-to-Code Traceability findings available during drafting. It is a **working baseline**, not proof that every requirement is implemented or that every owner decision is approved. Existing detailed design documents, transition matrices, API contracts and owner decisions remain authoritative for their specific subjects. Reconcile this FRD against those documents before treating it as a formally approved replacement.

Version 0.5 consolidates the owner's Project Intention and Complete Feature Scope document. That document is itself a scope review, not proof that any feature is implemented or that every earlier proposal was approved. Where it conflicts with direction given later in conversation, the later explicit owner direction is used and the difference is recorded in Appendix A.

## 12. Change record

### v0.2
Source: owner direction given in conversation on 2026-10-26; a formal approval record is TBD.
- §4 rewritten: five default roles are now `USER`, `MANAGER`, `MANAGER_ADMIN`, `SUPER_ADMIN`, `SPECTATOR` (previously `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, `SPECTATOR`); added authorization rules and a default capability matrix with explicit `TBD` cells.
- Super Admin changed from "permission-driven, not a generic bypass" to "full control", implemented as an explicit all-permission grant with full auditing.
- Oracle changed from "deferred" to a supported alternative provider (FR-023); PostgreSQL remains primary and the first validation path.
- Added FR-019 to FR-023 and NFR-011; extended FR-008 and FR-009; extended §2, §3, §7 and §10.
- FR-001 to FR-018 statuses and acceptance criteria are otherwise unchanged; new FRs start as `NOT STARTED` or `NOT VERIFIED`.
- Dependent documents needing alignment: `SLICES.md` (S3-02, S3-04, and new slices for FR-019 to FR-023), `SOP.md` §7.3 ("Oracle remains deferred"), and the role references in the roadmap and traceability matrix.

### v0.3
Source: owner direction given in conversation on 2026-10-26; a formal approval record is TBD.
- Added FR-024 (SignalR real-time live updates) and NFR-012 (scalability and availability).
- Extended §2, §3.1 and §3.2 (real-time is push-only and not a system of record), the FR-019 module candidates (Real-time Updates) and §10 open decisions.
- No existing FR or NFR statuses changed; FR-024 starts as `NOT STARTED`.
- Dependent documents needing alignment: `SLICES.md` (new slices for FR-019 to FR-024), `SOP.md` §7.3 (Oracle wording), and the roadmap and traceability matrix.

### v0.4
Source: owner direction given in conversation on 2026-10-26; a formal approval record is TBD.
- Added §2.1 (purpose and usage context), FR-025 to FR-030 and NFR-013.
- §4: `USER`, `MANAGER` and `SUPER_ADMIN` are now fixed roles; `MANAGER_ADMIN` and `SPECTATOR` are configurable defaults. Added a Configurability column, and capability-matrix rows for approvers, policy authoring and cross-department work. "View team tickets" for `USER` changed from `N` to `TBD`.
- Extended FR-019 (modules), FR-020 (role administration), FR-021, FR-022 (metrics), FR-024 (events), §3.1 and §10.
- No existing FR or NFR statuses changed; new FRs start as `NOT STARTED`.
- Dependent documents needing alignment: `SLICES.md` (new slices for FR-019 to FR-030, plus S1-02 reconciliation of Pending with the transition matrix), `SOP.md` §7.3 (Oracle wording), and the roadmap and traceability matrix.

### v0.5
Source: the owner's Project Intention and Complete Feature Scope document, and owner direction given in conversation on 2026-10-26; a formal approval record is TBD.
- Added §2.2 (intent, objectives, principles), origin tags, a work-item type table and a requirement index.
- Added FR-031 (Message Center), FR-032 (Chat), FR-033 to FR-040 (catalogue/intake, workflow definitions, identifiers, calendars, SSO/MFA, announcements, incident/problem/change specifics, audit and evidence export), and NFR-014 to NFR-016.
- Extended FR-001, FR-010, FR-011, FR-012, FR-013, FR-014, FR-019 (screen-based feature switches), FR-024 (chat and ephemeral signals), §3, §4.4, §7 and §10.
- Normalized the compound statuses of FR-006 and FR-008 from `PARTIAL/BLOCKED` to `PARTIAL`, with the blocked provider acceptance stated in the text. The meaning is unchanged.
- No other existing status changed. New FRs start as `NOT STARTED` or `NOT VERIFIED`.
- Added Appendices A (source reconciliation), B (proposed additions) and C (standards alignment themes).
- Dependent documents needing alignment: `SLICES.md` (new slices for FR-019 to FR-040), `SOP.md` §7.3 (Oracle wording) and the approved E2E target, and the roadmap and traceability matrix.

### v0.6
Source: owner decisions given in conversation on 2026-10-26 and a review of the Ticket_x repository (branch `workstream-i-lifecycle-authority`, commit `f080a33`); a formal approval record is TBD.
- Roles: the repository's codes are kept; Manager and Manager Admin are display labels for `ADMIN` and `ADMIN_HEAD` (DD-01). §4 rewritten with relations, fixed/configurable meaning and a default capability matrix.
- FR-021 rewritten: configurable assignment-authority rules, delegate mode, mandatory notification to the target's Manager (DD-02, DD-03).
- FR-023 rewritten: PostgreSQL is the only database; Oracle is out of scope (DD-06). Oracle statements removed from §2, §3 and §7.
- FR-027: single policy engine with ADR-020 (DD-05). FR-035: ticket-number format decided (DD-04). FR-037: post-V1.
- §2.3 milestones M1 to M4 added. §10 replaced by a decision register (DD-01 to DD-17) and a short list of remaining open questions. Statuses for FR-019 to FR-040 refreshed from repository inspection (not test runs). The E2E target and its partial results are recorded in §7 (DD-07).
- Defaults replaced earlier TBD sentences in FR-019, FR-022, FR-024, FR-025, FR-026, FR-030, FR-031, FR-032, FR-036 and FR-038.
- Dependent documents: `SLICES.md` v3; `SOP.md` §7.3 ("Oracle remains deferred") is now consistent and needs no change; the roadmap role wording should reference this FRD.

### v0.7

- Clarified that decision-register entries marked Default are working assumptions, not owner approvals; provenance and formal operational authorization are separate.
- Clarified that unresolved approval evidence blocks only the affected consequential/provider/database operation, not independent safe source or UI work.
- Preserved feature scope, role codes, database restrictions and historical status claims; no new implementation or test result is asserted.

## Appendix A — Source reconciliation (Project Intention document versus this FRD)

| Topic | Intent document | Resolution in this FRD |
|---|---|---|
| Role names | `USER`, `ADMIN`, `SUPER_ADMIN`, `ADMIN_HEAD`, `SPECTATOR`, described as names discussed, not final | Codes kept (DD-01). Manager and Manager Admin are display labels for `ADMIN` and `ADMIN_HEAD`. |
| Database | PostgreSQL is current; Oracle 19c is an older plan | PostgreSQL only; Oracle out of scope, with a future move being a translation of the PostgreSQL scripts (DD-06). |
| E2E database | `pa_tickets_codex_e2e` on `localhost:5432`, role `pa_tickets_codex` | Owner confirms the credential was created and produced partial results; recorded in §7 (DD-07). Further operations need explicit authorization. |
| Identifiers | Internal key, 16-character public ID and ticket number are unresolved | Decided in FR-035 (DD-04). |
| Spectator | Read-only visibility within the permitted scope | Read-only and organization-wide by default, with configurable visibility (§4.2). |
| Notification channels | In-app, email, WhatsApp, push; queue and provider adapters | FR-012 and FR-031. |
| Chat versus the real-time rule | Not addressed | Chat is sent by REST and delivered by push; only ephemeral typing/presence signals use the hub from the client (FR-024, FR-032). |
| Compliance | Audit-grade design; not ISO 27001 certified | NFR-014: alignment intent only, no certification claim. |
| Tenancy | One internal organization, no `TENANT_ID` | §3.2 and §7. |
| Current position | Stage 2 PARTIAL; provider-backed Incident atomicity tests blocked because `PA_TICKETS_INCIDENT` was missing in the approved test database | Consistent with FR-005 (BLOCKED). Owner-reported and not re-verified here. |

## Appendix B — Proposed enterprise additions (not baseline)

These were suggested for completeness. None is part of the baseline until the owner accepts it.

| Proposed item | Why it matters | Candidate home |
|---|---|---|
| Global search, saved filters/views and bulk actions | Productivity at volume | New FR |
| Watchers/followers and @mentions on comments | Keeps stakeholders informed without assignment | FR-031 / FR-032 |
| Ticket templates, recurring tasks and checklists | Repeatable work | FR-028 / FR-030 |
| Knowledge base linked to tickets | Faster resolution, service-management practice | New FR |
| Satisfaction survey on closure | Service quality measure | FR-022 |
| Integration API, webhooks and connectors (CRM, source control) | Fits CRM and software-development use | New FR |
| Data import/export and migration from existing tools | Adoption | FR-028 |
| Known-error records for problems; asset/CI links for change impact | Service-management completeness | FR-039 |
| Accessibility target (for example WCAG 2.2 AA) and localization | Enterprise usability | FR-014 |
| Data-loss prevention and content scanning for chat attachments | Confidentiality | FR-010 / FR-032 |
| Progressive web app and mobile push | Android-like push outside the browser | FR-031 |
| Retention automation and legal hold | Records management | FR-040 |

## Appendix C — Standards alignment themes (indicative)

This table is indicative only. The security/compliance owner must verify and complete the mapping, and no compliance claim follows from it.

| Theme | Supported by |
|---|---|
| Access control and least privilege | FR-008, FR-009, FR-020, NFR-001 |
| Authentication and MFA | FR-007, FR-037 |
| Logging, monitoring and audit | FR-006, FR-040, NFR-002, NFR-006 |
| Change management | FR-016, FR-034, FR-039 |
| Incident and problem management | FR-039, FR-012, FR-031 |
| Service-level management | FR-011, FR-036, FR-013, FR-022 |
| Cryptography and data protection | FR-010, NFR-015 |
| Backup and continuity | FR-018, NFR-016 |
| Records retention | FR-040 |
| Provider and supplier management (email, WhatsApp, push providers) | FR-012 |
| Secure development and evidence | NFR-007, SOP |
