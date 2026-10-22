# PA TICKETS Master Implementation and E2E Roadmap

Status: EVIDENCE-BASED ROADMAP - NOT AN EXECUTION AUTHORIZATION

This roadmap consolidates the repository evidence for RBAC, ticket workflow, security, communications, reporting, persistence, migration safety, and end-to-end acceptance. It does not approve a database operation, migration, provisioning action, production deployment, or provider test.

Statuses are based on inspected source, tests, and existing reports. A source implementation or unit test is not treated as provider-backed or browser acceptance evidence.

## Evidence baseline and protected boundaries

The existing E2E plan records 8 PASS, 3 PARTIAL, and 13 BLOCKED. `tests/TESTING.md` records database-independent .NET and Angular results, while PostgreSQL API/persistence/concurrency/E2E and all Oracle validation remain unexecuted in the latest continuation. No tests or builds were run for this roadmap.

`pa_tickets_h_verify` and `pa_tickets_test` are excluded. Migration 026 remains a separately gated subproject. Its authorization, target identity, lock/recovery, provider validation, and execution approvals are independent of this product roadmap. No migration runner or migration execution is authorized here.

## 1. Five-role RBAC and identity

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| RBAC-01 | USER, ADMIN, SUPER_ADMIN, ADMIN_HEAD, SPECTATOR role semantics | PARTIAL | `Docs/PA_TICKETS_FIVE_ROLE_RBAC_REVIEW.md`; `src/PA.Tickets.Infrastructure/Foundations/RoleAuthorizationPolicy.cs`; policy tests recorded 48/48 and later focused suites | Provider persistence, hosted API, and browser acceptance remain pending | Freeze role/permission matrix and add route-by-route API evidence | Yes |
| RBAC-02 | JWT validation, password rotation, account status | PARTIAL | `HostedAuthenticationAuthorizationTests.cs`, `HostedAuthenticationAuthorizationTests` record local Kestrel coverage; `SECURITY_CONFIGURATION.md` | Full API host, persistence, secret rotation, account lifecycle, and production abuse controls | Run approved isolated hosted API acceptance after target identity approval | Yes |
| RBAC-03 | Effective-dated grants, custom roles, module/action controls | PARTIAL | `EffectiveGrantAuthorizationTests.cs`, `SensitiveRbacApprovalPolicyTests.cs`, configurable RBAC design | Database date/materialization compatibility, migration review, and persistence tests | Resolve historical unknown permission dates and validate schema mapping | Yes |
| RBAC-04 | Department/team/client scoping and organization-wide Super Admin | PARTIAL | `DatabaseAuthorizationService.cs`, `OrganizationScopeAuthorization.cs`, `DatabaseAuthorizationScopeTests.cs` | Organization scope proposal workflow and provider-backed scope enforcement | Approve organization-scope workflow and test direct API object boundaries | Yes |
| RBAC-05 | Admin Head delegation, expiry, revocation, audit | PARTIAL | `PA_TICKETS_FIVE_ROLE_RBAC_REVIEW.md`; delegation services and migration notes | Persistence, concurrent revocation, and full API/browser acceptance | Add isolated provider and route-level delegation tests | Yes |
| RBAC-06 | Spectator read-only at API boundary | PARTIAL | Role policy tests and documented deny precedence | Hosted API negative tests for every mutation route and mixed-role membership | Build a route inventory and prove non-GET denial | Yes |
| RBAC-07 | Direct privilege escalation and object-scope tests | BLOCKED | Pure scope/policy tests exist; E2E plan records hosted/security gaps | API authentication, target/resource scope, concurrency, and browser evidence | Execute only on an approved isolated test target | Yes |

Policy decisions still requiring approval include legacy `ADMINISTRATOR` transition, organization-wide scope assignment, effective-date treatment of unknown historical rows, role delegation ownership, password/abuse controls, and identity verification delivery.

## 2. Ticket lifecycle and workflow

