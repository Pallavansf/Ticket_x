-- REVIEW CANDIDATE ONLY: 026_phase8_6_reporting.sql
-- This file is not approved for execution and does not replace the original migration.
--
-- Transaction contract: the deployment runner MUST provide one outer transaction and
-- abort on any error. This candidate deliberately contains no BEGIN/COMMIT so it can
-- be composed by a runner that owns the transaction. Do not run it through a runner
-- that auto-commits each statement. PostgreSQL transactional DDL provides rollback
-- before commit; recovery after commit is an operator responsibility. A connection
-- drop or uncertain commit outcome MUST be reported as UNKNOWN and must not be
-- retried automatically; a separate read-only catalog verification is required.
--
-- Scope: reporting persistence only. No FKs are added for requested-by, schedule
-- owner, schedule, or export references; each remains a separate owner decision.
-- Sequence OWNED BY remains an owner decision. Alternatives are unowned sequences
-- (matching the original) or OWNED BY the identity column (stronger lifecycle coupling).
-- This candidate does not choose between them and blocks ambiguous existing ownership.
--
-- Constraint policy is also an owner decision. Strict equivalence rejects every
-- unexpected constraint; permissive adoption allows extras but is not definition-
-- equivalent and may change application behavior. This candidate recommends strict
-- equivalence and fails closed on unexpected constraints until that policy is approved.
--
-- Column equivalence requires name, ordinal position, type identity/typmod, length,
-- nullability, default, default collation, and no identity/generated expression.
--
-- Sequence adoption requires start, increment, min, max, type, cycle and cache
-- compatibility. A non-initial current value or unknown is blocked for data review;
-- this candidate never resets or advances a sequence.
-- Sequence OWNED BY policy is intentionally unresolved; the original migration does not set it.
-- Empty-reporting-object alternative: because the structural foundation already exists,
-- a later deployment may require that these three reporting tables, three sequences,
-- two indexes, and their constraints are absent before creation. The earlier empty
-- preflight cannot be reused as proof of this state. If any named reporting object
-- exists, adoption checks remain mandatory and ambiguous objects block execution.
--
-- No ticket-aging evidence, authorization, audit, retention, cleanup, provisioning,
-- or migration-ledger behavior is included.

-- Name/order, collation, identity and generated-column checks are separate
    -- from information_schema type checks so adoption is not count-only.
    DO $$
    DECLARE
        cols record;
        actual_names text[];
    BEGIN
        FOR cols IN
            SELECT * FROM (VALUES
              ('PA_TICKETS_REPORT_EXPORT'::text, ARRAY['N_EXPORT_ID','S_REPORT_CODE','S_FORMAT','N_REQUESTED_BY','S_FILTERS_JSON','S_STATUS','D_CREATED_AT','D_STARTED_AT','D_COMPLETED_AT','N_ROW_COUNT','N_FILE_SIZE','S_CONTENT_TYPE','S_FILE_HASH','S_STORAGE_KEY','S_FAILURE_CLASSIFICATION','S_FAILURE_MESSAGE','S_CORRELATION_ID','D_EXPIRES_AT']::text[]),
              ('PA_TICKETS_REPORT_SCHEDULE'::text, ARRAY['N_SCHEDULE_ID','N_OWNER_USER_ID','S_REPORT_CODE','S_FORMAT','S_FILTERS_JSON','S_FREQUENCY','N_LOCAL_HOUR','N_LOCAL_MINUTE','N_DAY_OF_WEEK','N_DAY_OF_MONTH','S_TIME_ZONE','D_NEXT_RUN_AT','S_STATUS','D_CREATED_AT','D_UPDATED_AT']::text[]),
              ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text, ARRAY['N_EXECUTION_ID','N_SCHEDULE_ID','N_EXPORT_ID','S_STATUS','D_STARTED_AT','D_COMPLETED_AT','S_CORRELATION_ID','S_IDEMPOTENCY_KEY','S_FAILURE_MESSAGE']::text[])
            ) AS x(table_name, expected_names)
        LOOP
            IF to_regclass(format('public.%I', cols.table_name)) IS NOT NULL THEN
                SELECT array_agg(c.column_name ORDER BY c.ordinal_position)
                  INTO actual_names
                FROM information_schema.columns c
                WHERE c.table_schema = 'public' AND c.table_name = cols.table_name;
                IF actual_names IS DISTINCT FROM cols.expected_names THEN
                    RAISE EXCEPTION 'Existing public.% has incompatible column names or ordinal positions', cols.table_name;
                END IF;
                IF EXISTS (
                    SELECT 1
                    FROM pg_attribute a
                    JOIN pg_class t ON t.oid = a.attrelid
                    JOIN pg_namespace n ON n.oid = t.relnamespace
                    JOIN pg_type ty ON ty.oid = a.atttypid
                    WHERE n.nspname = 'public'
                      AND t.relname = cols.table_name
                      AND a.attnum > 0 AND NOT a.attisdropped
                      AND (a.attidentity <> '' OR a.attgenerated <> ''
                           OR (a.attcollation <> 0 AND a.attcollation <> ty.typcollation))
                ) THEN
                    RAISE EXCEPTION 'Existing public.% has incompatible identity, generated, or collation metadata', cols.table_name;
                END IF;
            END IF;
        END LOOP;
    END $$;

