-- Review-only canonical permission definition needed by the M1 own-team
-- assignment path. This does not grant the permission to any role.
-- Run only after approved 034 metadata columns exist; never infer migration history.

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
        RAISE EXCEPTION 'TICKET_ASSIGN_TEAM catalogue candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS_PERMISSION"') IS NULL
       OR to_regclass('public."PA_TICKETS_PERMISSION_SEQ"') IS NULL THEN
        RAISE EXCEPTION 'TICKET_ASSIGN_TEAM catalogue candidate requires permission table and sequence';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'PA_TICKETS_PERMISSION'
          AND column_name = 'S_MODULE_CODE'
    ) THEN
        RAISE EXCEPTION 'TICKET_ASSIGN_TEAM catalogue candidate requires approved 034 metadata columns';
    END IF;

    IF (SELECT count(*) FROM public."PA_TICKETS_PERMISSION"
        WHERE "S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM') > 1 THEN
        RAISE EXCEPTION 'TICKET_ASSIGN_TEAM catalogue candidate found duplicate canonical codes';
    END IF;

    IF EXISTS (
        SELECT "S_PERMISSION_CODE" FROM public."PA_TICKETS_PERMISSION"
        WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE')
        GROUP BY "S_PERMISSION_CODE" HAVING count(*) <> 1
    ) OR (SELECT count(*) FROM public."PA_TICKETS_PERMISSION"
          WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE')) <> 2 THEN
        RAISE EXCEPTION 'M1 ticket catalogue candidate requires exactly one existing TICKET_READ and TICKET_UPDATE row';
    END IF;

    IF EXISTS (SELECT 1 FROM public."PA_TICKETS_PERMISSION"
               WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE','TICKET_ASSIGN_TEAM')
                 AND NOT "N_ACTIVE") THEN
        RAISE EXCEPTION 'M1 ticket permission candidate requires all three catalogue entries to be active';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."PA_TICKETS_PERMISSION"
        WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE','TICKET_ASSIGN_TEAM')
          AND (("S_PERMISSION_CODE" = 'TICKET_READ' AND
                ("S_MODULE_CODE" NOT IN ('TICKETS') OR "S_ACTION_CODE" NOT IN ('VIEW') OR
                 "S_SCOPE_CLASS" NOT IN ('RECORD') OR "S_SECURITY_CLASS" NOT IN ('READ_ONLY') OR
                 "N_READ_ONLY" IS DISTINCT FROM true)) OR
               ("S_PERMISSION_CODE" = 'TICKET_UPDATE' AND
                ("S_MODULE_CODE" NOT IN ('TICKETS') OR "S_ACTION_CODE" NOT IN ('EDIT') OR
                 "S_SCOPE_CLASS" NOT IN ('RECORD') OR "S_SECURITY_CLASS" NOT IN ('BUSINESS') OR
                 "N_READ_ONLY" IS DISTINCT FROM false)) OR
               ("S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM' AND
                ("S_MODULE_CODE" NOT IN ('TICKETS') OR "S_ACTION_CODE" NOT IN ('ASSIGN') OR
                 "S_SCOPE_CLASS" NOT IN ('RECORD') OR "S_SECURITY_CLASS" NOT IN ('BUSINESS') OR
                 "N_READ_ONLY" IS DISTINCT FROM false)))
    ) THEN
        RAISE EXCEPTION 'M1 ticket permission metadata conflicts with the approved source contract';
    END IF;
END
$$;

INSERT INTO public."PA_TICKETS_PERMISSION"
    ("N_PERMISSION_ID", "S_PERMISSION_CODE", "S_PERMISSION_NAME", "S_DESCRIPTION", "N_ACTIVE",
     "S_MODULE_CODE", "S_ACTION_CODE", "S_SCOPE_CLASS", "S_SECURITY_CLASS", "N_READ_ONLY")
SELECT nextval('public."PA_TICKETS_PERMISSION_SEQ"'::regclass),
       'TICKET_ASSIGN_TEAM', 'Assign tickets to an authorized team',
       'Assignment capability; effective permission, resource scope, and FR-021 rule are all required.',
       true, 'TICKETS', 'ASSIGN', 'RECORD', 'BUSINESS', false
WHERE NOT EXISTS (
    SELECT 1 FROM public."PA_TICKETS_PERMISSION"
    WHERE "S_PERMISSION_CODE" = 'TICKET_ASSIGN_TEAM'
);

UPDATE public."PA_TICKETS_PERMISSION"
SET "S_MODULE_CODE" = COALESCE("S_MODULE_CODE", 'TICKETS'),
    "S_ACTION_CODE" = COALESCE("S_ACTION_CODE", CASE "S_PERMISSION_CODE"
        WHEN 'TICKET_READ' THEN 'VIEW' WHEN 'TICKET_UPDATE' THEN 'EDIT' WHEN 'TICKET_ASSIGN_TEAM' THEN 'ASSIGN' END),
    "S_SCOPE_CLASS" = COALESCE("S_SCOPE_CLASS", 'RECORD'),
    "S_SECURITY_CLASS" = COALESCE("S_SECURITY_CLASS", CASE "S_PERMISSION_CODE"
        WHEN 'TICKET_READ' THEN 'READ_ONLY' WHEN 'TICKET_UPDATE' THEN 'BUSINESS' WHEN 'TICKET_ASSIGN_TEAM' THEN 'BUSINESS' END),
    "N_READ_ONLY" = COALESCE("N_READ_ONLY", CASE "S_PERMISSION_CODE"
        WHEN 'TICKET_READ' THEN true WHEN 'TICKET_UPDATE' THEN false WHEN 'TICKET_ASSIGN_TEAM' THEN false END)
WHERE "S_PERMISSION_CODE" IN ('TICKET_READ','TICKET_UPDATE','TICKET_ASSIGN_TEAM');

COMMIT;
