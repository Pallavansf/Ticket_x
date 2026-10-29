-- M1 additive bootstrap for the two canonical ticket permissions required by
-- the separately approved catalogue artifact. This is a distinct, review-only
-- artifact; it creates no grants, memberships, scopes, or migration history.
-- Execute only after 034 adds metadata columns and after exact hash/target and
-- sequence preconditions have been verified.

BEGIN;
SET LOCAL search_path TO public, pg_catalog;
SET LOCAL lock_timeout = '3s';
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF current_database() <> 'pa_tickets_codex_e2e'
       OR current_user <> 'pa_tickets_codex'
       OR inet_server_addr() IS DISTINCT FROM '::1'::inet
       OR inet_server_port() <> 5432 THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap target guard failed';
    END IF;
END
$$;

LOCK TABLE public."PA_TICKETS_PERMISSION" IN SHARE ROW EXCLUSIVE MODE;

DO $$
DECLARE
    v_last_value bigint;
    v_is_called boolean;
    v_increment bigint;
    v_cache bigint;
    v_cycle boolean;
    v_max_id bigint;
    v_missing_count integer;
    v_next numeric;
    v_default text;
BEGIN
    IF to_regclass('public."PA_TICKETS_PERMISSION"') IS NULL
       OR to_regclass('public."PA_TICKETS_PERMISSION_SEQ"') IS NULL THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires the permission table and sequence';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND a.attname = 'N_PERMISSION_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND a.attname = 'S_PERMISSION_CODE' AND a.atttypid = 'varchar(150)'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND a.attname = 'N_ACTIVE' AND a.atttypid = 'boolean'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap found incompatible key/code/active columns';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_constraint c
        JOIN pg_catalog.pg_attribute a ON a.attrelid = c.conrelid AND a.attname = 'N_PERMISSION_ID'
        WHERE c.conrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND c.contype IN ('p','u') AND c.conkey = ARRAY[a.attnum]::smallint[]
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires a unique permission ID key';
    END IF;

    IF EXISTS (
        SELECT expected.column_name
        FROM (VALUES
            ('S_MODULE_CODE', 'character varying', 80),
            ('S_ACTION_CODE', 'character varying', 50),
            ('S_SCOPE_CLASS', 'character varying', 30),
            ('S_SECURITY_CLASS', 'character varying', 40),
            ('N_READ_ONLY', 'boolean', NULL::integer)
        ) AS expected(column_name, data_type, character_maximum_length)
        LEFT JOIN information_schema.columns actual
          ON actual.table_schema = 'public'
         AND actual.table_name = 'PA_TICKETS_PERMISSION'
         AND actual.column_name = expected.column_name
        WHERE actual.column_name IS NULL
           OR actual.data_type IS DISTINCT FROM expected.data_type
           OR actual.character_maximum_length IS DISTINCT FROM expected.character_maximum_length
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires the exact 034 metadata column definitions';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_constraint c
        JOIN pg_catalog.pg_attribute a ON a.attrelid = c.conrelid AND a.attname = 'S_PERMISSION_CODE'
        WHERE c.conrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND c.contype IN ('p','u') AND c.conkey = ARRAY[a.attnum]::smallint[]
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires a unique permission-code key';
    END IF;

    SELECT pg_catalog.pg_get_expr(d.adbin, d.adrelid) INTO v_default
    FROM pg_catalog.pg_attribute a
    JOIN pg_catalog.pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
    WHERE a.attrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
      AND a.attname = 'N_PERMISSION_ID' AND NOT a.attisdropped;
    IF v_default IS DISTINCT FROM 'nextval(''"PA_TICKETS_PERMISSION_SEQ"''::regclass)' THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires the approved permission sequence default';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."PA_TICKETS_PERMISSION"
        WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE','TICKET_ASSIGN_TEAM')
        GROUP BY "S_PERMISSION_CODE" HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap found duplicate canonical permission codes';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."PA_TICKETS_PERMISSION" p
        WHERE p."S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE','TICKET_ASSIGN_TEAM')
          AND (p."N_ACTIVE" IS DISTINCT FROM true
            OR (p."S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE') AND (
                   (p."S_MODULE_CODE" IS NOT NULL AND p."S_MODULE_CODE" IS DISTINCT FROM 'TICKETS')
                OR (p."S_ACTION_CODE" IS NOT NULL AND p."S_ACTION_CODE" IS DISTINCT FROM
                    CASE p."S_PERMISSION_CODE" WHEN 'TICKET_READ' THEN 'VIEW' ELSE 'EDIT' END)
                OR (p."S_SCOPE_CLASS" IS NOT NULL AND p."S_SCOPE_CLASS" IS DISTINCT FROM 'RECORD')
                OR (p."S_SECURITY_CLASS" IS NOT NULL AND p."S_SECURITY_CLASS" IS DISTINCT FROM
                    CASE p."S_PERMISSION_CODE" WHEN 'TICKET_READ' THEN 'READ_ONLY' ELSE 'BUSINESS' END)
                OR (p."N_READ_ONLY" IS NOT NULL AND p."N_READ_ONLY" IS DISTINCT FROM
                    (p."S_PERMISSION_CODE" = 'TICKET_READ'))))
            OR (p."S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM' AND (
                   p."S_MODULE_CODE" IS DISTINCT FROM 'TICKETS'
                OR p."S_ACTION_CODE" IS DISTINCT FROM 'ASSIGN'
                OR p."S_SCOPE_CLASS" IS DISTINCT FROM 'RECORD'
                OR p."S_SECURITY_CLASS" IS DISTINCT FROM 'BUSINESS'
                OR p."N_READ_ONLY" IS DISTINCT FROM false)))
    ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap found inactive or conflicting canonical permission metadata';
    END IF;

    SELECT last_value, is_called INTO v_last_value, v_is_called
    FROM public."PA_TICKETS_PERMISSION_SEQ";
    SELECT increment_by, cache_size, cycle INTO v_increment, v_cache, v_cycle
    FROM pg_catalog.pg_sequences
    WHERE schemaname = 'public' AND sequencename = 'PA_TICKETS_PERMISSION_SEQ';
    IF v_increment IS NULL OR v_increment <= 0 OR v_cache IS DISTINCT FROM 1 OR v_cycle IS DISTINCT FROM false THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap requires a positive, noncycling, cache-1 sequence';
    END IF;

    SELECT max("N_PERMISSION_ID") INTO v_max_id FROM public."PA_TICKETS_PERMISSION";
    SELECT count(*)::integer INTO v_missing_count
    FROM (VALUES ('TICKET_READ'), ('TICKET_UPDATE'), ('TICKET_ASSIGN_TEAM')) AS expected(code)
    WHERE NOT EXISTS (SELECT 1 FROM public."PA_TICKETS_PERMISSION" p WHERE p."S_PERMISSION_CODE" = expected.code);
    v_next := CASE WHEN NOT v_is_called THEN v_last_value::numeric
                   ELSE v_last_value::numeric + v_increment::numeric END;
    IF v_max_id IS NOT NULL AND v_missing_count > 0
       AND v_next + ((v_missing_count - 1)::numeric * v_increment::numeric) <= v_max_id::numeric THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap detected permission sequence collision risk';
    END IF;