Supported domain types are `REQUEST`, `INCIDENT`, `PROBLEM`, `TASK`, and `CHANGE`, represented by `TicketType.cs`, type-specific contracts/services, and lifecycle services.

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| LIFE-01 | Create, read, assign, ownership, comments, attachments | PARTIAL | `TicketLifecycleService.cs`, `ProblemService.cs`, `IncidentService.cs`, `ChangeService.cs`, attachment services, unit tests | Hosted API, persistence, attachment authorization/download, and concurrency evidence | Complete API acceptance matrix for each ticket type and role | Yes |
| LIFE-02 | Valid lifecycle transitions and denial paths | PARTIAL | `TicketStatusTransitionPolicy.cs`, `TicketLifecycleAuthorityTests.cs`, `TicketLifecycleConfiguredTransitionTests.cs` | Full HTTP and provider-backed transition history | Add negative tests for every role/transition pair | Yes |
| LIFE-03 | REQUEST and TASK workflows | PARTIAL | Common ticket/lifecycle source and domain tests | Direct type-specific API and browser journeys are not evidenced | Add request/task endpoint and journey coverage | Yes |
| LIFE-04 | INCIDENT workflow | PARTIAL | `IncidentService.cs`, `PostgreSqlIncidentTests.cs` exists but is provider-gated | No current approved provider result | Run isolated provider acceptance after approval | Yes |
| LIFE-05 | PROBLEM workflow | PARTIAL | `ProblemService.cs`, `PostgreSqlProblemTests.cs`; idempotency and validation code present | Provider persistence, relation, and audit evidence | Add API/provider assertions for diagnosis and linkage | Yes |
| LIFE-06 | CHANGE workflow | PARTIAL | `ChangeService.cs`, `PostgreSqlChangeTests.cs`; schedule/conflict/rollback code present | Provider concurrency, approval, implementation, rollback, and browser journeys | Complete change lifecycle acceptance and conflict tests | Yes |
| LIFE-07 | Audit history and idempotency | PARTIAL | Event/audit writes and `TicketCreationIdempotencyStore.cs`; unit tests | Provider transaction atomicity and replay evidence | Validate idempotency and audit transaction behavior on isolated target | Yes |

## 3. Security and audit

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| SEC-01 | Authentication failure handling and rate limits | PARTIAL | `HostedAuthenticationAuthorizationTests.cs`; `SECURITY_CONFIGURATION.md` records five-failure throttling | Distributed limiter, trusted proxy identity, monitoring, and operational response are absent | Approve production abuse-control design and test it outside migration work | Yes |
| SEC-02 | Authorization failure and privilege escalation | PARTIAL | Role/scope policy tests and security configuration | Hosted API route coverage and object-scope concurrency remain pending | Build authenticated negative API suite | Yes |
| SEC-03 | Audit completeness and tamper evidence | PARTIAL | `AuditChainVerifier.cs`, audit tests, audit design docs | Historical actor contract, durable provider evidence, retention/custody decisions | Resolve actor model and audit retention/ownership | Yes |
| SEC-04 | Idempotency and replay | PARTIAL | Ticket/workflow idempotency services and unit tests | Provider race, replay, and failure-store evidence | Add approved provider-backed replay tests | Yes |
| SEC-05 | Attachment/download authorization | BLOCKED | Attachment source and tests exist | End-to-end ownership/scope/download and storage evidence not established | Define storage/retention policy and run isolated API/browser tests | Yes |
| SEC-06 | Secrets, password rotation, account verification | BLOCKED | Security docs explicitly state sender/verification and production secret controls are unresolved | Approved delivery adapter, rotation operations, revocation/retention evidence | Assign security owner and create acceptance contract | Yes |

