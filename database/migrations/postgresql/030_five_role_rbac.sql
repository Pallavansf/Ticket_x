-- SUPERSEDED DRAFT — DO NOT EXECUTE.
-- The fixed permission allowlist does not implement configurable module/action RBAC.
-- See Docs/PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md for the replacement design and migration plan.
-- Additive PostgreSQL migration: five-role RBAC and effective-dated Admin Head delegation.
-- Existing role IDs, memberships, role-permission rows, and audit history are retained.

BEGIN;

CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_ADMIN_DELEGATION_SEQ" START WITH 1 INCREMENT BY 1;

CREATE TABLE IF NOT EXISTS "PA_TICKETS_ADMIN_HEAD_DELEGATION" (
    "N_ADMIN_DELEGATION_ID" bigint NOT NULL,
    "N_ADMIN_HEAD_USER_ID" bigint NOT NULL,
    "N_ADMIN_USER_ID" bigint NOT NULL,
    "D_VALID_FROM" timestamp with time zone NOT NULL,
    "D_VALID_TO" timestamp with time zone,
    "N_ACTIVE" boolean NOT NULL DEFAULT true,
    "D_CREATED_AT" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "N_CREATED_BY" bigint NOT NULL,
    CONSTRAINT "PK_PA_TICKETS_ADMIN_DELEG" PRIMARY KEY ("N_ADMIN_DELEGATION_ID"),
    CONSTRAINT "FK_PA_TICKETS_AHD_HEAD" FOREIGN KEY ("N_ADMIN_HEAD_USER_ID") REFERENCES "PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "FK_PA_TICKETS_AHD_ADMIN" FOREIGN KEY ("N_ADMIN_USER_ID") REFERENCES "PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "CK_PA_TICKETS_AHD_ACTIVE" CHECK ("N_ACTIVE" IN (true, false)),
    CONSTRAINT "CK_PA_TICKETS_AHD_DATES" CHECK ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    CONSTRAINT "UK_PA_TICKETS_AHD_START" UNIQUE ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "D_VALID_FROM")
);

CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_AHD_EFFECTIVE"
    ON "PA_TICKETS_ADMIN_HEAD_DELEGATION"
       ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

DO $$
DECLARE
    role_def record;
    generated_id bigint;
BEGIN
    IF (SELECT count(*) FROM "PA_TICKETS_PERMISSION"
         WHERE "N_ACTIVE" AND "S_PERMISSION_CODE" IN (
            'APPROVAL_ADMIN', 'APPROVAL_PUBLISH', 'APPROVAL_READ', 'AUDIT_EXPORT', 'AUDITOR_VIEW',
            'CATALOGUE_PUBLISH', 'CATEGORY_ADMIN', 'CATEGORY_READ', 'CLIENT_ADMIN',
            'COMMUNICATION_ADMIN', 'COMMUNICATION_PUBLISH', 'COMMUNICATION_READ', 'COMMUNICATION_RETIRE',
            'CONFIG_ADMIN', 'NOTIFICATION_ADMIN', 'NOTIFICATION_PUBLISH', 'NOTIFICATION_READ',
            'NOTIFICATION_RETIRE', 'REQUEST_TYPE_ADMIN', 'REQUEST_TYPE_READ', 'ROLE_ADMIN',
            'SERVICE_ADMIN', 'SERVICE_READ', 'SLA_ADMIN', 'SLA_PUBLISH', 'SLA_READ', 'SLA_RETIRE',
            'TICKET_ADMIN', 'TICKET_READ', 'TICKET_UPDATE', 'USER_ADMIN', 'WORKFLOW_ADMIN',
            'WORKFLOW_PUBLISH', 'WORKFLOW_READ')) <> 34 THEN
        RAISE EXCEPTION 'RBAC migration requires the reviewed active permission catalog';
    END IF;

    FOR role_def IN
        SELECT * FROM (VALUES
            ('USER', 'User', 'Standard ticket operations within authorized scope.'),
            ('ADMIN', 'Admin', 'Department-scoped administrative operations.'),
            ('SUPER_ADMIN', 'Super Admin', 'Organization-wide administration.'),
            ('ADMIN_HEAD', 'Admin Head', 'Admin capabilities and explicitly delegated Admin management.'),
            ('SPECTATOR', 'Spectator', 'Read-only access within authorized scope.')
        ) AS roles(code, name, description)
    LOOP
        IF NOT EXISTS (SELECT 1 FROM "PA_TICKETS_ROLE" WHERE "S_ROLE_CODE" = role_def.code) THEN
            LOOP
                generated_id := nextval('"PA_TICKETS_ROLE_SEQ"'::regclass);
                EXIT WHEN NOT EXISTS (
                    SELECT 1 FROM "PA_TICKETS_ROLE" WHERE "N_ROLE_ID" = generated_id);
            END LOOP;
            INSERT INTO "PA_TICKETS_ROLE"
                ("N_ROLE_ID", "S_ROLE_CODE", "S_ROLE_NAME", "S_DESCRIPTION", "N_SYSTEM_ROLE", "N_ACTIVE")
            VALUES
                (generated_id, role_def.code, role_def.name, role_def.description, true, true);
        END IF;
    END LOOP;
END $$;

DO $$
DECLARE
    grant_row record;
    generated_id bigint;
BEGIN
    FOR grant_row IN
        SELECT r."N_ROLE_ID", p."N_PERMISSION_ID"
          FROM "PA_TICKETS_ROLE" r
          JOIN "PA_TICKETS_PERMISSION" p
            ON (r."S_ROLE_CODE" = 'SUPER_ADMIN' AND p."N_ACTIVE")
            OR (r."S_ROLE_CODE" IN ('ADMIN', 'ADMIN_HEAD') AND p."S_PERMISSION_CODE" IN
                ('TICKET_READ', 'TICKET_UPDATE', 'USER_ADMIN', 'ROLE_ADMIN'))
            OR (r."S_ROLE_CODE" = 'USER' AND p."S_PERMISSION_CODE" IN ('TICKET_READ', 'TICKET_UPDATE'))
            OR (r."S_ROLE_CODE" = 'SPECTATOR' AND p."S_PERMISSION_CODE" IN
                ('APPROVAL_READ', 'AUDITOR_VIEW', 'CATEGORY_READ', 'COMMUNICATION_READ',
                 'NOTIFICATION_READ', 'REQUEST_TYPE_READ', 'SERVICE_READ', 'SLA_READ',
                 'TICKET_READ', 'WORKFLOW_READ'))
         WHERE NOT EXISTS (
             SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION" rp
              WHERE rp."N_ROLE_ID" = r."N_ROLE_ID" AND rp."N_PERMISSION_ID" = p."N_PERMISSION_ID")
    LOOP
        LOOP
            generated_id := nextval('"PA_TICKETS_ROLE_PERMISSION_SEQ"'::regclass);
            EXIT WHEN NOT EXISTS (
                SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION" WHERE "N_ROLE_PERMISSION_ID" = generated_id);
        END LOOP;
        INSERT INTO "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_PERMISSION_ID", "N_ROLE_ID", "N_PERMISSION_ID")
        VALUES (generated_id, grant_row."N_ROLE_ID", grant_row."N_PERMISSION_ID");
    END LOOP;
END $$;

COMMIT;
