# PA_TICKETS Standard Operating Procedure (SOP)

**Applies to:** AI agents, developers, reviewers and automation working in `G:\WSS\aw\Tickets`  
**Related documents:** `FRD.md`, `SLICES.md`, `Docs/PA_TICKETS_MASTER_ROADMAP.md`, `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md`  
**Purpose:** ensure each task follows the approved requirements, is sliced into verifiable work, preserves existing work, and never overstates evidence.

## 1. Order of authority

When beginning work, read in this order:
1. `SOP.md` — safety, process and evidence rules.
2. `FRD.md` — product requirements and open decisions.
3. `SLICES.md` — selected slice, dependencies and acceptance criteria.
4. `Docs/PA_TICKETS_MASTER_ROADMAP.md` — current stage order/status.
5. `Docs/PA_TICKETS_REQUIREMENT_TRACEABILITY.md` and relevant domain-specific documents.
6. The relevant source, tests, migrations and prior result reports.

If documents conflict, do not silently choose one. Identify the conflict, preserve the more restrictive safety boundary, and request/record an explicit owner decision. The roadmap remains the single source of truth for stage status until the owner changes that governance rule.

## 2. Mandatory task declaration

Before changing code, state:
- selected slice ID and FR IDs;
- the exact behavior/gap being addressed;
- relevant source files and tests already present;
- dependencies and gates;
- permitted and prohibited side effects;
- test commands and acceptance evidence expected.

Do not start a slice that has no traceable FR or explicit maintenance/technical-enabler rationale.

## 3. Slice execution protocol

1. **Read:** inspect the selected slice, FRD, SOP, roadmap and existing implementation.
2. **Baseline:** inspect git status/diff before editing; preserve all pre-existing changes.
3. **Trace:** locate current code and tests; do not duplicate an existing implementation.
4. **Plan:** choose the smallest useful change that advances the acceptance criterion.
5. **Implement:** change only necessary files; preserve behavior unless the FR explicitly authorizes a change.
6. **Test:** run focused tests first, then relevant broader tests/builds where practical.
7. **Review:** inspect diff, test output, security impact and regressions.
8. **Evidence:** record exact commands, counts, outcomes, limitations and artifact hashes where relevant.
9. **Status:** update FR/slice/traceability/roadmap status only from evidence.
10. **Handoff:** state what passed, what did not, what changed and the next executable slice.

A slice is complete only when its stated acceptance criteria pass or its remaining blockers are explicitly recorded. A report alone does not satisfy an implementation criterion.

## 4. No-loop / no-documentation-only cycle

- Do not create a new slice/report merely to restate an unchanged blocker.
- Before proposing a new slice, compare it with prior slice reports and explain what new evidence, code, test or dependency it advances.
- If the same gate is blocked twice with no new evidence, stop generating repeated reports. Identify the actual human/operator action required and continue only independent safe source work.
- Prefer one narrow implementation change plus regression tests over a planning-only slice.
- Do not repeat already passing test suites without a reason.
- Do not mark a slice complete solely because documentation was created.
- Maintain a single roadmap and a single traceability matrix; link evidence instead of duplicating long findings.

## 5. Code-change rules

- Preserve existing business logic and output unless the selected FR explicitly authorizes a behavior change.
- Avoid broad refactors, unrelated formatting, renames and dependency upgrades.
- Follow existing architecture, naming, error handling, dependency injection and test patterns.
- Add regression tests for behavior changes and bugs.
- Do not weaken authorization, validation, transaction boundaries, audit behavior or test guards to make tests pass.
- Do not replace real provider tests with mocks and then claim provider acceptance.
- Do not remove failing tests or narrow test selection without explaining why.
- Keep frontend/backend contracts aligned; document intentional contract changes.
- Never claim performance improvement without a comparable baseline and repeatable measurements.

## 6. Git/worktree safety

- Preserve all pre-existing tracked, untracked and ignored changes.
- Do not run `git reset`, `git clean`, stash, branch switch, commit, push, or destructive history operations unless the user explicitly requests that exact action.
- Do not overwrite unrelated files.
- Inspect diffs after changes and report any pre-existing whitespace/line-ending notices separately.
- Do not claim a file was changed in the user's local repository when only a draft/downloadable artifact was produced in ChatGPT.