-- Strict-equivalence preflight: unexpected constraints are never assumed safe.
    -- This remains blocked until the owner approves strict equivalence.
    DO $$
    DECLARE
        c record;
    BEGIN
        FOR c IN
            SELECT conrelid::regclass::text AS relation_name, conname
            FROM pg_constraint con
            JOIN pg_class t ON t.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
              AND t.relname IN ('PA_TICKETS_REPORT_EXPORT','PA_TICKETS_REPORT_SCHEDULE','PA_TICKETS_REPORT_SCHEDULE_EXECUTION')
              AND conname NOT IN ('PK_PA_TICKETS_REPORT_EXPORT','PK_PA_TICKETS_REPORT_SCHEDULE','PK_PA_TICKETS_REPORT_SCHEDULE_EXECUTION','UK_PA_TICKETS_REPORT_SCHEDULE_EXECUTION_KEY')
        LOOP
            RAISE EXCEPTION 'Unexpected constraint %.% requires owner adoption decision', c.relation_name, c.conname;
        END LOOP;
    END $$;

DO $$
DECLARE
    r record;
    expected record;

    column_count integer;
    sequence_name text;
    expected_default text;
    sequence_last bigint;
    sequence_called boolean;
BEGIN
    -- The runner must use a role with schema CREATE/USAGE and catalog visibility;
    -- this candidate never grants privileges.
    IF NOT has_schema_privilege(current_user, 'public', 'USAGE')
       OR NOT has_schema_privilege(current_user, 'public', 'CREATE') THEN
        RAISE EXCEPTION 'Execution role lacks required public schema privileges';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'public') THEN
        RAISE EXCEPTION 'Reporting migration requires schema public';
    END IF;

    -- Existing objects are adopted only when their relation kind and complete
    -- column contract match. Same-name presence is never treated as compatibility.
    FOR r IN
        SELECT * FROM (VALUES
            ('PA_TICKETS_REPORT_EXPORT'::text, 'r'::"char"),
            ('PA_TICKETS_REPORT_SCHEDULE'::text, 'r'::"char"),
            ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text, 'r'::"char"),
            ('PA_TICKETS_REPORT_EXPORT_SEQ'::text, 'S'::"char"),
            ('PA_TICKETS_REPORT_SCHEDULE_SEQ'::text, 'S'::"char"),
            ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ'::text, 'S'::"char")
        ) AS x(object_name, expected_kind)
    LOOP
        IF to_regclass(format('public.%I', r.object_name)) IS NOT NULL
           AND NOT EXISTS (
               SELECT 1
               FROM pg_class c
               JOIN pg_namespace n ON n.oid = c.relnamespace
               WHERE n.nspname = 'public'
                 AND c.relname = r.object_name
                 AND c.relkind = r.expected_kind
           ) THEN
            RAISE EXCEPTION 'Existing public.% has incompatible relation kind', r.object_name;
        END IF;
        IF to_regclass(format('public.%I', r.object_name)) IS NOT NULL
           AND r.expected_kind = 'r'::"char"
           AND NOT has_table_privilege(current_user, format('public.%I', r.object_name), 'SELECT') THEN
            RAISE EXCEPTION 'Execution role cannot inspect existing table public.%', r.object_name;
        END IF;
        IF to_regclass(format('public.%I', r.object_name)) IS NOT NULL
           AND r.expected_kind = 'S'::"char"
           AND NOT has_sequence_privilege(current_user, format('public.%I', r.object_name), 'USAGE') THEN
            RAISE EXCEPTION 'Execution role cannot inspect existing sequence public.%', r.object_name;
        END IF;
        IF to_regclass(format('public.%I', r.object_name)) IS NOT NULL
           AND r.expected_kind = 'S'::"char"
           AND NOT has_sequence_privilege(current_user, format('public.%I', r.object_name), 'SELECT') THEN
            RAISE EXCEPTION 'Execution role cannot observe state of existing sequence public.%', r.object_name;
        END IF;
    END LOOP;

    -- Validate existing table columns before any CREATE. Column type, length,
    -- nullability and default are checked from the catalog; missing or extra
    -- columns are fatal for adoption.
    FOR expected IN
        SELECT * FROM (VALUES
          ('PA_TICKETS_REPORT_EXPORT'::text,'N_EXPORT_ID'::text,'bigint'::text,NULL::integer,false,'nextval(''"PA_TICKETS_REPORT_EXPORT_SEQ"''::regclass)'::text),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_REPORT_CODE','character varying',100,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_FORMAT','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'N_REQUESTED_BY','bigint',NULL,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_FILTERS_JSON','jsonb',NULL,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_STATUS','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'D_CREATED_AT','timestamp with time zone',NULL,false,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'D_STARTED_AT','timestamp with time zone',NULL,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'D_COMPLETED_AT','timestamp with time zone',NULL,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'N_ROW_COUNT','integer',NULL,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'N_FILE_SIZE','bigint',NULL,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_CONTENT_TYPE','character varying',200,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_FILE_HASH','character varying',128,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_STORAGE_KEY','character varying',500,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_FAILURE_CLASSIFICATION','character varying',40,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_FAILURE_MESSAGE','character varying',1000,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'S_CORRELATION_ID','character varying',100,true,NULL),
          ('PA_TICKETS_REPORT_EXPORT'::text,'D_EXPIRES_AT','timestamp with time zone',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_SCHEDULE_ID','bigint',NULL,false,'nextval(''"PA_TICKETS_REPORT_SCHEDULE_SEQ"''::regclass)'),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_OWNER_USER_ID','bigint',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_REPORT_CODE','character varying',100,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_FORMAT','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_FILTERS_JSON','jsonb',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_FREQUENCY','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_LOCAL_HOUR','integer',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_LOCAL_MINUTE','integer',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_DAY_OF_WEEK','integer',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'N_DAY_OF_MONTH','integer',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_TIME_ZONE','character varying',100,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'D_NEXT_RUN_AT','timestamp with time zone',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'S_STATUS','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'D_CREATED_AT','timestamp with time zone',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE'::text,'D_UPDATED_AT','timestamp with time zone',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'N_EXECUTION_ID','bigint',NULL,false,'nextval(''"PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ"''::regclass)'),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'N_SCHEDULE_ID','bigint',NULL,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'N_EXPORT_ID','bigint',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'S_STATUS','character varying',20,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'D_STARTED_AT','timestamp with time zone',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'D_COMPLETED_AT','timestamp with time zone',NULL,true,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'S_CORRELATION_ID','character varying',100,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'S_IDEMPOTENCY_KEY','character varying',200,false,NULL),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION'::text,'S_FAILURE_MESSAGE','character varying',1000,true,NULL)
        ) AS x(table_name,column_name,data_type,max_length,nullable,default_expression)
    LOOP
        IF to_regclass(format('public.%I', expected.table_name)) IS NOT NULL THEN


            SELECT COUNT(*) INTO column_count
            FROM information_schema.columns c
            WHERE c.table_schema = 'public'
              AND c.table_name = expected.table_name
              AND c.column_name = expected.column_name;
            IF column_count = 0 THEN
                RAISE EXCEPTION 'Existing public.%.% is missing', expected.table_name, expected.column_name;
            END IF;
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns c
                WHERE c.table_schema = 'public'
                  AND c.table_name = expected.table_name
                  AND c.column_name = expected.column_name
                  AND c.data_type = expected.data_type
                  AND (expected.max_length IS NULL OR c.character_maximum_length = expected.max_length)
                  AND (c.is_nullable = CASE WHEN expected.nullable THEN 'YES' ELSE 'NO' END)
            ) THEN
                RAISE EXCEPTION 'Existing public.%.% has incompatible type, length, or nullability', expected.table_name, expected.column_name;
            END IF;
            IF expected.default_expression IS NULL AND EXISTS (
                SELECT 1 FROM information_schema.columns c
                WHERE c.table_schema = 'public'
                  AND c.table_name = expected.table_name
                  AND c.column_name = expected.column_name
                  AND c.column_default IS NOT NULL
            ) THEN
                RAISE EXCEPTION 'Existing default for public.%.% is incompatible; expected no default', expected.table_name, expected.column_name;
            END IF;
            IF expected.default_expression IS NOT NULL THEN
                sequence_name := CASE expected.column_name
                    WHEN 'N_EXPORT_ID' THEN 'PA_TICKETS_REPORT_EXPORT_SEQ'
                    WHEN 'N_SCHEDULE_ID' THEN 'PA_TICKETS_REPORT_SCHEDULE_SEQ'
                    WHEN 'N_EXECUTION_ID' THEN 'PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ'
                END;
                expected_default := regexp_replace(format('nextval(''public."%s"''::regclass)', sequence_name), '\s+', '', 'g');
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_attrdef d
                    JOIN pg_class tbl ON tbl.oid = d.adrelid
                    JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
                    JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum
                    JOIN pg_depend dep ON dep.classid = 'pg_attrdef'::regclass
                                         AND dep.objid = d.oid
                                         AND dep.refclassid = 'pg_class'::regclass
                    JOIN pg_class seq ON seq.oid = dep.refobjid
                    JOIN pg_namespace sns ON sns.oid = seq.relnamespace
                    WHERE ns.nspname = 'public'
                      AND tbl.relname = expected.table_name
                      AND a.attname = expected.column_name
                      AND sns.nspname = 'public'
                      AND seq.relname = sequence_name
                      AND regexp_replace(pg_get_expr(d.adbin, d.adrelid), '\s+', '', 'g') = expected_default
                ) THEN
                    RAISE EXCEPTION 'Existing default for public.%.% does not resolve to public.%', expected.table_name, expected.column_name, sequence_name;
                END IF;
            END IF;
        END IF;
    END LOOP;
    -- Existing sequences must also have the expected generator contract.
    FOR r IN
        SELECT * FROM (VALUES
          ('PA_TICKETS_REPORT_EXPORT_SEQ'::text),
          ('PA_TICKETS_REPORT_SCHEDULE_SEQ'::text),
          ('PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ'::text)
        ) AS x(sequence_name)
    LOOP
        IF to_regclass(format('public.%I', r.sequence_name)) IS NOT NULL
           AND NOT EXISTS (
               SELECT 1
               FROM pg_sequences s
               WHERE s.schemaname = 'public'
                 AND s.sequencename = r.sequence_name
                 AND s.start_value = 1
                 AND s.increment_by = 1
                 AND s.min_value = 1
                 AND s.max_value = 9223372036854775807
                 AND s.data_type::text = 'bigint'
                 AND s.cycle = false
                 AND s.cache_size = 1
           ) THEN
            RAISE EXCEPTION 'Existing public.% has incompatible sequence definition', r.sequence_name;
        END IF;
        BEGIN
            EXECUTE format('SELECT last_value, is_called FROM public.%I', r.sequence_name)
                INTO sequence_last, sequence_called;
        EXCEPTION WHEN OTHERS THEN
            RAISE EXCEPTION 'Unable to observe state of existing public.%; sequence adoption is blocked', r.sequence_name;
        END;
        IF sequence_called IS DISTINCT FROM false OR sequence_last IS DISTINCT FROM 1 THEN
            RAISE EXCEPTION 'Existing public.% is not an untouched sequence at start value 1', r.sequence_name;
        END IF;
        IF EXISTS (
            SELECT 1
            FROM pg_depend d
            JOIN pg_class seq ON seq.oid = d.objid
            JOIN pg_namespace ns ON ns.oid = seq.relnamespace
            WHERE ns.nspname = 'public' AND seq.relname = r.sequence_name
              AND d.classid = 'pg_class'::regclass
              AND d.refclassid = 'pg_class'::regclass
              AND d.deptype IN ('a','i')
        ) THEN
            RAISE EXCEPTION 'Existing public.% has OWNED BY metadata; ownership policy requires owner approval', r.sequence_name;
        END IF;
    END LOOP;
