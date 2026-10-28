-- PA_TICKETS one-time privilege preflight proposal; NOT AUTHORIZED or executed.
-- Target: [::1]:5432 / pa_tickets_codex_e2e / pa_tickets_codex.
-- This artifact is separate from the S5-03 schema fingerprint artifact.
-- It contains only a read-only transaction, a non-mutating target assertion,
-- and SELECTs over PostgreSQL catalog views and privilege inquiry functions.
-- It does not create/alter/drop objects, change grants, advance sequences,
-- or read/write application rows.

BEGIN;
SET TRANSACTION READ ONLY;

-- Fail before privilege catalog queries on an unexpected session.
DO $target_guard$
BEGIN
  IF current_database() IS DISTINCT FROM 'pa_tickets_codex_e2e'
     OR current_user IS DISTINCT FROM 'pa_tickets_codex'
     OR inet_server_addr() IS DISTINCT FROM '::1'::inet
     OR inet_server_port() IS DISTINCT FROM 5432
     OR current_setting('transaction_read_only') IS DISTINCT FROM 'on' THEN
    RAISE EXCEPTION 'Privilege preflight target/session check failed';
  END IF;
END
$target_guard$;

-- Target and current role attributes.
SELECT current_database() AS database_name,
       current_user AS role_name,
       inet_server_addr()::text AS server_address,
       inet_server_port() AS server_port,
       current_setting('transaction_read_only') AS transaction_read_only;

SELECT r.rolname,
       r.rolsuper,
       r.rolcreatedb,
       r.rolcreaterole,
       r.rolreplication,
       r.rolbypassrls,
       r.rolinherit,
       r.rolcanlogin
FROM pg_catalog.pg_roles AS r
WHERE r.rolname = current_user;

-- Every direct/transitive membership path and PostgreSQL's effective USAGE
-- membership test. Membership facts are reported; they are not inferred.
WITH RECURSIVE membership_paths(member_oid, role_oid, depth, oid_path) AS (
    SELECT m.member, m.roleid, 1, ARRAY[m.member, m.roleid]::oid[]
    FROM pg_catalog.pg_auth_members AS m
    JOIN pg_catalog.pg_roles AS current_role ON current_role.oid = m.member
    WHERE current_role.rolname = current_user
  UNION ALL
    SELECT m.member, m.roleid, p.depth + 1, p.oid_path || m.roleid
    FROM membership_paths AS p
    JOIN pg_catalog.pg_auth_members AS m ON m.member = p.role_oid
    WHERE NOT m.roleid = ANY (p.oid_path)
)
SELECT member_role.rolname AS member_role,
       granted_role.rolname AS granted_role,
       p.depth,
       m.admin_option,
       m.inherit_option,
       m.set_option,
       granted_role.rolsuper AS granted_role_is_superuser,
       granted_role.rolcreatedb AS granted_role_can_create_database,
       granted_role.rolcreaterole AS granted_role_can_create_roles,
       granted_role.rolreplication AS granted_role_can_replicate,
       granted_role.rolbypassrls AS granted_role_can_bypass_rls,
       pg_catalog.pg_has_role(current_user, granted_role.oid, 'USAGE') AS privileges_inherited_by_current_role
FROM membership_paths AS p
JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = p.member_oid
JOIN pg_catalog.pg_roles AS granted_role ON granted_role.oid = p.role_oid
JOIN pg_catalog.pg_auth_members AS m ON m.member = p.member_oid AND m.roleid = p.role_oid
ORDER BY p.depth, granted_role.rolname;

-- Database-level CONNECT and CREATE capabilities for the current role.
SELECT d.datname AS database_name,
       pg_catalog.has_database_privilege(current_user, d.oid, 'CONNECT') AS can_connect,
       pg_catalog.has_database_privilege(current_user, d.oid, 'CREATE') AS can_create_schema_or_objects,
       pg_catalog.has_database_privilege(current_user, d.oid, 'TEMP') AS can_create_temporary_objects
FROM pg_catalog.pg_database AS d
WHERE d.datname = current_database();

-- Effective USAGE/CREATE on every non-system schema.
SELECT n.nspname AS schema_name,
       pg_catalog.pg_get_userbyid(n.nspowner) AS schema_owner,
       pg_catalog.has_schema_privilege(current_user, n.oid, 'USAGE') AS can_use_schema,
       pg_catalog.has_schema_privilege(current_user, n.oid, 'CREATE') AS can_create_in_schema
FROM pg_catalog.pg_namespace AS n
WHERE n.nspname <> 'information_schema'
  AND n.nspname !~ '^pg_'
ORDER BY n.nspname;

-- Effective rights on every non-system table-like relation. Owner rights,
-- direct grants, PUBLIC grants and inherited role grants are reflected by
-- PostgreSQL's has_table_privilege() inquiry function.
SELECT n.nspname AS schema_name,
       c.relname AS relation_name,
       c.relkind,
       pg_catalog.pg_get_userbyid(c.relowner) AS relation_owner,
       pg_catalog.has_table_privilege(current_user, c.oid, 'SELECT') AS can_select,
       pg_catalog.has_table_privilege(current_user, c.oid, 'INSERT') AS can_insert,
       pg_catalog.has_table_privilege(current_user, c.oid, 'UPDATE') AS can_update,
       pg_catalog.has_table_privilege(current_user, c.oid, 'DELETE') AS can_delete,
       pg_catalog.has_table_privilege(current_user, c.oid, 'TRUNCATE') AS can_truncate
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f')
  AND n.nspname <> 'information_schema'
  AND n.nspname !~ '^pg_'
ORDER BY n.nspname, c.relname;

-- Column-level grants can permit SELECT/INSERT/UPDATE even when the relation
-- level privilege is absent, so report those effective rights separately.
SELECT n.nspname AS schema_name,
       c.relname AS relation_name,
       a.attname AS column_name,
       pg_catalog.has_column_privilege(current_user, c.oid, a.attnum, 'SELECT') AS can_select_column,
       pg_catalog.has_column_privilege(current_user, c.oid, a.attnum, 'INSERT') AS can_insert_column,
       pg_catalog.has_column_privilege(current_user, c.oid, a.attnum, 'UPDATE') AS can_update_column
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_attribute AS a ON a.attrelid = c.oid
WHERE c.relkind IN ('r', 'p', 'v', 'f')
  AND a.attnum > 0
  AND NOT a.attisdropped
  AND n.nspname <> 'information_schema'
  AND n.nspname !~ '^pg_'
ORDER BY n.nspname, c.relname, a.attnum;

-- Effective USAGE and UPDATE rights on every non-system sequence.
SELECT n.nspname AS schema_name,
       c.relname AS sequence_name,
       pg_catalog.pg_get_userbyid(c.relowner) AS sequence_owner,
       pg_catalog.has_sequence_privilege(current_user, c.oid, 'USAGE') AS can_use_sequence,
       pg_catalog.has_sequence_privilege(current_user, c.oid, 'UPDATE') AS can_update_sequence
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
WHERE c.relkind = 'S'
  AND n.nspname <> 'information_schema'
  AND n.nspname !~ '^pg_'
ORDER BY n.nspname, c.relname;

SELECT 'PRIVILEGE_PREFLIGHT_COMPLETE' AS preflight_status,
       current_database() AS database_name,
       current_user AS role_name,
       inet_server_addr()::text AS server_address,
       inet_server_port() AS server_port,
       current_setting('transaction_read_only') AS transaction_read_only;

ROLLBACK;
