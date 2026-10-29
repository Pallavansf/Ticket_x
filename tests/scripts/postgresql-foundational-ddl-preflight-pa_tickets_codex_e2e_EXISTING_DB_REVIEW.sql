-- PA_TICKETS PostgreSQL existing-database catalog fingerprint (review-only successor)
-- TARGET: IPv6 loopback ::1, port 5432,
-- database pa_tickets_codex_e2e, role pa_tickets_codex
-- Target identification is not operation authorization. S5-01 remains blocked;
-- do not run this artifact unless its exact bytes and operation are authorized.
-- READ-ONLY: this script starts a READ ONLY transaction and rolls it back.
-- It never creates, alters, drops, truncates, inserts, updates, deletes,
-- grants, revokes, provisions, or executes the foundational adapter.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL search_path TO public, pg_catalog;

-- Exact-target assertions. Any mismatch aborts this preflight.
DO $$
DECLARE v_major integer;
BEGIN
  IF current_database() <> 'pa_tickets_codex_e2e' THEN
    RAISE EXCEPTION 'Wrong database: expected pa_tickets_codex_e2e';
  END IF;
  IF current_database() IN ('pa_tickets_test','pa_tickets_h_verify') THEN
    RAISE EXCEPTION 'Prohibited database target';
  END IF;
  v_major := (current_setting('server_version_num')::integer / 10000);
  IF v_major <> 18 THEN
    RAISE EXCEPTION 'Wrong PostgreSQL major version: expected 18';
  END IF;
  IF inet_server_addr() IS DISTINCT FROM '::1'::inet THEN
    RAISE EXCEPTION 'Wrong server address: expected IPv6 loopback ::1; actual=%', inet_server_addr();
  END IF;
  IF inet_server_port() <> 5432 THEN
    RAISE EXCEPTION 'Wrong server port: expected 5432';
  END IF;
  IF current_user <> 'pa_tickets_codex' THEN
    RAISE EXCEPTION 'Wrong role: expected pa_tickets_codex';
  END IF;
  IF current_schema() <> 'public' THEN
    RAISE EXCEPTION 'Wrong schema context: expected public';
  END IF;
  IF current_setting('transaction_read_only') <> 'on' THEN
    RAISE EXCEPTION 'Preflight must run in a read-only transaction';
  END IF;
END $$;

-- Target fingerprint (catalog-only).
SELECT current_setting('server_version') AS server_version,
       current_database() AS database_name,
       current_user AS role_name,
       inet_server_addr() AS server_address,
       inet_server_port() AS server_port,
       current_schema() AS schema_name,
       current_setting('search_path') AS effective_search_path;

SELECT n.nspname AS schema_name, c.relname AS object_name, c.relkind,
       pg_get_userbyid(c.relowner) AS owner_name
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
ORDER BY c.relkind, c.relname;
-- Schema metadata is descriptive only: owner and ACL are reported as catalog values.
SELECT ns.nspname AS schema_name, pg_get_userbyid(ns.nspowner) AS owner_name,
       ns.nspacl::text AS schema_acl
FROM pg_catalog.pg_namespace AS ns
WHERE ns.nspname NOT LIKE 'pg_%' AND ns.nspname <> 'information_schema'
ORDER BY ns.nspname;

-- pg_catalog inventory captures all visible public relation columns and preserves
-- PostgreSQL-specific type, identity/generated, collation and expression details.
SELECT ns.nspname AS schema_name, cls.relname AS relation_name,
       att.attnum AS ordinal_position, att.attname AS column_name,
       format_type(att.atttypid, att.atttypmod) AS formatted_type,
       typ.typname AS internal_type_name, att.attnotnull AS not_null,
       pg_get_expr(def.adbin, def.adrelid, true) AS default_expression,
       att.attidentity AS identity_kind, att.attgenerated AS generated_kind,
       coll.collname AS collation_name
FROM pg_catalog.pg_attribute AS att
JOIN pg_catalog.pg_class AS cls ON cls.oid = att.attrelid
JOIN pg_catalog.pg_namespace AS ns ON ns.oid = cls.relnamespace
JOIN pg_catalog.pg_type AS typ ON typ.oid = att.atttypid
LEFT JOIN pg_catalog.pg_attrdef AS def
  ON def.adrelid = att.attrelid AND def.adnum = att.attnum
