Follow `SOP.md` strictly. Treat `FRD.md` as the functional requirements baseline and `SLICES.md` as the implementation and validation plan. Before starting, inspect the existing repository, roadmap, traceability matrix, relevant source code, tests and prior slice results.

Select one specific slice and identify its FR IDs, dependencies, scope and acceptance criteria. Make the smallest useful implementation change, preserve existing business logic and unrelated work, and run the relevant tests. Record actual commands and results. Update statuses only when supported by evidence.

Do not invent requirements, approvals, migration history or test results. Do not repeat documentation-only slices without new evidence. If blocked, identify the exact missing dependency and continue independent work that is safe to perform. Follow all database, migration, credential, target and Git restrictions in `SOP.md`.

Do not declare a slice, stage or release complete until its defined acceptance criteria are satisfied.