END $$;

CREATE SEQUENCE IF NOT EXISTS public."PA_TICKETS_REPORT_EXPORT_SEQ" START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE IF NOT EXISTS public."PA_TICKETS_REPORT_SCHEDULE_SEQ" START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE IF NOT EXISTS public."PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ" START WITH 1 INCREMENT BY 1;

CREATE TABLE IF NOT EXISTS public."PA_TICKETS_REPORT_EXPORT" (
  "N_EXPORT_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_REPORT_EXPORT_SEQ"'::regclass),
  "S_REPORT_CODE" varchar(100) NOT NULL,
  "S_FORMAT" varchar(20) NOT NULL,
  "N_REQUESTED_BY" bigint NOT NULL,
  "S_FILTERS_JSON" jsonb NOT NULL,
  "S_STATUS" varchar(20) NOT NULL,
  "D_CREATED_AT" timestamptz NOT NULL,
  "D_STARTED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "N_ROW_COUNT" integer,
  "N_FILE_SIZE" bigint,
  "S_CONTENT_TYPE" varchar(200),
  "S_FILE_HASH" varchar(128),
  "S_STORAGE_KEY" varchar(500),
  "S_FAILURE_CLASSIFICATION" varchar(40),
  "S_FAILURE_MESSAGE" varchar(1000),
  "S_CORRELATION_ID" varchar(100),
  "D_EXPIRES_AT" timestamptz,
  CONSTRAINT "PK_PA_TICKETS_REPORT_EXPORT" PRIMARY KEY ("N_EXPORT_ID")
);