LEFT JOIN pg_catalog.pg_collation AS coll ON coll.oid = att.attcollation
WHERE ns.nspname = 'public' AND att.attnum > 0 AND NOT att.attisdropped
  AND cls.relkind IN ('r','p','v','m','f')
ORDER BY ns.nspname, cls.relname, att.attnum;

SELECT n.nspname AS schema_name, c.relname AS relation_name,
       con.conname, con.contype, pg_get_constraintdef(con.oid, true) AS definition
FROM pg_constraint con
JOIN pg_class c ON c.oid = con.conrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
ORDER BY c.relname, con.conname;

SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
ORDER BY tablename, indexname;

SELECT ns.nspname AS schema_name, seq.relname AS sequence_name,
       pg_get_userbyid(seq.relowner) AS owner_name,
       s.seqstart, s.seqincrement, s.seqmin, s.seqmax, s.seqcycle, s.seqcache,
       dep.refobjid::regclass AS owned_by_relation,
       a.attname AS owned_by_column
FROM pg_class seq
JOIN pg_namespace ns ON ns.oid = seq.relnamespace
JOIN pg_catalog.pg_sequence s ON s.seqrelid = seq.oid
LEFT JOIN pg_depend dep ON dep.classid = 'pg_class'::regclass
 AND dep.objid = seq.oid AND dep.deptype = 'a'
LEFT JOIN pg_attribute a ON a.attrelid = dep.refobjid AND a.attnum = dep.refobjsubid
WHERE seq.relkind = 'S' AND ns.nspname = 'public'
ORDER BY seq.relname;

SELECT n.nspname AS schema_name, c.relname AS relation_name,
       a.attname AS column_name, pg_get_expr(d.adbin, d.adrelid) AS default_expression
FROM pg_attrdef d
JOIN pg_class c ON c.oid = d.adrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum
WHERE n.nspname = 'public'
ORDER BY c.relname, a.attnum;

SELECT extname, extversion FROM pg_extension ORDER BY extname;

