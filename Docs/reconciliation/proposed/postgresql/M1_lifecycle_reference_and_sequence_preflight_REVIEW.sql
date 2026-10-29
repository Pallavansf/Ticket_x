-- Fixed-target, read-only lifecycle data and sequence preflight.
-- Review only; operation is not enabled or approved for execution.
-- It reads state and metadata; it never calls nextval/setval/restart/ALTER.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '10s';
SET LOCAL lock_timeout = '2s';
SET LOCAL search_path TO public, pg_catalog;

DO $$
BEGIN
    IF current_database() <> 'pa_tickets_codex_e2e'
       OR current_user <> 'pa_tickets_codex'
       OR inet_server_addr() IS DISTINCT FROM '::1'::inet
       OR inet_server_port() <> 5432
       OR current_setting('transaction_read_only') <> 'on' THEN
        RAISE EXCEPTION 'M1 sequence inspection target/read-only guard failed';
    END IF;
END
$$;

-- Only the identity/configuration fields required by the selected lifecycle test.
SELECT "N_USER_ID", "N_ACTIVE", "N_DEPARTMENT_ID", "N_TEAM_ID"
FROM public."PA_TICKETS_USER"
WHERE "N_USER_ID" = 1001;

SELECT "N_STATUS_ID", "S_STATUS_CODE", "N_ACTIVE", "N_IS_INITIAL"
FROM public."PA_TICKETS_STATUS"
WHERE "S_STATUS_CODE" IN ('NEW','TRIAGE','ASSIGNED')
ORDER BY "S_STATUS_CODE";

SELECT "S_FROM_STATUS_CODE", "S_TO_STATUS_CODE", "S_CONDITION", "N_ALLOWED"
FROM public."PA_TICKETS_STATUS_TRANSITION"
WHERE ("S_FROM_STATUS_CODE", "S_TO_STATUS_CODE") IN (('NEW','TRIAGE'),('TRIAGE','ASSIGNED'))
ORDER BY "S_FROM_STATUS_CODE", "S_TO_STATUS_CODE";

SELECT "N_PRIORITY_ID", "S_PRIORITY_CODE", "N_ACTIVE", "N_IS_DEFAULT"
FROM public."PA_TICKETS_PRIORITY"
WHERE "N_PRIORITY_ID" = 2 OR "N_IS_DEFAULT"
ORDER BY "N_PRIORITY_ID";
WITH sequence_state(sequence_name, last_value, is_called) AS (
    SELECT 'PA_TICKETS_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_TICKET_NO_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_TICKET_NO_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_WORK_ITEM_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_WORK_ITEM_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_EVENT_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_EVENT_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_AUDIT_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_AUDIT_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_ASSIGNMENT_SEQ', s.last_value, s.is_called FROM public."PA_TICKETS_ASSIGNMENT_SEQ" AS s
    UNION ALL SELECT 'PA_TICKETS_AUDIT_MAINTENANCE_N_MAINTENANCE_ID_seq', s.last_value, s.is_called
      FROM public."PA_TICKETS_AUDIT_MAINTENANCE_N_MAINTENANCE_ID_seq" AS s
), key_max(sequence_name, relation_name, column_name, maximum_key) AS (
    SELECT 'PA_TICKETS_SEQ', 'PA_TICKETS', 'N_TICKET_ID', max("N_TICKET_ID")::bigint FROM public."PA_TICKETS"
    UNION ALL
    SELECT 'PA_TICKETS_TICKET_NO_SEQ', 'PA_TICKETS', 'S_TICKET_NO',
           max((regexp_match("S_TICKET_NO", '^[A-Z][A-Z0-9]*-([0-9]+)$'))[1]::bigint)
      FROM public."PA_TICKETS"
    UNION ALL
    SELECT 'PA_TICKETS_WORK_ITEM_SEQ', 'PA_TICKETS_WORK_ITEM', 'N_WORK_ITEM_ID', max("N_WORK_ITEM_ID")::bigint FROM public."PA_TICKETS_WORK_ITEM"
    UNION ALL
    SELECT 'PA_TICKETS_EVENT_SEQ', 'PA_TICKETS_EVENT', 'N_EVENT_ID', max("N_EVENT_ID")::bigint FROM public."PA_TICKETS_EVENT"
    UNION ALL
    SELECT 'PA_TICKETS_AUDIT_SEQ', 'PA_TICKETS_AUDIT', 'N_AUDIT_ID', max("N_AUDIT_ID")::bigint FROM public."PA_TICKETS_AUDIT"
    UNION ALL
    SELECT 'PA_TICKETS_ASSIGNMENT_SEQ', 'PA_TICKETS_ASSIGNMENT', 'N_ASSIGNMENT_ID', max("N_ASSIGNMENT_ID")::bigint FROM public."PA_TICKETS_ASSIGNMENT"
    UNION ALL
    SELECT 'PA_TICKETS_AUDIT_MAINTENANCE_N_MAINTENANCE_ID_seq', 'PA_TICKETS_AUDIT_MAINTENANCE', 'N_MAINTENANCE_ID', max("N_MAINTENANCE_ID")::bigint FROM public."PA_TICKETS_AUDIT_MAINTENANCE"
)
SELECT st.sequence_name,
       st.last_value AS persisted_last_value,
       st.is_called,
       ps.start_value,
       ps.increment_by,
       ps.cache_size,
       CASE WHEN st.is_called THEN st.last_value::numeric + ps.increment_by::numeric ELSE st.last_value::numeric END AS conservative_next_value,
       km.relation_name,
       km.column_name,
       km.maximum_key,
       CASE WHEN km.maximum_key IS NULL THEN false
            WHEN (CASE WHEN st.is_called THEN st.last_value::numeric + ps.increment_by::numeric ELSE st.last_value::numeric END) <= km.maximum_key THEN true
            ELSE false END AS next_value_conflicts_with_existing_key,
       pg_get_expr(ad.adbin, ad.adrelid) AS column_default,
       owner_table.relname AS owned_by_relation,
       owner_column.attname AS owned_by_column
FROM sequence_state st
JOIN pg_catalog.pg_sequences ps
  ON ps.schemaname = 'public' AND ps.sequencename = st.sequence_name
JOIN key_max km ON km.sequence_name = st.sequence_name
JOIN pg_catalog.pg_class seq ON seq.oid = to_regclass(format('public.%I', st.sequence_name))
LEFT JOIN pg_catalog.pg_depend dep
  ON dep.classid = 'pg_catalog.pg_class'::regclass AND dep.objid = seq.oid
 AND dep.refclassid = 'pg_catalog.pg_class'::regclass AND dep.deptype IN ('a','i')
LEFT JOIN pg_catalog.pg_class owner_table ON owner_table.oid = dep.refobjid
LEFT JOIN pg_catalog.pg_attribute owner_column
  ON owner_column.attrelid = dep.refobjid AND owner_column.attnum = dep.refobjsubid
LEFT JOIN pg_catalog.pg_class map_table
  ON map_table.relnamespace = 'public'::regnamespace AND map_table.relname = km.relation_name
LEFT JOIN pg_catalog.pg_attribute map_column
  ON map_column.attrelid = map_table.oid AND map_column.attname = km.column_name AND NOT map_column.attisdropped
LEFT JOIN pg_catalog.pg_attrdef ad
  ON ad.adrelid = map_table.oid AND ad.adnum = map_column.attnum
ORDER BY st.sequence_name;

ROLLBACK;
