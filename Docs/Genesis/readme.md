Follow `SOP.md` strictly. Treat `FRD.md` as the functional requirements baseline and `SLICES.md` as the implementation and validation plan. Before starting, inspect the existing repository, roadmap, traceability matrix, relevant source code, tests and prior slice results.

Select one specific slice and identify its FR IDs, dependencies, scope and acceptance criteria. Make the smallest useful implementation change, preserve existing business logic and unrelated work, and run the relevant tests. Record actual commands and results. Update statuses only when supported by evidence.

Do not invent requirements, approvals, migration history or test results. Do not repeat documentation-only slices without new evidence. If blocked, identify the exact missing dependency and continue independent work that is safe to perform. Follow all database, migration, credential, target and Git restrictions in `SOP.md`.

Do not declare a slice, stage or release complete until its defined acceptance criteria are satisfied.

**Local E2E acceleration (owner-adopted 2026-10-29):** See the exact policy body in `LOCAL_E2E_POLICY_PROPOSED.md`, SHA-256 `A171054FB8440B06B3A15EE5242A6DF6402C31647FC128F0B8007924A24798B8`, and its adoption entry in the canonical owner decision pack. Its standing fixed-target permissions apply only to the dedicated local E2E database and bounded provider tests after their preconditions are checked. Actual migration execution still requires explicit owner approval of one reviewed batch. Protect all nonlocal environments and product-level RBAC/audit behavior.