CREATE TABLE IF NOT EXISTS public."PA_TICKETS_REPORT_SCHEDULE" (
  "N_SCHEDULE_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_REPORT_SCHEDULE_SEQ"'::regclass),
  "N_OWNER_USER_ID" bigint NOT NULL,
  "S_REPORT_CODE" varchar(100) NOT NULL,
  "S_FORMAT" varchar(20) NOT NULL,
  "S_FILTERS_JSON" jsonb NOT NULL,
  "S_FREQUENCY" varchar(20) NOT NULL,
  "N_LOCAL_HOUR" integer NOT NULL,
  "N_LOCAL_MINUTE" integer NOT NULL,
  "N_DAY_OF_WEEK" integer,
  "N_DAY_OF_MONTH" integer,
  "S_TIME_ZONE" varchar(100) NOT NULL,
  "D_NEXT_RUN_AT" timestamptz NOT NULL,
  "S_STATUS" varchar(20) NOT NULL,
  "D_CREATED_AT" timestamptz NOT NULL,
  "D_UPDATED_AT" timestamptz NOT NULL,
  CONSTRAINT "PK_PA_TICKETS_REPORT_SCHEDULE" PRIMARY KEY ("N_SCHEDULE_ID")
);

CREATE TABLE IF NOT EXISTS public."PA_TICKETS_REPORT_SCHEDULE_EXECUTION" (
  "N_EXECUTION_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_REPORT_SCHEDULE_EXECUTION_SEQ"'::regclass),
  "N_SCHEDULE_ID" bigint NOT NULL,
  "N_EXPORT_ID" bigint,
  "S_STATUS" varchar(20) NOT NULL,
  "D_STARTED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "S_CORRELATION_ID" varchar(100) NOT NULL,
  "S_IDEMPOTENCY_KEY" varchar(200) NOT NULL,
  "S_FAILURE_MESSAGE" varchar(1000),
  CONSTRAINT "PK_PA_TICKETS_REPORT_SCHEDULE_EXECUTION" PRIMARY KEY ("N_EXECUTION_ID"),
  CONSTRAINT "UK_PA_TICKETS_REPORT_SCHEDULE_EXECUTION_KEY" UNIQUE ("S_IDEMPOTENCY_KEY")
);

