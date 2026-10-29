-- Fixed-target read-only preflight for the four-artifact M1 schema batch.
-- Reports the permission catalogue and its allocator state without consuming
-- a sequence value or changing schema/data. Migration history is not inferred.

BEGIN;
SET TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '30s';
SET LOCAL lock_timeout = '3s';

DO $$
BEGIN
    IF current_database() <> 'pa_tickets_codex_e2e'
       OR current_user <> 'pa_tickets_codex'
       OR inet_server_addr() IS DISTINCT FROM '::1'::inet
       OR inet_server_port() <> 5432 THEN
        RAISE EXCEPTION 'M1 permission/sequence preflight target guard failed';
    END IF;
END
$$;

SELECT current_database() AS database_name,
       current_user AS role_name,
       inet_server_addr()::text AS server_address,
       inet_server_port() AS server_port,
       current_setting('transaction_read_only') AS transaction_read_only,
       'UNKNOWN'::text AS migration_history_status;

SELECT s.last_value AS persisted_last_value,
       s.is_called,
       ps.start_value,
       ps.increment_by,
       ps.cache_size,
       CASE WHEN NOT s.is_called THEN s.last_value::numeric
            ELSE s.last_value::numeric + ps.increment_by::numeric END AS conservative_next_value,
       (SELECT max(p."N_PERMISSION_ID") FROM public."PA_TICKETS_PERMISSION" AS p) AS maximum_permission_id,
       CASE WHEN (SELECT max(p."N_PERMISSION_ID") FROM public."PA_TICKETS_PERMISSION" AS p) IS NULL THEN false
            WHEN (CASE WHEN NOT s.is_called THEN s.last_value::numeric
                       ELSE s.last_value::numeric + ps.increment_by::numeric END)
                 <= (SELECT max(p."N_PERMISSION_ID")::numeric FROM public."PA_TICKETS_PERMISSION" AS p)
            THEN true ELSE false END AS next_value_conflicts_with_existing_key,
       pg_get_serial_sequence('public."PA_TICKETS_PERMISSION"', 'N_PERMISSION_ID') AS owned_sequence,
       pg_catalog.pg_get_expr(d.adbin, d.adrelid) AS column_default,
       ps.increment_by AS catalog_increment,
       ps.cache_size AS catalog_cache
FROM public."PA_TICKETS_PERMISSION_SEQ" AS s
CROSS JOIN pg_catalog.pg_sequences AS ps
JOIN pg_catalog.pg_class AS c ON c.relname = ps.sequencename
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace AND n.nspname = ps.schemaname
JOIN pg_catalog.pg_attribute AS a ON a.attrelid = 'public."PA_TICKETS_PERMISSION"'::regclass
                                 AND a.attname = 'N_PERMISSION_ID' AND NOT a.attisdropped
JOIN pg_catalog.pg_attrdef AS d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
WHERE ps.schemaname = 'public' AND ps.sequencename = 'PA_TICKETS_PERMISSION_SEQ';

SELECT p."N_PERMISSION_ID" AS permission_id,
       p."S_PERMISSION_CODE" AS permission_code,
       p."N_ACTIVE" AS active
FROM public."PA_TICKETS_PERMISSION" AS p
WHERE p."S_PERMISSION_CODE" IN ('TICKET_READ', 'TICKET_UPDATE', 'TICKET_ASSIGN_TEAM')
ORDER BY p."S_PERMISSION_CODE", p."N_PERMISSION_ID";

SELECT count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_READ') AS ticket_read_rows,
       count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_READ' AND p."N_ACTIVE") AS active_ticket_read_rows,
       count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_UPDATE') AS ticket_update_rows,
       count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_UPDATE' AND p."N_ACTIVE") AS active_ticket_update_rows,
       count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM') AS ticket_assign_team_rows,
       count(*) FILTER (WHERE p."S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM' AND p."N_ACTIVE") AS active_ticket_assign_team_rows
FROM public."PA_TICKETS_PERMISSION" AS p
WHERE p."S_PERMISSION_CODE" IN ('TICKET_READ', 'TICKET_UPDATE', 'TICKET_ASSIGN_TEAM');

ROLLBACK;