-- Migration history remains UNKNOWN: object shape and ledger presence do not prove
-- execution provenance. Do not enumerate, interpret, or emit ledger row contents.
SELECT 'UNKNOWN'::text AS migration_history_status,
       (to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') IS NOT NULL) AS ledger_object_present,
       'No independently verified migration-history evidence supplied'::text AS basis;
-- Read-only duplicate checks. A missing table is reported as NOT_APPLICABLE;
-- an existing table is checked against the exact source key/predicate.
DO $$
DECLARE r record; v_count bigint;
BEGIN
  FOR r IN SELECT * FROM (VALUES
    ('PA_TICKETS_WORK_ITEM','"N_TICKET_ID","S_WORK_ITEM_NO"'),
    ('PA_TICKETS_WORKFLOW','"S_WORKFLOW_CODE"'),
    ('PA_TICKETS_WORKFLOW_VERSION','"N_WORKFLOW_ID","N_VERSION_NO"'),
    ('PA_TICKETS_WORKFLOW_VERSION','"N_WORKFLOW_VERSION_ID","N_WORKFLOW_ID"'),
    ('PA_TICKETS_WORKFLOW_STEP','"N_WORKFLOW_VERSION_ID","S_STEP_CODE"'),
    ('PA_TICKETS_WORKFLOW_TRANSITION','"N_WORKFLOW_VERSION_ID","S_TRANSITION_CODE"'),
    ('PA_TICKETS_WORKFLOW_STEP_INSTANCE','"N_WORKFLOW_INSTANCE_ID","N_WORKFLOW_STEP_ID"'),
    ('PA_TICKETS_BACKGROUND_JOB','"S_JOB_TYPE","S_DEDUPE_KEY"')
  ) AS x(table_name, key_columns) LOOP
    IF to_regclass(format('public.%I', r.table_name)) IS NULL THEN
      RAISE NOTICE 'NOT_APPLICABLE duplicate check: %', r.table_name;
    ELSE
      EXECUTE format('SELECT count(*) FROM (SELECT %s FROM public.%I WHERE %s GROUP BY %s HAVING count(*) > 1) q',
        r.key_columns, r.table_name,
        'TRUE',
        r.key_columns) INTO v_count;
      IF v_count > 0 THEN RAISE EXCEPTION 'Duplicate key groups in %: %', r.table_name, v_count; END IF;
    END IF;
  END LOOP;
END $$;

-- Exact checks for the adapter's partial idempotency indexes and expression
-- unique email index. These predicates intentionally mirror the index DDL.
DO $$
DECLARE v_count bigint;
BEGIN
  IF to_regclass('public."PA_TICKETS_IDEMPOTENCY"') IS NOT NULL THEN
    EXECUTE 'SELECT count(*) FROM (SELECT "S_SCOPE","N_ACTOR_USER_ID","S_IDEMPOTENCY_KEY" FROM public."PA_TICKETS_IDEMPOTENCY" WHERE "N_ACTOR_USER_ID" IS NOT NULL GROUP BY "S_SCOPE","N_ACTOR_USER_ID","S_IDEMPOTENCY_KEY" HAVING count(*) > 1) q' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Idempotency actor-key duplicate groups: %', v_count; END IF;
    EXECUTE 'SELECT count(*) FROM (SELECT "S_SCOPE","S_IDEMPOTENCY_KEY" FROM public."PA_TICKETS_IDEMPOTENCY" WHERE "N_ACTOR_USER_ID" IS NULL GROUP BY "S_SCOPE","S_IDEMPOTENCY_KEY" HAVING count(*) > 1) q' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Idempotency anonymous-key duplicate groups: %', v_count; END IF;
  ELSE RAISE NOTICE 'NOT_APPLICABLE duplicate check: PA_TICKETS_IDEMPOTENCY'; END IF;
  IF to_regclass('public."PA_TICKETS_USER"') IS NOT NULL THEN
    EXECUTE 'SELECT count(*) FROM (SELECT upper("S_EMAIL") FROM public."PA_TICKETS_USER" WHERE "S_EMAIL" IS NOT NULL GROUP BY upper("S_EMAIL") HAVING count(*) > 1) q' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Case-insensitive user email duplicate groups: %', v_count; END IF;
  ELSE RAISE NOTICE 'NOT_APPLICABLE duplicate check: PA_TICKETS_USER'; END IF;
END $$;

-- Foreign-key orphan checks derived from the adapter references.
DO $$
DECLARE r record; v_count bigint;
BEGIN
  FOR r IN SELECT * FROM (VALUES
    ('PA_TICKETS_WORK_ITEM','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_ASSIGNMENT','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_ASSIGNMENT','N_WORK_ITEM_ID','PA_TICKETS_WORK_ITEM','N_WORK_ITEM_ID'),
    ('PA_TICKETS_EVENT','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_APPROVAL_RULE_VERSION','N_APPROVAL_RULE_ID','PA_TICKETS_APPROVAL_RULE','N_APPROVAL_RULE_ID'),
    ('PA_TICKETS_APPROVAL_RULE_STEP','N_APPROVAL_RULE_ID','PA_TICKETS_APPROVAL_RULE','N_APPROVAL_RULE_ID'),
    ('PA_TICKETS_WORKFLOW_INSTANCE','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_APPROVAL_INSTANCE','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_APPROVAL_INSTANCE','N_APPROVAL_RULE_VERSION_ID','PA_TICKETS_APPROVAL_RULE_VERSION','N_APPROVAL_RULE_VERSION_ID'),
    ('PA_TICKETS_APPROVAL','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_APPROVAL','N_APPROVAL_INSTANCE_ID','PA_TICKETS_APPROVAL_INSTANCE','N_APPROVAL_INSTANCE_ID'),
    ('PA_TICKETS_SLA','N_TICKET_ID','PA_TICKETS','N_TICKET_ID'),
    ('PA_TICKETS_TEAM','N_DEPARTMENT_ID','PA_TICKETS_DEPARTMENT','N_DEPARTMENT_ID'),
    ('PA_TICKETS_USER','N_DEPARTMENT_ID','PA_TICKETS_DEPARTMENT','N_DEPARTMENT_ID'),
    ('PA_TICKETS_USER','N_TEAM_ID','PA_TICKETS_TEAM','N_TEAM_ID'),
    ('PA_TICKETS_USER','N_MANAGER_USER_ID','PA_TICKETS_USER','N_USER_ID'),
    ('PA_TICKETS_USER_ROLE','N_USER_ID','PA_TICKETS_USER','N_USER_ID'),
    ('PA_TICKETS_USER_ROLE','N_ROLE_ID','PA_TICKETS_ROLE','N_ROLE_ID'),
    ('PA_TICKETS_ROLE_PERMISSION','N_ROLE_ID','PA_TICKETS_ROLE','N_ROLE_ID'),
    ('PA_TICKETS_ROLE_PERMISSION','N_PERMISSION_ID','PA_TICKETS_PERMISSION','N_PERMISSION_ID'),
    ('PA_TICKETS_USER_CLIENT','N_USER_ID','PA_TICKETS_USER','N_USER_ID'),
    ('PA_TICKETS_APPROVAL_RULE_STEP','N_ROLE_ID','PA_TICKETS_ROLE','N_ROLE_ID'),
    ('PA_TICKETS_APPROVAL_RULE_STEP','N_DEPARTMENT_ID','PA_TICKETS_DEPARTMENT','N_DEPARTMENT_ID'),
    ('PA_TICKETS_APPROVAL_RULE_STEP','N_TEAM_ID','PA_TICKETS_TEAM','N_TEAM_ID'),
    ('PA_TICKETS_APPROVAL_RULE_STEP','N_USER_ID','PA_TICKETS_USER','N_USER_ID')
  ) AS x(child_table, child_column, parent_table, parent_column) LOOP
    IF to_regclass(format('public.%I', r.child_table)) IS NULL OR to_regclass(format('public.%I', r.parent_table)) IS NULL THEN
      RAISE NOTICE 'NOT_APPLICABLE orphan check: %.% -> %.%', r.child_table, r.child_column, r.parent_table, r.parent_column;
    ELSE
      EXECUTE format('SELECT count(*) FROM public.%I c LEFT JOIN public.%I p ON c.%I = p.%I WHERE c.%I IS NOT NULL AND p.%I IS NULL', r.child_table, r.parent_table, r.child_column, r.parent_column, r.child_column, r.parent_column) INTO v_count;
      IF v_count > 0 THEN RAISE EXCEPTION 'Foreign-key orphan rows in %.%: %', r.child_table, r.child_column, v_count; END IF;
    END IF;
  END LOOP;
  IF to_regclass('public."PA_TICKETS_APPROVAL_RULE_STEP"') IS NOT NULL AND to_regclass('public."PA_TICKETS_APPROVAL_RULE_VERSION"') IS NOT NULL THEN
    EXECUTE 'SELECT count(*) FROM public."PA_TICKETS_APPROVAL_RULE_STEP" c LEFT JOIN public."PA_TICKETS_APPROVAL_RULE_VERSION" p ON c."N_APPROVAL_RULE_VERSION_ID" = p."N_APPROVAL_RULE_VERSION_ID" AND c."N_APPROVAL_RULE_ID" = p."N_APPROVAL_RULE_ID" WHERE c."N_APPROVAL_RULE_VERSION_ID" IS NOT NULL AND c."N_APPROVAL_RULE_ID" IS NOT NULL AND p."N_APPROVAL_RULE_VERSION_ID" IS NULL' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Composite approval-rule orphan rows: %', v_count; END IF;
  END IF;
END $$;

-- Partial-index-specific checks. These are dynamic so absent tables are safe.
DO $$
DECLARE v_count bigint;
BEGIN
  IF to_regclass('public."PA_TICKETS_ASSIGNMENT"') IS NOT NULL THEN
    EXECUTE 'SELECT count(*) FROM (SELECT "N_WORK_ITEM_ID" FROM public."PA_TICKETS_ASSIGNMENT" WHERE "D_UNASSIGNED_AT" IS NULL GROUP BY "N_WORK_ITEM_ID" HAVING count(*) > 1) q' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Active assignment duplicate groups: %', v_count; END IF;
  ELSE RAISE NOTICE 'NOT_APPLICABLE duplicate check: PA_TICKETS_ASSIGNMENT'; END IF;
  IF to_regclass('public."PA_TICKETS_APPROVAL_RULE_STEP"') IS NOT NULL THEN
    EXECUTE 'SELECT count(*) FROM (SELECT "N_APPROVAL_RULE_VERSION_ID","N_STEP_NO","S_TARGET_KEY" FROM public."PA_TICKETS_APPROVAL_RULE_STEP" WHERE "N_APPROVAL_RULE_VERSION_ID" IS NOT NULL GROUP BY "N_APPROVAL_RULE_VERSION_ID","N_STEP_NO","S_TARGET_KEY" HAVING count(*) > 1) q' INTO v_count;
    IF v_count > 0 THEN RAISE EXCEPTION 'Approval-rule target duplicate groups: %', v_count; END IF;
  ELSE RAISE NOTICE 'NOT_APPLICABLE duplicate check: PA_TICKETS_APPROVAL_RULE_STEP'; END IF;
END $$;

-- Review-only expected-vs-observed inventory. The expected relation names are
-- extracted from the immutable historical baseline artifact at commit
-- 1bcc493d267e23e3e126b9b698fa4344ebe8588a (SHA-256
-- 4F4EC0066DC62AAA61C75DB67177A25D152FBF6484CCB4711A942FD1DC58EC19).
-- Presence/absence is reported, never interpreted as execution history or approval.
WITH expected(object_kind, object_name) AS (
  VALUES
    ('TABLE', 'PA_TICKETS'),
    ('TABLE', 'PA_TICKETS_APPROVAL'),
    ('TABLE', 'PA_TICKETS_APPROVAL_DELEGATION'),
    ('TABLE', 'PA_TICKETS_APPROVAL_HISTORY'),
    ('TABLE', 'PA_TICKETS_APPROVAL_INSTANCE'),
    ('TABLE', 'PA_TICKETS_APPROVAL_RULE'),
    ('TABLE', 'PA_TICKETS_APPROVAL_RULE_STEP'),
    ('TABLE', 'PA_TICKETS_APPROVAL_RULE_VERSION'),
    ('TABLE', 'PA_TICKETS_ASSIGNMENT'),
    ('TABLE', 'PA_TICKETS_AUDIT'),
    ('TABLE', 'PA_TICKETS_AUDIT_CHAIN_LOCK'),
    ('TABLE', 'PA_TICKETS_AUDIT_MAINTENANCE'),
    ('TABLE', 'PA_TICKETS_AUDIT_MAINTENANCE_TOKEN'),
    ('TABLE', 'PA_TICKETS_BACKGROUND_JOB'),
    ('TABLE', 'PA_TICKETS_DEPARTMENT'),
    ('TABLE', 'PA_TICKETS_EVENT'),
    ('TABLE', 'PA_TICKETS_IDEMPOTENCY'),
    ('TABLE', 'PA_TICKETS_PERMISSION'),
    ('TABLE', 'PA_TICKETS_ROLE'),
    ('TABLE', 'PA_TICKETS_ROLE_PERMISSION'),
    ('TABLE', 'PA_TICKETS_SLA'),
    ('TABLE', 'PA_TICKETS_SLA_CALENDAR'),
    ('TABLE', 'PA_TICKETS_SLA_PAUSE'),
    ('TABLE', 'PA_TICKETS_SLA_POLICY'),
    ('TABLE', 'PA_TICKETS_TEAM'),
    ('TABLE', 'PA_TICKETS_USER'),
    ('TABLE', 'PA_TICKETS_USER_CLIENT'),
    ('TABLE', 'PA_TICKETS_USER_ROLE'),
    ('TABLE', 'PA_TICKETS_WAITING_REASON'),
    ('TABLE', 'PA_TICKETS_WORK_ITEM'),
    ('TABLE', 'PA_TICKETS_WORKFLOW'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_INSTANCE'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_STEP'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_STEP_INSTANCE'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_TRANSITION'),
    ('TABLE', 'PA_TICKETS_WORKFLOW_VERSION'),
    ('INDEX', 'IX_PA_TICKETS_APPROVAL_INSTANCE'),
    ('INDEX', 'IX_PA_TICKETS_SLA_POLICY_TARGET'),
    ('INDEX', 'IX_PA_TICKETS_USER_CLIENT_EFFECTIVE'),
    ('INDEX', 'IX_PA_TICKETS_USER_ROLE_EFFECTIVE'),
    ('INDEX', 'IX_PA_TICKETS_WAITING_REASON_EFFECTIVE'),
    ('INDEX', 'UK_PA_TICKETS_USER_EMAIL_UPPER'),
    ('INDEX', 'UX_PA_TICKETS_APRS_VERSION_TARGET'),
    ('INDEX', 'UX_PA_TICKETS_ASSIGNMENT_ACTIVE'),
    ('INDEX', 'UX_PA_TICKETS_BACKGROUND_JOB_DEDUPE'),
    ('INDEX', 'UX_PA_TICKETS_IDEMPOTENCY_SCOPE_ACTOR_KEY'),
    ('INDEX', 'UX_PA_TICKETS_IDEMPOTENCY_SCOPE_KEY'),
    ('INDEX', 'UX_PA_TICKETS_STEP_INSTANCE_ONE'),
    ('INDEX', 'UX_PA_TICKETS_WF_STEP_CODE'),
    ('INDEX', 'UX_PA_TICKETS_WF_TRANSITION_CODE'),
    ('INDEX', 'UX_PA_TICKETS_WFV_CODE'),
    ('INDEX', 'UX_PA_TICKETS_WFV_ID_WF'),
    ('INDEX', 'UX_PA_TICKETS_WORK_ITEM_TICKET_NO'),
    ('INDEX', 'UX_PA_TICKETS_WORKFLOW_CODE'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_DELEGATION_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_HISTORY_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_INSTANCE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_RULE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_RULE_STEP_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_RULE_VERSION_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_APPROVAL_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_ASSIGNMENT_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_AUDIT_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_BACKGROUND_JOB_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_DEPARTMENT_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_EVENT_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_IDEMPOTENCY_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_PERMISSION_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_ROLE_PERMISSION_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_ROLE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_SLA_PAUSE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_SLA_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_TEAM_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_TICKET_NO_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_USER_CLIENT_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_USER_ROLE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_USER_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WAITING_REASON_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORK_ITEM_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_INSTANCE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_STEP_INSTANCE_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_STEP_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_TRANSITION_SEQ'),
    ('SEQUENCE', 'PA_TICKETS_WORKFLOW_VERSION_SEQ')
), observed AS (
  SELECT CASE c.relkind WHEN 'S' THEN 'SEQUENCE' WHEN 'r' THEN 'TABLE' WHEN 'p' THEN 'PARTITIONED_TABLE' WHEN 'v' THEN 'VIEW' WHEN 'm' THEN 'MATERIALIZED_VIEW' WHEN 'f' THEN 'FOREIGN_TABLE' WHEN 'i' THEN 'INDEX' WHEN 'I' THEN 'PARTITIONED_INDEX' END AS object_kind,
         c.relname AS object_name
  FROM pg_catalog.pg_class AS c
  JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
  WHERE n.nspname = 'public' AND c.relkind IN ('r','p','v','m','f','S','i','I')
), compared AS (
  SELECT COALESCE(e.object_kind, o.object_kind) AS object_kind,
         COALESCE(e.object_name, o.object_name) AS object_name,
         CASE WHEN e.object_name IS NOT NULL AND o.object_name IS NOT NULL THEN 'EXPECTED_PRESENT'
              WHEN e.object_name IS NOT NULL THEN 'MISSING_EXPECTED'
              ELSE 'UNEXPECTED_OBSERVED' END AS inventory_status
  FROM expected AS e
  FULL OUTER JOIN observed AS o USING (object_kind, object_name)
)
SELECT 'HISTORICAL_BASELINE_CANDIDATE_NOT_ADOPTED'::text AS comparison_basis,
       object_kind, object_name, inventory_status
FROM compared
ORDER BY object_kind, object_name, inventory_status;

-- Ledger presence is reported separately; migration history is still UNKNOWN.
SELECT 'UNKNOWN'::text AS migration_history_status,
       (to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') IS NOT NULL) AS ledger_object_present,
       'Catalog shape and ledger rows do not establish applied history'::text AS basis;
-- A runner must require this marker and a successful SQL exit status; errors raise before it.
SELECT 'PREFLIGHT_COMPLETE' AS preflight_status, current_database() AS database_name, inet_server_addr() AS server_address, inet_server_port() AS server_port, current_user AS role_name;

ROLLBACK;