CREATE INDEX IF NOT EXISTS public."IX_PA_TICKETS_REPORT_EXPORT_OWNER"
  ON public."PA_TICKETS_REPORT_EXPORT" ("N_REQUESTED_BY", "D_CREATED_AT");
CREATE INDEX IF NOT EXISTS public."IX_PA_TICKETS_REPORT_SCHEDULE_DUE"
  ON public."PA_TICKETS_REPORT_SCHEDULE" ("S_STATUS", "D_NEXT_RUN_AT");

-- Post-create definition checks are intentionally explicit. They are a review
-- checklist in this candidate until validated on a disposable PostgreSQL target.
DO $$
DECLARE
  n integer;
BEGIN
  SELECT count(*) INTO n FROM information_schema.columns
  WHERE table_schema='public' AND table_name='PA_TICKETS_REPORT_EXPORT';
  IF n <> 18 THEN RAISE EXCEPTION 'PA_TICKETS_REPORT_EXPORT column count mismatch: %', n; END IF;
  SELECT count(*) INTO n FROM information_schema.columns
  WHERE table_schema='public' AND table_name='PA_TICKETS_REPORT_SCHEDULE';
  IF n <> 15 THEN RAISE EXCEPTION 'PA_TICKETS_REPORT_SCHEDULE column count mismatch: %', n; END IF;
  SELECT count(*) INTO n FROM information_schema.columns
  WHERE table_schema='public' AND table_name='PA_TICKETS_REPORT_SCHEDULE_EXECUTION';
  IF n <> 9 THEN RAISE EXCEPTION 'PA_TICKETS_REPORT_SCHEDULE_EXECUTION column count mismatch: %', n; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace s ON s.oid=t.relnamespace WHERE s.nspname='public' AND t.relname='PA_TICKETS_REPORT_EXPORT' AND c.conname='PK_PA_TICKETS_REPORT_EXPORT' AND pg_get_constraintdef(c.oid)='PRIMARY KEY ("N_EXPORT_ID")') THEN
    RAISE EXCEPTION 'Export primary-key definition mismatch';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace s ON s.oid=t.relnamespace WHERE s.nspname='public' AND t.relname='PA_TICKETS_REPORT_SCHEDULE' AND c.conname='PK_PA_TICKETS_REPORT_SCHEDULE' AND pg_get_constraintdef(c.oid)='PRIMARY KEY ("N_SCHEDULE_ID")') THEN
    RAISE EXCEPTION 'Schedule primary-key definition mismatch';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace s ON s.oid=t.relnamespace WHERE s.nspname='public' AND t.relname='PA_TICKETS_REPORT_SCHEDULE_EXECUTION' AND c.conname='PK_PA_TICKETS_REPORT_SCHEDULE_EXECUTION' AND pg_get_constraintdef(c.oid)='PRIMARY KEY ("N_EXECUTION_ID")') THEN
    RAISE EXCEPTION 'Execution primary-key definition mismatch';
  END IF;
  IF NOT EXISTS (
      SELECT 1
      FROM pg_index i
      JOIN pg_class idx ON idx.oid = i.indexrelid
      JOIN pg_class tbl ON tbl.oid = i.indrelid
      JOIN pg_namespace s ON s.oid = tbl.relnamespace
            JOIN pg_am am ON am.oid = i.relam
      WHERE s.nspname='public' AND idx.relname='IX_PA_TICKETS_REPORT_EXPORT_OWNER'
        AND tbl.relname='PA_TICKETS_REPORT_EXPORT' AND i.indisunique=false AND i.indpred IS NULL
        AND i.indexprs IS NULL AND i.indisvalid AND i.indisready AND NOT i.indisexclusion
        AND i.indnkeyatts = 2 AND i.indnatts = 2 AND am.amname = 'btree'
        AND idx.reloptions IS NULL AND i.indoption::int2vector = ARRAY[0,0]::int2vector
        AND i.indclass = ARRAY[
          (SELECT opc.oid FROM pg_opclass opc WHERE opc.opcmethod=am.oid AND opc.opcdefault
             AND opc.opcintype=(SELECT a.atttypid FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='N_REQUESTED_BY')),
          (SELECT opc.oid FROM pg_opclass opc WHERE opc.opcmethod=am.oid AND opc.opcdefault
             AND opc.opcintype=(SELECT a.atttypid FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='D_CREATED_AT'))
        ]::oidvector
        AND (SELECT array_agg(a.attname ORDER BY k.ord)
             FROM unnest(i.indkey) WITH ORDINALITY k(attnum,ord)
             JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum) = ARRAY['N_REQUESTED_BY','D_CREATED_AT']
  ) THEN RAISE EXCEPTION 'Export-owner index definition mismatch'; END IF;
  IF NOT EXISTS (
      SELECT 1
      FROM pg_index i
      JOIN pg_class idx ON idx.oid = i.indexrelid
      JOIN pg_class tbl ON tbl.oid = i.indrelid
      JOIN pg_namespace s ON s.oid = tbl.relnamespace
            JOIN pg_am am ON am.oid = i.relam
      WHERE s.nspname='public' AND idx.relname='IX_PA_TICKETS_REPORT_SCHEDULE_DUE'
        AND tbl.relname='PA_TICKETS_REPORT_SCHEDULE' AND i.indisunique=false AND i.indpred IS NULL
        AND i.indexprs IS NULL AND i.indisvalid AND i.indisready AND NOT i.indisexclusion
        AND i.indnkeyatts = 2 AND i.indnatts = 2 AND am.amname = 'btree'
        AND idx.reloptions IS NULL AND i.indoption::int2vector = ARRAY[0,0]::int2vector
        AND i.indclass = ARRAY[
          (SELECT opc.oid FROM pg_opclass opc WHERE opc.opcmethod=am.oid AND opc.opcdefault
             AND opc.opcintype=(SELECT a.atttypid FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='S_STATUS')),
          (SELECT opc.oid FROM pg_opclass opc WHERE opc.opcmethod=am.oid AND opc.opcdefault
             AND opc.opcintype=(SELECT a.atttypid FROM pg_attribute a WHERE a.attrelid=tbl.oid AND a.attname='D_NEXT_RUN_AT'))
        ]::oidvector
        AND (SELECT array_agg(a.attname ORDER BY k.ord)
             FROM unnest(i.indkey) WITH ORDINALITY k(attnum,ord)
             JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum) = ARRAY['S_STATUS','D_NEXT_RUN_AT']
  ) THEN RAISE EXCEPTION 'Schedule-due index definition mismatch'; END IF;
END $$;

