END
$$;

INSERT INTO public."PA_TICKETS_PERMISSION"
    ("N_PERMISSION_ID", "S_PERMISSION_CODE", "S_PERMISSION_NAME", "S_DESCRIPTION", "N_ACTIVE",
     "S_MODULE_CODE", "S_ACTION_CODE", "S_SCOPE_CLASS", "S_SECURITY_CLASS", "N_READ_ONLY")
SELECT nextval('public."PA_TICKETS_PERMISSION_SEQ"'::regclass),
       'TICKET_READ', 'View tickets', 'Read tickets subject to effective permission and resource scope.',
       true, 'TICKETS', 'VIEW', 'RECORD', 'READ_ONLY', true
WHERE NOT EXISTS (SELECT 1 FROM public."PA_TICKETS_PERMISSION" WHERE "S_PERMISSION_CODE" = 'TICKET_READ');

INSERT INTO public."PA_TICKETS_PERMISSION"
    ("N_PERMISSION_ID", "S_PERMISSION_CODE", "S_PERMISSION_NAME", "S_DESCRIPTION", "N_ACTIVE",
     "S_MODULE_CODE", "S_ACTION_CODE", "S_SCOPE_CLASS", "S_SECURITY_CLASS", "N_READ_ONLY")
SELECT nextval('public."PA_TICKETS_PERMISSION_SEQ"'::regclass),
       'TICKET_UPDATE', 'Update tickets', 'Update tickets subject to effective permission and resource scope.',
       true, 'TICKETS', 'EDIT', 'RECORD', 'BUSINESS', false
WHERE NOT EXISTS (SELECT 1 FROM public."PA_TICKETS_PERMISSION" WHERE "S_PERMISSION_CODE" = 'TICKET_UPDATE');

DO $$
BEGIN
    IF (SELECT count(*) FROM public."PA_TICKETS_PERMISSION" WHERE "S_PERMISSION_CODE" = 'TICKET_READ') <> 1
       OR (SELECT count(*) FROM public."PA_TICKETS_PERMISSION" WHERE "S_PERMISSION_CODE" = 'TICKET_UPDATE') <> 1
       OR EXISTS (
           SELECT 1 FROM public."PA_TICKETS_PERMISSION" p
           WHERE p."S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE')
             AND (p."N_ACTIVE" IS DISTINCT FROM true OR p."S_MODULE_CODE" IS DISTINCT FROM 'TICKETS'
               OR p."S_ACTION_CODE" IS DISTINCT FROM CASE p."S_PERMISSION_CODE" WHEN 'TICKET_READ' THEN 'VIEW' ELSE 'EDIT' END
               OR p."S_SCOPE_CLASS" IS DISTINCT FROM 'RECORD'
               OR p."S_SECURITY_CLASS" IS DISTINCT FROM CASE p."S_PERMISSION_CODE" WHEN 'TICKET_READ' THEN 'READ_ONLY' ELSE 'BUSINESS' END
               OR p."N_READ_ONLY" IS DISTINCT FROM (p."S_PERMISSION_CODE" = 'TICKET_READ'))
       ) THEN
        RAISE EXCEPTION 'M1 ticket-permission bootstrap postcondition failed';
    END IF;
END
$$;

COMMIT;
