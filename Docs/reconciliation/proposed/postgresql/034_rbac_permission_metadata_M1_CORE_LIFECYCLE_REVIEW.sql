-- Core-lifecycle successor: removes only the artificial empty-ledger prerequisite.
-- Review only; not approved or executed. No migration history is created.
-- Candidate successor for PostgreSQL migration 034, scoped to M1 runtime needs.
-- NOT APPROVED / NOT EXECUTED. Exact target authorization, recovery, and owner
-- approval are required before use. Historical review artifact remains intact.
--
-- This transaction adds schema only. It preserves IDs and grant rows, assigns
-- unknown historical grants the non-authorizing status UNKNOWN and inactive
-- state, and leaves permission metadata and effective dates NULL. It creates
-- no catalogue rows, grants, ledger rows, or fabricated provenance.

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
        RAISE EXCEPTION '034 candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS_PERMISSION"') IS NULL
       OR to_regclass('public."PA_TICKETS_ROLE_PERMISSION"') IS NULL
 THEN
        RAISE EXCEPTION '034 candidate precondition failed: exact RBAC tables are required';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_PERMISSION"')
          AND a.attname = 'N_PERMISSION_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_ROLE_PERMISSION"')
          AND a.attname = 'N_ROLE_PERMISSION_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION '034 candidate precondition failed: expected bigint key columns are absent or incompatible';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
          AND ((table_name = 'PA_TICKETS_PERMISSION' AND column_name IN
                ('S_MODULE_CODE','S_ACTION_CODE','S_SCOPE_CLASS','S_SECURITY_CLASS','N_READ_ONLY'))
            OR (table_name = 'PA_TICKETS_ROLE_PERMISSION' AND column_name IN
                ('D_VALID_FROM','D_VALID_TO','S_EFFECTIVE_STATUS','S_PROVENANCE_REF',
                 'N_ACTIVE','D_UPDATED_AT','N_UPDATED_BY')))
    ) THEN
        RAISE EXCEPTION '034 candidate precondition failed: partial or out-of-band metadata exists';
    END IF;

    IF EXISTS (SELECT 1 FROM pg_catalog.pg_constraint c
               WHERE c.conname IN ('CK_PA_TICKETS_RP_EFFECTIVE_STATUS_M1',
                                   'CK_PA_TICKETS_RP_EFFECTIVE_DATES_M1',
                                   'CK_PA_TICKETS_RP_UNKNOWN_INACTIVE_M1'))
       OR to_regclass('public."IX_PA_TICKETS_RP_EFFECTIVE_FAIL_CLOSED_M1"') IS NOT NULL THEN
        RAISE EXCEPTION '034 candidate precondition failed: one or more candidate constraint/index names already exist';
    END IF;

END
$$;

ALTER TABLE public."PA_TICKETS_PERMISSION"
    ADD COLUMN "S_MODULE_CODE" varchar(80),
    ADD COLUMN "S_ACTION_CODE" varchar(50),
    ADD COLUMN "S_SCOPE_CLASS" varchar(30),
    ADD COLUMN "S_SECURITY_CLASS" varchar(40),
    ADD COLUMN "N_READ_ONLY" boolean;

ALTER TABLE public."PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN "D_VALID_FROM" timestamptz,
    ADD COLUMN "D_VALID_TO" timestamptz,
    ADD COLUMN "S_EFFECTIVE_STATUS" varchar(20) NOT NULL DEFAULT 'UNKNOWN',
    ADD COLUMN "S_PROVENANCE_REF" varchar(300),
    ADD COLUMN "N_ACTIVE" boolean NOT NULL DEFAULT false,
    ADD COLUMN "D_UPDATED_AT" timestamptz,
    ADD COLUMN "N_UPDATED_BY" bigint;

ALTER TABLE public."PA_TICKETS_ROLE_PERMISSION"
    ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_STATUS_M1"
        CHECK ("S_EFFECTIVE_STATUS" IN ('UNKNOWN','ACTIVE','EXPIRED','REVOKED')),
    ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_DATES_M1"
        CHECK ("D_VALID_TO" IS NULL OR "D_VALID_FROM" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    ADD CONSTRAINT "CK_PA_TICKETS_RP_UNKNOWN_INACTIVE_M1"
        CHECK ("S_EFFECTIVE_STATUS" <> 'UNKNOWN' OR "N_ACTIVE" = false);

CREATE INDEX "IX_PA_TICKETS_RP_EFFECTIVE_FAIL_CLOSED_M1"
    ON public."PA_TICKETS_ROLE_PERMISSION"
       ("N_ROLE_ID", "N_PERMISSION_ID", "S_EFFECTIVE_STATUS", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

COMMIT;
