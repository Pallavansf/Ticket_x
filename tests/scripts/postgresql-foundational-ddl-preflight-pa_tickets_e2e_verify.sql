-- PA_TICKETS PostgreSQL foundational DDL preflight proposal
-- TARGET: host localhost (must resolve to a loopback address), port 5432,
-- database pa_tickets_e2e_verify, role postgres
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
  IF current_database() <> 'pa_tickets_e2e_verify' THEN
    RAISE EXCEPTION 'Wrong database: expected pa_tickets_e2e_verify';
  END IF;
  IF current_database() IN ('pa_tickets_test','pa_tickets_h_verify') THEN
    RAISE EXCEPTION 'Prohibited database target';
  END IF;
  v_major := (current_setting('server_version_num')::integer / 10000);
  IF v_major <> 18 THEN
    RAISE EXCEPTION 'Wrong PostgreSQL major version: expected 18';
  END IF;
  IF inet_server_addr() IS NULL OR NOT (inet_server_addr() <<= '127.0.0.0/8'::inet OR inet_server_addr() = '::1'::inet) THEN
    RAISE EXCEPTION 'Wrong server address: expected a local loopback address; actual=%', inet_server_addr();
  END IF;
  IF inet_server_port() <> 5432 THEN
    RAISE EXCEPTION 'Wrong server port: expected 5432';
  END IF;
  IF current_user <> 'postgres' THEN
    RAISE EXCEPTION 'Wrong role: expected postgres';
  END IF;
  IF current_schema() <> 'public' THEN
    RAISE EXCEPTION 'Wrong schema context: expected public';
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

SELECT table_schema, table_name, column_name, ordinal_position,
       data_type, udt_schema, udt_name, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;

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
JOIN pg_sequence s ON s.seqrelid = seq.oid
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

-- Migration-history evidence. Object existence is reported, never treated as
-- proof that a migration ran.
SELECT to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') AS migration_ledger;
DO $$
DECLARE r record;
BEGIN
  IF to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') IS NULL THEN
    RAISE NOTICE 'Migration ledger is absent; no applied identifiers are established.';
  ELSE
    FOR r IN EXECUTE 'SELECT "S_MIGRATION_ID", "S_MIGRATION_SHA256", "S_RELEASE_BASELINE", "S_TARGET_FINGERPRINT", "S_STATUS", "D_APPLIED_AT", "S_APPROVED_BY" FROM public."PA_TICKETS_SCHEMA_MIGRATION" ORDER BY "S_MIGRATION_ID"' LOOP
      RAISE NOTICE 'Ledger row: id=%, hash=%, baseline=%, target=%, status=%, applied_at=%, approved_by=%', r."S_MIGRATION_ID", r."S_MIGRATION_SHA256", r."S_RELEASE_BASELINE", r."S_TARGET_FINGERPRINT", r."S_STATUS", r."D_APPLIED_AT", r."S_APPROVED_BY";
    END LOOP;
  END IF;
END $$;

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

-- Baseline gate. Existing relevant objects or ledger rows require a reviewed
-- catalog-definition comparison and approved adoption procedure; this script
-- does not infer a baseline from object shape.
DO $$
DECLARE v_existing integer; v_ledger boolean;
BEGIN
  SELECT count(*) INTO v_existing
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
  WHERE n.nspname = 'public' AND c.relkind IN ('r','p','v','m','S','i');
  SELECT to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') IS NOT NULL INTO v_ledger;
  IF v_existing > 0 OR v_ledger THEN
    RAISE EXCEPTION 'Baseline not established: existing public objects or migration ledger require owner-approved adoption review';
  END IF;
END $$;

-- A runner must require this marker and a successful SQL exit status; errors raise before it.
SELECT 'PREFLIGHT_COMPLETE' AS preflight_status, current_database() AS database_name, inet_server_addr() AS server_address, inet_server_port() AS server_port, current_user AS role_name;

ROLLBACK;