## 4. SLA, escalation, and communications

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| COM-01 | Configurable SLA policies, calendars, pauses, aging | PARTIAL | `PersistentSlaConfigurationService.cs`, `SlaDeadlineMonitor.cs`, SLA unit tests, `KpiServiceTests.cs` | Provider state, calendar/DST policy, recovery and worker evidence remain open | Approve calendar semantics and test durable job transitions | Yes |
| COM-02 | Escalation and ticket aging | PARTIAL | `SlaEscalationJobHandler.cs`, deadline monitor, operational docs | Provider-backed timing, retries, duplicate prevention, and notification delivery | Add isolated worker/retry acceptance | Yes |
| COM-03 | In-app/email/WhatsApp/push delivery | BLOCKED | `NotificationOrchestrator.cs`, communication services, PostgreSQL notification tests exist | Provider adapters, credentials, delivery policy, retries, duplicate prevention, and audit are not accepted | Select approved channels and operational owners; test with non-production providers | Yes |
| COM-04 | Delivery audit and failure handling | PARTIAL | Communication delivery and administration code/tests | Durable provider evidence and retention/custody decisions | Define delivery event schema and failure/replay acceptance | Yes |

WhatsApp, email sender configuration, push credentials, and production delivery claims require separate operational approval. No external delivery was performed in this gate.

## 5. Reporting and exports

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| REP-01 | Permissioned operational reports and ticket-aging evidence | PARTIAL | `Docs/OPERATIONAL_REPORTS.md`, `ReportingQueryService.cs`, `AuditorReportingService.cs`; source report catalogue documented | Hosted authorization, KPI formula approval, and provider evidence remain pending | Complete report-by-report authorization matrix and acceptance data | Yes |
| REP-02 | Scheduled reports and durable execution | PARTIAL | `ReportExportService.cs`, durable job repositories, `EXPORT_AND_SCHEDULED_REPORTING.md` | Provider restart/retry and worker evidence are pending | Validate schedule state transitions on isolated target | Yes |
| REP-03 | CSV/XLSX output correctness and limits | PARTIAL | `ReportExportFormatter.cs`, `ReportExportFormatterTests.cs`; 10,000-row/25 MiB rules documented | Large-data provider performance and download authorization evidence | Add bounded dataset acceptance and artifact integrity checks | Yes |
| REP-04 | Artifact expiry, downloads, permission rechecks | PARTIAL | Export service and local storage lifecycle documented | Multi-instance storage, expiry worker, API/browser download evidence | Approve production storage/retention and test rechecks | Yes |
| REP-05 | Auditor reporting and audit export | BLOCKED | `AUDITOR_REPORTING.md`, report services, audit export permissions | Audit actor model, retention/custody, and hosted acceptance unresolved | Resolve audit governance before release | Yes |

## 6. Persistence and migration safety

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| PERSIST-01 | PostgreSQL schema/mappings, constraints, sequences/defaults | PARTIAL | `TicketsDbContext.cs`, `ExecutionDbContext.cs`, `Docs/SCHEMA_REVIEW.md`, provider compatibility tests | No current provider-backed continuation result; baseline and historical data decisions remain | Obtain separate target identity and run approved read-only/provider acceptance | Yes |
| PERSIST-02 | Transactions, concurrency, and rollback | PARTIAL | Transaction/audit source, provider test inventory, unit tests | Provider behavior and fault evidence are intentionally unverified | Approve provider test plan and collect evidence | Yes |
| PERSIST-03 | Oracle compatibility | BLOCKED | `OracleTicketServices.cs`, provider compatibility source | Oracle migration/locking/rollback/deployment tests intentionally unexecuted | Treat Oracle as a separate provider release gate | Yes |
| PERSIST-04 | Migration 026 candidate and runner | BLOCKED | `Docs/PA_TICKETS_MIGRATION_026_EXECUTION_LOCK_RECOVERY_CONTRACT.v6.md`, `Runner.cs`, `Program.cs` | Authorization, target identity, lock/recovery, provider validation, and execution approval absent; entry point disabled | Complete independent v6 audit and owner approvals; do not execute | No - separate gated subproject |
| PERSIST-05 | Other configurable RBAC migrations | BLOCKED | `PA_TICKETS_E2E_MASTER_PLAN.md`, migration review tables | Baseline ledger, unknown role dates, audit actors, and target approval unresolved | Resolve migration-specific decisions under protected-target rules | Yes, but migrations remain separately gated |

