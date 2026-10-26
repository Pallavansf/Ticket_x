# PA_TICKETS Stage 2 Foundational DDL Baseline Forensic Comparison

**Repository:** `G:\WSS\aw\Tickets`
**Branch:** `workstream-i-lifecycle-authority`
**Purpose:** source-only evidence for D01; no baseline is adopted by this report.

## Artifact identity and byte hashes

The working artifact is `tests/scripts/postgresql-foundational-ddl.sql` in the current worktree. Hashing its raw bytes with SHA-256 produced:

`21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`

The historical artifact was retrieved directly with `git cat-file blob` from:

`1bcc493d267e23e3e126b9b698fa4344ebe8588a:tests/scripts/postgresql-foundational-ddl.sql`

Its raw bytes produced:

`4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19`

The Git object ID for that historical path is `aaa63d138d845472d405d4411bf36b9e8284201e` (Git's object hash, not the recorded SHA-256 artifact hash). Commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a` is authored and committed on 2026-10-22 with message `Publish PA Tickets application baseline`; its existence is provenance evidence only and is not owner approval.

The repeated `5CAA69711E3804C301F04ADEF049CEE33830F937B7F5DAF8BD3BEF796D5537F7` value has no identifiable byte artifact or verified provenance in the reviewed repository evidence.

The two verified hashes use the same SHA-256 algorithm over raw file bytes. The line-oriented Git diff is not used as a hash input. The working file is 42,379 bytes/749 lines; the historical blob is 33,519 bytes/643 lines.

## Deterministic comparison

`git diff --unified=0 1bcc493d267e23e3e126b9b698fa4344ebe8588a -- tests/scripts/postgresql-foundational-ddl.sql` reports **106 added lines and zero removed lines**. All historical statements are retained byte-for-byte in order; no formatting-only change or replacement of an existing statement was observed. The additions are semantic and are grouped below.

| Current lines | Historical anchor | Semantic addition | Compatibility/risk visible from source |
|---|---|---|---|
| 49–52 | after historical line 48 | `PA_TICKETS.N_REQUEST_TYPE_VERSION` integer and `PA_TICKETS.N_SUB_CATEGORY_ID` bigint, both additive nullable columns. | Existing rows need no backfill by the script; EF shadow-property compatibility must be reviewed before adoption. |
| 54–120 | after historical line 48 | Status/transition/priority sequences; three metadata tables; unique constraints, indexes, canonical status/priority/transition inserts, and `setval` calls. | This is executable DDL plus DML and sequence advancement. It introduces operational metadata and fixed reference rows; it must not be treated as a read-only baseline comparison or executed without D01 and operation authorization. |
| 213–246 | after historical line 140 | Audit-maintenance token seed, revocation of public access, `pa_tickets_audit_guard` trigger function/trigger, and security-definer maintenance-delete function. | Adds security behavior and a privileged cleanup path. Function ownership, grants, `search_path`, token custody, and interaction with audit-chain guarantees require owner/provider review. |

The semantic additions are visible in the current file at the cited lines and absent from the historical file at the corresponding anchors. No table, column, constraint, index, sequence, function, trigger, grant, role, or search-path statement was removed from the historical artifact.

## Object-level differences

The historical artifact already contains the foundational ticket, work-item, waiting-reason, assignment, event, audit, workflow, approval, SLA, idempotency, background-job, and identity/RBAC object declarations that precede the added blocks. The current artifact adds:

- Three operational sequences: `PA_TICKETS_STATUS_SEQ`, `PA_TICKETS_STATUS_TRANSITION_SEQ`, and `PA_TICKETS_PRIORITY_SEQ`.
- Tables `PA_TICKETS_STATUS`, `PA_TICKETS_STATUS_TRANSITION`, and `PA_TICKETS_PRIORITY`, with primary keys, code uniqueness, transition foreign keys, and the listed defaults/check-like flags.
- Indexes `IX_PA_TICKETS_STATUS_ACTIVE_ORDER`, `IX_PA_TICKETS_TRANSITION_TO`, `IX_PA_TICKETS_PRIORITY_ACTIVE_ORDER`, and partial unique index `UX_PA_TICKETS_PRIORITY_DEFAULT`.
- Twenty-one canonical transition rows, ten status rows, and four priority rows, inserted with `ON CONFLICT DO NOTHING`.
- Three `setval(..., false)` calls whose expressions derive the next sequence value from current maxima.
- Two nullable ticket columns.
- `PA_TICKETS_AUDIT_MAINTENANCE_TOKEN` initialization and access revocation, plus a security-definer trigger guard and maintenance deletion function.

No independent `GRANT` or `CREATE ROLE` statement was added. The additions do set explicit function `search_path` values (`pg_catalog, public`) and revoke public access to maintenance objects. The file begins with `BEGIN`, sets `search_path` locally to `public, pg_catalog`, and ends with the existing transaction structure; execution order therefore matters, especially for references to tables, sequences, and audit maintenance objects.

## Provenance classification

**Direct artifact facts:** the paths, byte lengths, hashes, line ranges, statements, and zero-removal diff above were recomputed from the worktree and Git blob without executing SQL.

**Git-history facts:** the historical path is present in commit `1bcc493d267e23e3e126b9b698fa4344ebe8588a`; the current worktree file is modified relative to that commit by the additions listed above.

**Documented claims:** Slice 24/27/31/32 records the same three hash references and states that no owner-adopted baseline exists.

**Unresolved assumptions:** no reviewed evidence establishes that the current additions were applied to a live database, that their reference rows are approved, that the maintenance function's privilege model is acceptable, or that either verified artifact has been adopted by D01. The `5CAA...` source remains unknown.

## D01 decision matrix

| Artifact | Verified provenance | Semantic delta | Compatibility risk | Owner review required |
|---|---|---|---|---|
| Current worktree, SHA-256 `21D7...` | Direct raw-byte hash; modified worktree file. | Adds operational metadata, sequence set-values, nullable ticket columns, and audit-maintenance security objects. | DDL/DML, sequence advancement, fixed reference data, and privileged cleanup behavior require separate review. | Confirm exact immutable version, definitions, reference rows, sequence policy, security behavior, and adoption scope. |
| Historical Git blob, SHA-256 `4F4E...` | Exact blob at commit `1bcc493...`, path verified. | Earlier foundational object set; lacks the 106 added lines. | May not satisfy current EF/lifecycle/audit source contracts that rely on additions. | Confirm whether this historical artifact is the intended baseline and how later additions are authorized. |
| `5CAA...` | No identifiable artifact or byte source. | Unknown. | Cannot compare or reproduce. | Supply an immutable artifact and provenance, or reject this reference. |

D01 remains `PENDING`. Before adoption, the owner must select one immutable artifact/version, review a normalized expected-versus-observed definition report, reconcile sequence and reference-data implications, and record attributable identity, date, scope, rationale, and approval reference. No recommendation is made solely from recency or reproducibility.

## Validation and boundaries

The hashes and diff were recomputed from raw bytes and are reproducible. No SQL, provider connection, migration, DDL execution, DML, database test, or source DDL modification occurred. The temporary historical-byte file used during verification was removed from the repository. Existing worktree changes were preserved.
