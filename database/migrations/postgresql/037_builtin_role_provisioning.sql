-- PA_TICKETS RBAC migration 037 (REVIEW DRAFT; do not execute without approval).
-- Purpose: duplicate-safe provisioning of the five built-in role identities only.
-- It preserves existing IDs and inserts no memberships, permissions, scopes, users, or grants.
-- Execution requires a separate approved session setting; this file is never safe by discovery alone.

BEGIN;

DO $$
BEGIN
    IF current_setting('pa_tickets.allow_builtin_role_provisioning', true) <> 'approved' THEN
        RAISE EXCEPTION 'RBAC migration 037 blocked: explicit role-provisioning approval is required';
    END IF;
    IF to_regclass('"PA_TICKETS_ROLE"') IS NULL THEN
        RAISE EXCEPTION 'RBAC migration 037 blocked: role relation is absent';
    END IF;
    IF to_regclass('"PA_TICKETS_ROLE_SEQ"') IS NULL THEN
        RAISE EXCEPTION 'RBAC migration 037 blocked: role sequence is absent';
    END IF;
END $$;

DO $$
DECLARE
    role_def record;
    role_id bigint;
BEGIN
    IF EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE"
         WHERE UPPER("S_ROLE_CODE") IN ('USER','ADMIN','SUPER_ADMIN','ADMIN_HEAD','SPECTATOR')
         GROUP BY UPPER("S_ROLE_CODE") HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'RBAC migration 037 blocked: duplicate built-in role identity';
    END IF;

    FOR role_def IN
        SELECT * FROM (VALUES
            ('USER', 'User', 'Standard ticket operations within an explicitly assigned scope.'),
            ('ADMIN', 'Admin', 'Department-scoped administration within an explicitly assigned scope.'),
            ('SUPER_ADMIN', 'Super Admin', 'Organization-wide administration only with explicit permission and scope.'),
            ('ADMIN_HEAD', 'Admin Head', 'Admin capabilities plus explicitly delegated administration.'),
            ('SPECTATOR', 'Spectator', 'Read-only access within an explicitly assigned scope.')
        ) AS roles(code, name, description)
    LOOP
        IF NOT EXISTS (SELECT 1 FROM "PA_TICKETS_ROLE" WHERE "S_ROLE_CODE" = role_def.code) THEN
            role_id := nextval('"PA_TICKETS_ROLE_SEQ"'::regclass);
            INSERT INTO "PA_TICKETS_ROLE"
                ("N_ROLE_ID", "S_ROLE_CODE", "S_ROLE_NAME", "S_DESCRIPTION", "N_SYSTEM_ROLE", "N_ACTIVE", "N_CREATED_BY")
            VALUES (role_id, role_def.code, role_def.name, role_def.description, true, true, 0);
        END IF;
    END LOOP;
END $$;

DO $$
BEGIN
    IF (SELECT COUNT(*) FROM "PA_TICKETS_ROLE"
        WHERE "S_ROLE_CODE" IN ('USER','ADMIN','SUPER_ADMIN','ADMIN_HEAD','SPECTATOR')) <> 5 THEN
        RAISE EXCEPTION 'RBAC migration 037 failed: five built-in role identities are not present';
    END IF;
END $$;

COMMIT;
