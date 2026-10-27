# S5-08 PostgreSQL baseline split ? review only

**Slice:** S5-08 ? FR-016, NFR-007 ? Status: PARTIAL (source inspection and generated review artifacts; no SQL execution).

## Scope and safety

The original adapters remain unchanged. The three `.sql` files in this directory are review drafts and each begins with a `DO` block that raises an exception before candidate statements. They are not executable deliverables. No database connection, SQL execution, migration-history inference, or baseline adoption occurred. The original foundational script contains DDL plus seed DML and sequence changes; the split does not imply that any operation is approved.

**Migration history:** `UNKNOWN`. No trustworthy deployment ledger or catalog evidence was inspected, and no applied state is inferred from repository source files.

## Exact input and output hashes

| Artifact | SHA-256 (raw bytes) | Role |
|---|---|---|
| `tests/scripts/postgresql-foundational-ddl.sql` | `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64` | Combined source adapter; baseline reviewed input |
| `tests/scripts/postgresql-schema.sql` | `2CCC5708C27C9DACDF26EE2B2E417832CEED9AC7DFC97D8354CF204C04AE41BF` | Embedded baseline source |
| `tests/scripts/postgresql-slice1-schema.sql` | `26A6F76C0B82F85A74415733FBC11EF36A8DC93FAF60B39B7EB8CF699669129B` | Overlapping profile/RBAC adapter; reviewed but not merged into split |
| `database/migrations/postgresql/028_local_password_authentication.sql` | `0D5291A9B5EC06F0146E947A7B988A87013EE68B69DE1983AB75761FAEE0B45B` | PostgreSQL authentication-column owner |
| `database/migrations/postgresql/029_user_profile_hierarchy.sql` | `9F40FB5326CB2F5FC48CD68D73636BC8B3EED5366D6EC58320207FF0F5C7FB3D` | PostgreSQL hierarchy/profile-column owner |
| `Docs/reconciliation/proposed/postgresql/S5-08_foundational_ddl_only_REVIEW.sql` | `0807A915E7E501F64EBA7F689F533F67965EF586F651AC1AF4D4D2A687FCC313` | DDL-only review extraction |
| `Docs/reconciliation/proposed/postgresql/S5-08_foundational_data_only_REVIEW.sql` | `121397D455CEBEBA124BCE8D9E15B1C1C0D85CD1276D9E751141FA662B7289DA` | Seed/data-only review extraction |
| `Docs/reconciliation/proposed/postgresql/S5-08_sequence_operations_DEFERRED_REVIEW.sql` | `721CCB7546D028C223AE803489F271314B921EE1C761704B90676162C67BC9A3` | Sequence operations isolated for separate D03 review |

## Statement split

The foundational input contains 167 top-level DDL/procedural statements, 4 top-level data statements, 3 top-level `setval` statements, and 3 transaction/search-path wrappers. The DDL review artifact contains 162 statements after the five duplicate profile-column ALTER/index statements were removed. Wrappers were omitted from the review extracts. DML inside function/trigger definitions remains part of the DDL object definition and was not moved. The 3 `setval` statements are excluded from seed data and isolated because D03 prohibits sequence resets/advances without separate decision and compatibility evidence. The data draft includes reference rows and the audit-maintenance token seed; that token operation also requires explicit security review.

## Relation ownership

All 40 `CREATE TABLE` relation definitions in the DDL review extract are owned by the baseline DDL artifact below. Other schema objects (sequences, indexes, functions, triggers, constraints and ALTER statements) in the same extract are likewise owned by that baseline artifact unless the explicit profile-column exceptions below apply.

