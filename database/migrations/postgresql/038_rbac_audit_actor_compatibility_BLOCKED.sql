-- PA_TICKETS RBAC migration 038 (BLOCKED DESIGN GATE; intentionally non-executable).
-- Historical audit rows must not be attributed to an invented user or current administrator.
-- The deployed target has a non-null actor column without a stable actor-type contract.
-- Before any audit DDL, approve the application contract for LEGACY_UNKNOWN/SYSTEM actors,
-- nullable actor IDs, FK behavior, writer/reader compatibility, and chain-integrity handling.
-- This file exists to prevent accidental execution while that decision remains unresolved.

BEGIN;
DO $$
BEGIN
    RAISE EXCEPTION 'RBAC migration 038 blocked: audit actor compatibility decision is unresolved; no DDL is permitted';
END $$;
ROLLBACK;
