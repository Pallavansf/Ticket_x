# PA_TICKETS Gate T62A.2 Result

**Branch:** `workstream-i-lifecycle-authority`  
**Scope:** PostgreSQL write-access readiness by metadata only  
**Target:** `localhost:5432 / pa_tickets_e2e_verify / postgres`  
**Provider:** PostgreSQL 18.6

## Target identity and guard

The corrected `.env.test.local` connection setting was parsed without exposing
credentials. A fail-closed guard rejected the prohibited databases
`pa_tickets_test` and `pa_tickets_h_verify`, and rejected
`pa_tickets_test_user`, before the connection was opened. The guard accepted
only the approved host, port, database, and role.

The read-only identity metadata returned database `pa_tickets_e2e_verify`,
role `postgres`, loopback server `::1`, port `5432`, and PostgreSQL `18.6`.

## Read-only privilege evidence

The metadata query reported:

| Capability | Evidence |
|---|---|
| Database owner | `postgres` |
| Database allows connections | true |
| Database CONNECT | true |
| Database CREATE privilege | true |
| Database TEMP privilege | true |
| Role can login | true |
| Role superuser | true |
| Role createdb/createrole | true/true |
| Role replication/bypass-RLS | true/true |
| Public schema owner | `pg_database_owner` |
| Public schema USAGE | true |
| Public schema CREATE | true |

The account therefore has the metadata permissions normally needed for a
schema build: connect, create objects in the target schema, create sequences,
and use temporary objects. No write probe was performed.

The account is broader than minimum privilege because it is a superuser with
cluster-level capabilities. This gate did not alter ownership, revoke rights,
or create a replacement role. A database/security owner must decide whether
the separately gated build may use this account or requires a dedicated
non-superuser owner with narrowly scoped database and schema privileges.

## Existing schema state and readiness boundary

The same read-only metadata batch found 71 public catalog objects, including
`PA_TICKETS`, `PA_TICKETS_WORK_ITEM`, and `PA_TICKETS_WAITING_REASON`, all
owned by `postgres`. This contradicts an assumption that the approved database
is empty. Object presence does not prove which migrations or scripts produced
them. Before any schema operation, an owner-approved baseline/adoption decision,
catalog fingerprint, exact artifact hashes, and non-destructive equivalence
review are required.

Accordingly, privilege metadata is verified, but schema-build authorization and
safe build readiness remain separately gated by target-baseline review.

## Required privileges and gaps

For a build that owns the `public` schema, the minimum expected permissions are
CONNECT on the database, USAGE and CREATE on the target schema, and ownership
or explicit rights for any existing objects that must be altered. Sequence,
table, index, constraint, and extension operations may require additional
object or database privileges depending on the approved scripts. The current
role exceeds these requirements; no missing privilege was identified by
metadata. No inference was made from the role name alone.

The remaining gap is governance and evidence, not an observed denial:
determine whether existing objects are an approved baseline, whether the
`postgres` superuser is acceptable, and which exact operation is authorized.

## Commands and outcomes

- `git branch --show-current` and `git status --short --ignored`: branch and
  preservation captured.
- Redacted `.env.test.local` parsing: approved descriptor confirmed before
  connection; secrets were not printed.
- One `psql` metadata batch containing identity, database, role, schema,
  default-ACL, and catalog-object queries: succeeded read-only.
- No temporary object, DDL, DML, migration, seed, cleanup, privilege change,
  application test, or provider-backed test was run.
- `tests/scripts/provision-postgresql-schema.ps1` was not run.

## Preservation

The branch remained `workstream-i-lifecycle-authority`. All existing modified,
untracked, and ignored work was preserved. No reset, clean, stash, branch
switch, commit, push, configuration-file change, or database mutation was
performed. The only created file is this result document.

**GATE T62A.2 PASS — REQUIRED PRIVILEGES VERIFIED BY METADATA**
