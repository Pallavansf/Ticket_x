-- M1 replacement proposal for PostgreSQL migration 034.
-- REVIEW ONLY. This file is outside the executable migration directory.
-- It intentionally aborts before any DDL. Remove no guard without separate
-- owner approval, exact-byte review, target authorization, and recovery plan.
--
-- Data policy: preserve existing permission/grant rows and IDs. New permission
-- metadata stays NULL until explicitly reconciled. Legacy grants receive only
-- fail-closed schema defaults: UNKNOWN status and inactive=false. No effective
-- dates, provenance, permission metadata, grants, or catalogue rows are
-- backfilled. Migration history remains UNKNOWN; this file inserts no ledger
-- records. It never resets, advances, or reads sequence allocation state.

BEGIN;

DO $$
BEGIN
    RAISE EXCEPTION 'M1 RBAC metadata review artifact; execution is prohibited';
END
$$;

-- Proposed body for a separately reviewed and authorized migration:
-- prerequisite: public.PA_TICKETS_SCHEMA_MIGRATION exists as the empty/new
-- ledger structure established under D09; no historical applied rows are
-- required or permitted by this proposal. The ledger's presence is not proof
-- that any earlier migration ran.
DO $$
BEGIN
    IF to_regclass('public."PA_TICKETS_PERMISSION"') IS NULL
       OR to_regclass('public."PA_TICKETS_ROLE_PERMISSION"') IS NULL
       OR to_regclass('public."PA_TICKETS_SCHEMA_MIGRATION"') IS NULL THEN
        RAISE EXCEPTION '034 M1 proposal blocked: required exact-case public relations are absent';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
        JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = 'PA_TICKETS_PERMISSION'
          AND a.attname = 'N_PERMISSION_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        JOIN pg_catalog.pg_class c ON c.oid = a.attrelid
        JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = 'PA_TICKETS_ROLE_PERMISSION'
          AND a.attname = 'N_ROLE_PERMISSION_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION '034 M1 proposal blocked: expected bigint primary-key columns are absent or incompatible';
    END IF;

    -- The captured target had none of the proposed columns. Reject partial or
    -- out-of-band deployments rather than interpreting existing values or
    -- silently skipping an incompatible column with IF NOT EXISTS.
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND ((table_name = 'PA_TICKETS_PERMISSION' AND column_name IN
                ('S_MODULE_CODE','S_ACTION_CODE','S_SCOPE_CLASS','S_SECURITY_CLASS','N_READ_ONLY'))
            OR (table_name = 'PA_TICKETS_ROLE_PERMISSION' AND column_name IN
                ('D_VALID_FROM','D_VALID_TO','S_EFFECTIVE_STATUS','S_PROVENANCE_REF',
                 'N_ACTIVE','D_UPDATED_AT','N_UPDATED_BY')))
    ) THEN
        RAISE EXCEPTION '034 M1 proposal blocked: partial or out-of-band metadata exists; reconcile it before proceeding';
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

-- If any partially deployed column was nullable, don't silently rewrite its
-- existing facts: the SET NOT NULL will fail and roll back the whole proposal.
ALTER TABLE public."PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "S_EFFECTIVE_STATUS" SET DEFAULT 'UNKNOWN',
    ALTER COLUMN "S_EFFECTIVE_STATUS" SET NOT NULL,
    ALTER COLUMN "N_ACTIVE" SET DEFAULT false,
    ALTER COLUMN "N_ACTIVE" SET NOT NULL;

ALTER TABLE public."PA_TICKETS_ROLE_PERMISSION"
    ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_STATUS_M1"
        CHECK ("S_EFFECTIVE_STATUS" IN ('UNKNOWN','ACTIVE','EXPIRED','REVOKED')),
    ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_DATES_M1"
        CHECK ("D_VALID_TO" IS NULL OR "D_VALID_FROM" IS NULL OR "D_VALID_TO" > "D_VALID_FROM");

CREATE INDEX "IX_PA_TICKETS_RP_EFFECTIVE_FAIL_CLOSED_M1"
    ON public."PA_TICKETS_ROLE_PERMISSION"
       ("N_ROLE_ID", "N_PERMISSION_ID", "S_EFFECTIVE_STATUS", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

COMMIT;
