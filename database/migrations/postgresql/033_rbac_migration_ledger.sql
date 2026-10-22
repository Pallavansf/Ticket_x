-- PA_TICKETS RBAC migration 033 (REVIEW DRAFT; do not execute without approval).
-- Purpose: establish an explicit migration ledger without inferring history from object names.
-- This file performs additive DDL only. It does not mark any existing migration as applied.
-- Prerequisite: target identity and an approved schema fingerprint have been captured separately.

BEGIN;

CREATE TABLE IF NOT EXISTS "PA_TICKETS_SCHEMA_MIGRATION" (
    "S_MIGRATION_ID" varchar(100) NOT NULL,
    "S_MIGRATION_SHA256" char(64) NOT NULL,
    "S_RELEASE_BASELINE" varchar(200) NOT NULL,
    "S_TARGET_FINGERPRINT" char(64) NOT NULL,
    "S_STATUS" varchar(20) NOT NULL DEFAULT 'APPLIED',
    "D_APPLIED_AT" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "S_APPROVED_BY" varchar(200) NOT NULL,
    "S_NOTES" varchar(2000),
    CONSTRAINT "PK_PA_TICKETS_SCHEMA_MIGRATION" PRIMARY KEY ("S_MIGRATION_ID"),
    CONSTRAINT "CK_PA_TICKETS_SCHEMA_MIGRATION_STATUS"
        CHECK ("S_STATUS" IN ('APPLIED','ROLLED_FORWARD','RETIRED')),
    CONSTRAINT "CK_PA_TICKETS_SCHEMA_MIGRATION_SHA"
        CHECK ("S_MIGRATION_SHA256" ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "CK_PA_TICKETS_SCHEMA_MIGRATION_TARGET_SHA"
        CHECK ("S_TARGET_FINGERPRINT" ~ '^[0-9a-f]{64}$')
);

CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_SCHEMA_MIGRATION_RELEASE"
    ON "PA_TICKETS_SCHEMA_MIGRATION" ("S_RELEASE_BASELINE", "D_APPLIED_AT");

DO $$
BEGIN
    IF to_regclass('"PA_TICKETS_SCHEMA_MIGRATION"') IS NULL THEN
        RAISE EXCEPTION 'RBAC migration 033 failed: migration ledger was not created';
    END IF;
END $$;

COMMIT;
