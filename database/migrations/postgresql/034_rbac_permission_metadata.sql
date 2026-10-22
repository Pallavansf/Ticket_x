-- PA_TICKETS RBAC migration 034 (REVIEW DRAFT; do not execute without approval).
-- Purpose: add configurable permission metadata and provenance-aware effective-date fields.
-- No data is backfilled. Unknown historical dates remain non-authorizing until reconciliation.
-- Prerequisite: 033 ledger baseline and an application release that treats UNKNOWN as denied.

BEGIN;

DO $$
BEGIN
    IF to_regclass('"PA_TICKETS_PERMISSION"') IS NULL
       OR to_regclass('"PA_TICKETS_ROLE_PERMISSION"') IS NULL THEN
        RAISE EXCEPTION 'RBAC migration 034 blocked: required RBAC relations are absent';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_name = 'PA_TICKETS_PERMISSION' AND column_name = 'N_PERMISSION_ID')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
                      WHERE table_name = 'PA_TICKETS_ROLE_PERMISSION' AND column_name = 'N_ROLE_PERMISSION_ID') THEN
        RAISE EXCEPTION 'RBAC migration 034 blocked: required key columns are absent';
    END IF;
END $$;

ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "S_MODULE_CODE" varchar(80),
    ADD COLUMN IF NOT EXISTS "S_ACTION_CODE" varchar(50),
    ADD COLUMN IF NOT EXISTS "S_SCOPE_CLASS" varchar(30),
    ADD COLUMN IF NOT EXISTS "S_SECURITY_CLASS" varchar(40),
    ADD COLUMN IF NOT EXISTS "N_READ_ONLY" boolean;

ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_VALID_FROM" timestamptz,
    ADD COLUMN IF NOT EXISTS "D_VALID_TO" timestamptz,
    ADD COLUMN IF NOT EXISTS "S_EFFECTIVE_STATUS" varchar(20) NOT NULL DEFAULT 'UNKNOWN',
    ADD COLUMN IF NOT EXISTS "S_PROVENANCE_REF" varchar(300),
    ADD COLUMN IF NOT EXISTS "N_ACTIVE" boolean,
    ADD COLUMN IF NOT EXISTS "D_UPDATED_AT" timestamptz,
    ADD COLUMN IF NOT EXISTS "N_UPDATED_BY" bigint;
UPDATE "PA_TICKETS_ROLE_PERMISSION"
   SET "N_ACTIVE" = false
 WHERE "N_ACTIVE" IS NULL;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_ACTIVE" SET DEFAULT false;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_ACTIVE" SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conname = 'CK_PA_TICKETS_RP_EFFECTIVE_STATUS'
                     AND conrelid = '"PA_TICKETS_ROLE_PERMISSION"'::regclass) THEN
        ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
            ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_STATUS"
            CHECK ("S_EFFECTIVE_STATUS" IN ('UNKNOWN','ACTIVE','EXPIRED','REVOKED'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conname = 'CK_PA_TICKETS_RP_EFFECTIVE_DATES'
                     AND conrelid = '"PA_TICKETS_ROLE_PERMISSION"'::regclass) THEN
        ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
            ADD CONSTRAINT "CK_PA_TICKETS_RP_EFFECTIVE_DATES"
            CHECK ("D_VALID_TO" IS NULL OR "D_VALID_FROM" IS NULL OR "D_VALID_TO" > "D_VALID_FROM");
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_RP_EFFECTIVE_FAIL_CLOSED"
    ON "PA_TICKETS_ROLE_PERMISSION"
       ("N_ROLE_ID", "N_PERMISSION_ID", "S_EFFECTIVE_STATUS", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "PA_TICKETS_PERMISSION"
               WHERE "S_MODULE_CODE" IS NOT NULL AND "S_ACTION_CODE" IS NULL) THEN
        RAISE EXCEPTION 'RBAC migration 034 failed: module metadata exists without action metadata';
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION"
               WHERE "S_EFFECTIVE_STATUS" = 'UNKNOWN' AND "N_ACTIVE" = true) THEN
        RAISE EXCEPTION 'RBAC migration 034 blocked: unknown effective grants are active; reconcile them explicitly first';
    END IF;
END $$;
COMMIT;