| Relation | Owning artifact | Notes |
|---|---|---|
| `"PA_TICKETS"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_DELEGATION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_HISTORY"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_INSTANCE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_RULE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_RULE_STEP"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_APPROVAL_RULE_VERSION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_ASSIGNMENT"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_AUDIT"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_AUDIT_CHAIN_LOCK"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_AUDIT_MAINTENANCE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_AUDIT_MAINTENANCE_TOKEN"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_BACKGROUND_JOB"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_DEPARTMENT"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_EVENT"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_IDEMPOTENCY"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_PERMISSION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_PRIORITY"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_ROLE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_ROLE_PERMISSION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_SLA"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_SLA_CALENDAR"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_SLA_PAUSE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_SLA_POLICY"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_STATUS"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_STATUS_TRANSITION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_TEAM"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_USER"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_USER_CLIENT"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_USER_ROLE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WAITING_REASON"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_INSTANCE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_STEP"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_STEP_INSTANCE"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_TRANSITION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORKFLOW_VERSION"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |
| `"PA_TICKETS_WORK_ITEM"` | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline definition |

## Exclusive column ownership for overlap

| Relation / column | Sole owner | Baseline treatment |
|---|---|---|
| `PA_TICKETS_USER.S_PASSWORD_HASH` | `database/migrations/postgresql/028_local_password_authentication.sql` | Removed from baseline CREATE/ALTER statements |
| `PA_TICKETS_USER.N_PASSWORD_ROTATION_REQUIRED` | `database/migrations/postgresql/028_local_password_authentication.sql` | Removed from baseline CREATE/ALTER statements |
| `PA_TICKETS_USER.S_PHONE` | `database/migrations/postgresql/029_user_profile_hierarchy.sql` | Removed from baseline CREATE/ALTER statements |
| `PA_TICKETS_USER.N_MANAGER_USER_ID` | `database/migrations/postgresql/029_user_profile_hierarchy.sql` | Removed from baseline CREATE/ALTER statements |
| `PA_TICKETS_USER.N_EMAIL_VERIFIED` | `database/migrations/postgresql/029_user_profile_hierarchy.sql` | Removed from baseline CREATE/ALTER statements |
| `PA_TICKETS_USER` remaining columns and relation | `S5-08_foundational_ddl_only_REVIEW.sql` | Baseline owns base profile/auth relation; standalone 028/029 are additive owners for only the columns listed above |

The standalone adapters `postgresql-schema.sql` and `postgresql-slice1-schema.sql` are unchanged and are not additional build inputs in this split. Slice 1 overlaps the 028/029 columns and includes test fixture inserts; it must not be layered with the split baseline. The proposed sequence is baseline DDL ? migration 028 ? migration 029, then separately approved seed operations if authorized. This is a review order only, not migration-history evidence.

## Dependencies and unresolved review

- The baseline requires the `public` schema and foundational ticket objects referenced within its own definitions. The extraction retains source-defined relation dependencies; no catalog validation was performed.
- Existing audit-maintenance functions/triggers and their seed token need security-owner review; their definitions are included as DDL while the token row is in the separate data draft.
- The base script includes idempotent/conditional definitions. That does not prove equivalence with any live catalog.
- Column ownership was assigned from the source definitions and the standalone migrations; no schema execution or provider validation was used.

## Source-artifact recheck (2026-10-27)

Read-only checks in this task reproduced all three output SHA-256 values above. A static comparison of distinct `CREATE TABLE` names in the combined foundational input and the DDL review extract found 40 names in each, with no missing or extra names. The data-stage draft has four top-level `INSERT` statements and no `CREATE`, `ALTER`, or `DROP` statements. The only line-start `INSERT`/`DELETE` statements found in the DDL extract are inside the retained audit-maintenance function body, not standalone seed statements. The original foundational, standalone schema, and 028/029 migration files have no worktree diff; no consumer references the review artifacts outside the review evidence.

These checks validate hashes and text structure only. No PostgreSQL parser, server, database, or SQL execution was used; syntax and provider compatibility remain unverified. Migration history remains `UNKNOWN`.

D01 remains `PENDING`. The owner-decision pack's proposed baseline is the historically verified artifact at hash `4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19`, while this review split was generated from the current expanded source artifact at hash `21D7F96A02A079CB9870DAFA1F4FF24602CE6251F314F0E3C9D2E4B72B98CE64`. This review does not select or adopt either baseline; the explicit owner decision and compatibility evidence remain outstanding.