## 7. Database and migration safety — mandatory

### 7.1 Protected targets and configuration
- Never access `pa_tickets_test` or `pa_tickets_h_verify`.
- Never use `pa_tickets_test_user`.
- Never run `tests/scripts/provision-postgresql-schema.ps1`.
- Do not modify `.env.test.local` or `.vscode/mcp.json`.
- Do not print credentials, tokens, passwords, secret environment values or secret-bearing connection strings.

### 7.2 Explicit authorization
A conversational “proceed” or general owner approval is not automatically complete operational authorization. Before each database operation, verify the exact target, operation, operator, grants, scope, validity/expiry, revocation, recovery boundary and evidence retention required by the canonical process.

Authorization for read-only catalog inspection does not authorize DDL/DML. Migration authorization does not automatically authorize provider-test writes/cleanup. Each scope must be explicit.

### 7.3 Migration history and artifact integrity
- Never infer applied migrations from object shape.
- Never fabricate a migration ledger row.
- Preserve `UNKNOWN` history until reconciled with trustworthy evidence.
- Use the exact reviewed migration artifact bytes/hash. Do not silently substitute a working-copy file with different bytes.
- Do not automatically retry uncertain commits. Classify uncertain outcomes as `UNKNOWN` and reconcile read-only.
- Migration 026 is explicitly excluded and must not be executed or modified.
- Oracle remains deferred unless explicitly reopened.

### 7.4 Database test execution
Only run provider tests when the exact test scope and target are authorized, the guard variables match the approved target, and the schema preconditions are verified. Cleanup must be limited to unique records created by that test and the cleanup boundary must be evidenced. Stop on target mismatch, unexpected schema, partial objects, unknown compatibility, or uncertain outcomes.

## 8. Evidence and status integrity

- Every claim must say whether it is source-level, unit-test, integration/provider, browser/E2E or release evidence.
- Use only statuses: `PASS`, `PARTIAL`, `BLOCKED`, `NOT STARTED`, `NOT VERIFIED`, `UNKNOWN`.
- `PASS` requires evidence for the stated acceptance layer. A source implementation, mock, or in-memory test does not prove PostgreSQL/provider behavior.
- Record actual test command, exit result, pass/fail/skip counts, and relevant output references.
- Never invent approvals, identity, signature, timestamps, expiry, grants, artifact hashes, test outcomes or migration history.
- If an input or requirement is missing, record it as an open decision/TBD; do not silently fill it with common practice.
- Keep secrets out of logs, reports and copied command output.

## 9. Required slice result format

Every meaningful slice result should include:
1. Slice ID/title and selected FR IDs.
2. Objective and scope.
3. Files changed and why.
4. Implementation details or investigation result.
5. Tests/commands actually run and exact outcome.
6. Acceptance criteria: PASS/PARTIAL/BLOCKED with evidence.
7. Security/data/migration impact.
8. Known limitations and unresolved decisions.
9. `git diff --check` outcome, separating pre-existing notices.
10. Next executable action — not a generic “continue investigation.”

Do not create a result report if there is no meaningful new result; summarize in the active task instead.

## 10. Definition of Done

A slice is DONE only when:
- its FR/Slice acceptance criteria are met;
- focused tests pass;
- relevant broader validation is run or explicitly justified as not run;
- diff review is complete;
- security, data and compatibility impacts are considered;
- evidence is recorded and statuses updated truthfully.

A stage is DONE only when the roadmap's stage exit criteria pass. A product is release-ready only when Stage 7 evidence is approved. Never promote stage status because a report says so.

## 11. Standard agent instruction

For every task, use this instruction:

> Follow `SOP.md` strictly. Treat `FRD.md` as the functional requirement baseline and `SLICES.md` as the execution/acceptance plan. Select one slice, name its FR IDs and acceptance criteria, inspect existing implementation first, make the smallest useful change, run and report actual tests, preserve all pre-existing work, and update status only from evidence. Do not invent missing requirements or approvals. Do not repeat documentation-only slices without new evidence. Respect all database, migration, target, credential and Git restrictions. If blocked, state the exact blocker and next executable action while continuing safe independent work.