Migration 026 remains isolated from this roadmap. No migration, provisioning, cleanup, sequence reset, or database write is authorized.

## 7. Full API, Angular, security, and provider-backed E2E

| ID | Requirement | Status | Evidence and last verified result | Missing acceptance/dependency | Next task | Independent of migration 026 |
|---|---|---|---|---|---|---|
| E2E-01 | Unit and application tests | PASS | `tests/TESTING.md`; recorded .NET policy/proposal suites and Angular suites, including 51/51 Angular across 12 files and 50/50 proposal-policy continuation | Results are historical evidence, not a new run in this gate | Preserve focused regressions and rerun only under normal test approval | Yes |
| E2E-02 | API integration and authentication journeys | BLOCKED | Test inventory and hosted local auth tests exist | Full API host, database identity, persistence, and HTTP role journeys not accepted | Approve isolated target and execute route matrix | Yes |
| E2E-03 | Angular/browser user journeys | BLOCKED | Angular component specs and production build are recorded; no browser-to-API acceptance | Browser automation, live API, persistence, and role journeys absent | Define browser environment and execute approved journeys | Yes |
| E2E-04 | RBAC negative/security suite | BLOCKED | Database-independent policy/scope tests exist | Direct API escalation, object scope, concurrency, and browser denial evidence absent | Build and run isolated security matrix | Yes |
| E2E-05 | Provider-backed persistence/concurrency | BLOCKED | PostgreSQL test classes and scripts exist; `tests/TESTING.md` says not run in latest continuation | Approved target identity, provider evidence, concurrency/recovery traces absent | Obtain provider approval and execute only on approved isolated target | Yes |
| E2E-06 | Regression/release readiness | BLOCKED | Existing E2E plan baseline and remediation docs | 8/3/13 baseline gaps, migrations, browser, provider, and operational gates remain | Reconcile all blocked/partial rows and obtain release sign-off | Yes |
| E2E-07 | End-to-end login through ticket/reporting | BLOCKED | Source routes/components and service tests exist | No complete browser/API/provider journey evidence | Implement journey matrix across all roles and ticket types | Yes |

## Dependency and priority sequence

1. Resolve governance blockers: target identity, security ownership, audit actors, legacy role transition, organization scope, secret/delivery policy, and provider test authority.
2. Freeze route-level RBAC and ticket lifecycle acceptance matrices, including negative object-scope and Spectator tests.
3. Establish an approved isolated provider test target and execute persistence, concurrency, workflow, SLA, communication, reporting, and attachment evidence. Do not use protected targets.
4. Complete API and browser journeys across all five roles and ticket types, then reconcile the E2E baseline and release gaps.
5. Resolve report/export storage, retention, audit, and delivery operations; validate retries and permission rechecks.
6. Treat migration 026 as a separate gated project: independently audit v6, approve authorization/target identity/locking/recovery/provider evidence, and obtain explicit execution approval. Roadmap progress never authorizes it.

Dependencies are directional: governance and target identity precede provider testing; provider persistence precedes API/browser E2E; API authorization precedes browser role journeys; audit/retention policy precedes reporting/export release; all migration-026 contracts precede any migration execution.

## Work that can proceed without migration 026

Code review, route inventories, database-independent unit/application tests, Angular component tests, browser harness preparation, API contract tests with fakes, report formatter tests, security threat modeling, audit/retention policy decisions, provider test fixture design, and migration-runner contract review can proceed independently. Provider-backed work still requires its own approved isolated target and identity checks.

## Exit criteria

The product roadmap can move to release review only when every workstream row has direct evidence, all PARTIAL/BLOCKED acceptance criteria are closed or explicitly accepted by an owner, provider/API/browser evidence is recorded, security and retention decisions are approved, and migration-specific gates remain separately satisfied. Migration 026 additionally requires its independent authorization, target identity, lock/recovery, provider validation, and execution approvals.

This roadmap records repository evidence and open work. It does not claim unverified tests, provider behavior, approvals, or migration completion.
