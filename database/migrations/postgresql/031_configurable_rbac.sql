-- PA TICKETS additive PostgreSQL migration 031:
-- configurable permission metadata, effective grants, and explicit membership scopes.
-- This migration preserves existing role/user IDs, memberships, mappings, and audit rows.

BEGIN;

DO $rbac_identity_preflight$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE"
         GROUP BY UPPER("S_ROLE_CODE") HAVING COUNT(*) > 1
    ) OR EXISTS (
        SELECT 1 FROM "PA_TICKETS_PERMISSION"
         GROUP BY UPPER("S_PERMISSION_CODE") HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'RBAC migration blocked: duplicate role or permission identities differ only by case';
    END IF;
    IF EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE" WHERE "S_ROLE_CODE" <> UPPER("S_ROLE_CODE")
    ) OR EXISTS (
        SELECT 1 FROM "PA_TICKETS_PERMISSION" WHERE "S_PERMISSION_CODE" <> UPPER("S_PERMISSION_CODE")
    ) THEN
        RAISE EXCEPTION 'RBAC migration blocked: role and permission codes must be canonical uppercase';
    END IF;
    IF EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE"
         WHERE UPPER("S_ROLE_CODE") IN ('USER', 'ADMIN', 'SUPER_ADMIN', 'ADMIN_HEAD', 'SPECTATOR')
           AND "S_ROLE_CODE" <> UPPER("S_ROLE_CODE")
    ) OR EXISTS (
        SELECT 1 FROM "PA_TICKETS_ROLE"
         WHERE REGEXP_REPLACE(UPPER("S_ROLE_CODE"), '[^A-Z0-9]', '', 'g')
               IN ('USER', 'ADMIN', 'SUPERADMIN', 'ADMINHEAD', 'SPECTATOR')
           AND "S_ROLE_CODE" NOT IN ('USER', 'ADMIN', 'SUPER_ADMIN', 'ADMIN_HEAD', 'SPECTATOR')
    ) THEN
        RAISE EXCEPTION 'RBAC migration blocked: a built-in role identity has a non-canonical alias';
    END IF;
END
$rbac_identity_preflight$;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_PA_TICKETS_ROLE_CODE_CANONICAL"
    ON "PA_TICKETS_ROLE" (UPPER("S_ROLE_CODE"));
CREATE UNIQUE INDEX IF NOT EXISTS "UX_PA_TICKETS_PERMISSION_CODE_CANONICAL"
    ON "PA_TICKETS_PERMISSION" (UPPER("S_PERMISSION_CODE"));

CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_ROLE_SCOPE_SEQ" START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_ADMIN_DELEGATION_SEQ" START WITH 1 INCREMENT BY 1;

ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "S_MODULE_CODE" varchar(80);
ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "S_ACTION_CODE" varchar(50);
ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "S_SCOPE_CLASS" varchar(30);
ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "S_SECURITY_CLASS" varchar(40);
ALTER TABLE "PA_TICKETS_PERMISSION"
    ADD COLUMN IF NOT EXISTS "N_READ_ONLY" boolean NOT NULL DEFAULT false;
UPDATE "PA_TICKETS_PERMISSION"
   SET "N_READ_ONLY" = COALESCE("N_READ_ONLY", false)
 WHERE "N_READ_ONLY" IS NULL;
ALTER TABLE "PA_TICKETS_PERMISSION"
    ALTER COLUMN "N_READ_ONLY" SET DEFAULT false;
ALTER TABLE "PA_TICKETS_PERMISSION"
    ALTER COLUMN "N_READ_ONLY" SET NOT NULL;

ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_VALID_FROM" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_VALID_TO" timestamp with time zone;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "N_ACTIVE" boolean NOT NULL DEFAULT true;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "D_UPDATED_AT" timestamp with time zone;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ADD COLUMN IF NOT EXISTS "N_UPDATED_BY" bigint NOT NULL DEFAULT 0;
UPDATE "PA_TICKETS_ROLE_PERMISSION"
   SET "D_VALID_FROM" = COALESCE("D_VALID_FROM", "D_CREATED_AT", CURRENT_TIMESTAMP),
       "N_ACTIVE" = COALESCE("N_ACTIVE", true),
       "N_UPDATED_BY" = COALESCE("N_UPDATED_BY", 0);
UPDATE "PA_TICKETS_ROLE_PERMISSION"
   SET "D_UPDATED_AT" = COALESCE("D_UPDATED_AT", "D_VALID_FROM");
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "D_VALID_FROM" SET NOT NULL;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_ACTIVE" SET DEFAULT true;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_ACTIVE" SET NOT NULL;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_UPDATED_BY" SET DEFAULT 0;
ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    ALTER COLUMN "N_UPDATED_BY" SET NOT NULL;
DO $role_permission_constraints$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'CK_PA_TICKETS_RP_DATES'
           AND conrelid = '"PA_TICKETS_ROLE_PERMISSION"'::regclass
    ) THEN
        ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
            ADD CONSTRAINT "CK_PA_TICKETS_RP_DATES"
            CHECK ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM");
    END IF;
END
$role_permission_constraints$;

ALTER TABLE "PA_TICKETS_ROLE_PERMISSION"
    DROP CONSTRAINT IF EXISTS "UK_PA_TICKETS_RP";
CREATE UNIQUE INDEX IF NOT EXISTS "UK_PA_TICKETS_RP_ACTIVE"
    ON "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID", "N_PERMISSION_ID")
    WHERE "N_ACTIVE";
CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_RP_EFFECTIVE"
    ON "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

CREATE TABLE IF NOT EXISTS "PA_TICKETS_USER_ROLE_SCOPE" (
    "N_ROLE_SCOPE_ID" bigint NOT NULL,
    "N_USER_ROLE_ID" bigint NOT NULL,
    "S_SCOPE_TYPE" varchar(30) NOT NULL,
    "N_ORGANIZATION_ID" bigint,
    "N_DEPARTMENT_ID" bigint,
    "N_TEAM_ID" bigint,
    "N_CLIENT_ID" bigint,
    "S_RESOURCE_TYPE" varchar(100),
    "S_RESOURCE_ID" varchar(300),
    "D_VALID_FROM" timestamp with time zone NOT NULL,
    "D_VALID_TO" timestamp with time zone,
    "N_ACTIVE" boolean NOT NULL DEFAULT true,
    "D_CREATED_AT" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "N_CREATED_BY" bigint NOT NULL,
    CONSTRAINT "PK_PA_TICKETS_ROLE_SCOPE" PRIMARY KEY ("N_ROLE_SCOPE_ID"),
    CONSTRAINT "FK_PA_TICKETS_RSC_USER_ROLE" FOREIGN KEY ("N_USER_ROLE_ID")
        REFERENCES "PA_TICKETS_USER_ROLE" ("N_USER_ROLE_ID"),
    CONSTRAINT "FK_PA_TICKETS_RSC_DEPARTMENT" FOREIGN KEY ("N_DEPARTMENT_ID")
        REFERENCES "PA_TICKETS_DEPARTMENT" ("N_DEPARTMENT_ID"),
    CONSTRAINT "FK_PA_TICKETS_RSC_TEAM" FOREIGN KEY ("N_TEAM_ID")
        REFERENCES "PA_TICKETS_TEAM" ("N_TEAM_ID"),
    CONSTRAINT "FK_PA_TICKETS_RSC_CLIENT" FOREIGN KEY ("N_CLIENT_ID")
        REFERENCES "PA_TICKETS_CLIENT" ("N_CLIENT_ID"),
    CONSTRAINT "CK_PA_TICKETS_RSC_TYPE" CHECK
        ("S_SCOPE_TYPE" IN ('ORGANIZATION', 'DEPARTMENT', 'TEAM', 'CLIENT', 'ADMIN_GROUP', 'RECORD')),
    CONSTRAINT "CK_PA_TICKETS_RSC_DATES" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    CONSTRAINT "CK_PA_TICKETS_RSC_ACTIVE" CHECK ("N_ACTIVE" IN (true, false))
);
CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_RSC_EFFECTIVE"
    ON "PA_TICKETS_USER_ROLE_SCOPE" ("N_USER_ROLE_ID", "S_SCOPE_TYPE", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

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
    CONSTRAINT "FK_PA_TICKETS_AHD_HEAD" FOREIGN KEY ("N_ADMIN_HEAD_USER_ID")
        REFERENCES "PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "FK_PA_TICKETS_AHD_ADMIN" FOREIGN KEY ("N_ADMIN_USER_ID")
        REFERENCES "PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "CK_PA_TICKETS_AHD_ACTIVE" CHECK ("N_ACTIVE" IN (true, false)),
    CONSTRAINT "CK_PA_TICKETS_AHD_DATES" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    CONSTRAINT "UK_PA_TICKETS_AHD_START" UNIQUE
        ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "D_VALID_FROM")
);
CREATE INDEX IF NOT EXISTS "IX_PA_TICKETS_AHD_EFFECTIVE"
    ON "PA_TICKETS_ADMIN_HEAD_DELEGATION"
       ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

DO $roles$
DECLARE
    role_def record;
    role_id bigint;
    existing_code text;
BEGIN
    FOR role_def IN
        SELECT * FROM (VALUES
            ('USER', 'User', 'Standard ticket operations within assigned scope.'),
            ('ADMIN', 'Admin', 'Department-scoped administration.'),
            ('SUPER_ADMIN', 'Super Admin', 'Organization-wide administrative authority.'),
            ('ADMIN_HEAD', 'Admin Head', 'Admin capabilities and explicitly delegated Admin management.'),
            ('SPECTATOR', 'Spectator', 'Read-only access within assigned scope.')
        ) AS roles(code, name, description)
    LOOP
        SELECT "N_ROLE_ID", "S_ROLE_CODE" INTO role_id, existing_code
          FROM "PA_TICKETS_ROLE"
         WHERE UPPER("S_ROLE_CODE") = role_def.code;
        IF FOUND THEN
            IF existing_code <> role_def.code OR NOT (SELECT "N_SYSTEM_ROLE" AND "N_ACTIVE"
                      FROM "PA_TICKETS_ROLE"
                     WHERE "N_ROLE_ID" = role_id) THEN
                RAISE EXCEPTION 'Existing role code % conflicts with the protected built-in identity', role_def.code;
            END IF;
        ELSE
            LOOP
                role_id := nextval('"PA_TICKETS_ROLE_SEQ"'::regclass);
                EXIT WHEN NOT EXISTS (SELECT 1 FROM "PA_TICKETS_ROLE" WHERE "N_ROLE_ID" = role_id);
            END LOOP;
            INSERT INTO "PA_TICKETS_ROLE"
                ("N_ROLE_ID", "S_ROLE_CODE", "S_ROLE_NAME", "S_DESCRIPTION", "N_SYSTEM_ROLE", "N_ACTIVE", "N_CREATED_BY")
            VALUES
                (role_id, role_def.code, role_def.name, role_def.description, true, true, 0);
        END IF;
    END LOOP;
END
$roles$;

DO $permissions$
DECLARE
    permission_def record;
    permission_id bigint;
BEGIN
    FOR permission_def IN
        SELECT * FROM (VALUES
            ('APPROVAL_ADMIN', 'Administer approvals', 'APPROVALS', 'MANAGE', 'DEPARTMENT', 'DEPARTMENT_ADMIN', false),
            ('APPROVAL_PUBLISH', 'Publish approval rules', 'APPROVALS', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('APPROVAL_READ', 'View approvals', 'APPROVALS', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('AUDIT_EXPORT', 'Export audit records', 'AUDIT', 'EXPORT', 'RECORD', 'READ_ONLY', true),
            ('AUDITOR_VIEW', 'View audit records', 'AUDIT', 'VIEW', 'RECORD', 'READ_ONLY', true),
            ('CATALOGUE_PUBLISH', 'Publish catalogue configuration', 'CATALOGUE', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('CATEGORY_ADMIN', 'Administer categories', 'CATALOGUE', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('CATEGORY_READ', 'View categories', 'CATALOGUE', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('CLIENT_ADMIN', 'Administer client access', 'CLIENTS', 'MANAGE', 'DEPARTMENT', 'DEPARTMENT_ADMIN', false),
            ('COMMUNICATION_ADMIN', 'Administer communications', 'COMMUNICATIONS', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('COMMUNICATION_PUBLISH', 'Publish communication templates', 'COMMUNICATIONS', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('COMMUNICATION_READ', 'View communications', 'COMMUNICATIONS', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('COMMUNICATION_RETIRE', 'Retire communication templates', 'COMMUNICATIONS', 'RETIRE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('CONFIG_ADMIN', 'Administer protected configuration', 'CONFIGURATION', 'MANAGE', 'ORGANIZATION', 'SECURITY_GOVERNANCE', false),
            ('NOTIFICATION_ADMIN', 'Administer notifications', 'NOTIFICATIONS', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('NOTIFICATION_PUBLISH', 'Publish notification rules', 'NOTIFICATIONS', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('NOTIFICATION_READ', 'View notifications', 'NOTIFICATIONS', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('NOTIFICATION_RETIRE', 'Retire notification rules', 'NOTIFICATIONS', 'RETIRE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('REQUEST_TYPE_ADMIN', 'Administer request types', 'CATALOGUE', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('REQUEST_TYPE_READ', 'View request types', 'CATALOGUE', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('ROLE_ADMIN', 'Assign standard roles', 'SECURITY', 'ASSIGN', 'DEPARTMENT', 'DEPARTMENT_ADMIN', false),
            ('ROLE_PERMISSION_ADMIN', 'Configure role permissions', 'SECURITY', 'CONFIGURE', 'ORGANIZATION', 'SECURITY_GOVERNANCE', false),
            ('ROLE_SCOPE_ADMIN', 'Configure role scopes', 'SECURITY', 'SCOPE', 'ORGANIZATION', 'SECURITY_GOVERNANCE', false),
            ('SERVICE_ADMIN', 'Administer services', 'CATALOGUE', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('SERVICE_READ', 'View services', 'CATALOGUE', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('SLA_ADMIN', 'Administer SLA policies', 'SLA', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('SLA_PUBLISH', 'Publish SLA policies', 'SLA', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('SLA_READ', 'View SLA policies', 'SLA', 'VIEW', 'NONE', 'READ_ONLY', true),
            ('SLA_RETIRE', 'Retire SLA policies', 'SLA', 'RETIRE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('TICKET_ADMIN', 'Administer ticket status and priority', 'TICKETS', 'CONFIGURE', 'DEPARTMENT', 'DEPARTMENT_ADMIN', false),
            ('TICKET_READ', 'View tickets', 'TICKETS', 'VIEW', 'RECORD', 'READ_ONLY', true),
            ('TICKET_UPDATE', 'Update tickets', 'TICKETS', 'EDIT', 'RECORD', 'BUSINESS', false),
            ('USER_ADMIN', 'Administer users within scope', 'USERS', 'MANAGE', 'DEPARTMENT', 'DEPARTMENT_ADMIN', false),
            ('WORKFLOW_ADMIN', 'Administer workflows', 'WORKFLOWS', 'MANAGE', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('WORKFLOW_PUBLISH', 'Publish workflows', 'WORKFLOWS', 'PUBLISH', 'ORGANIZATION', 'DEPARTMENT_ADMIN', false),
            ('WORKFLOW_READ', 'View workflows', 'WORKFLOWS', 'VIEW', 'NONE', 'READ_ONLY', true)
        ) AS permissions(code, name, module_code, action_code, scope_class, security_class, read_only)
    LOOP
        SELECT "N_PERMISSION_ID" INTO permission_id
          FROM "PA_TICKETS_PERMISSION"
         WHERE "S_PERMISSION_CODE" = permission_def.code;
        IF FOUND THEN
            UPDATE "PA_TICKETS_PERMISSION"
               SET "S_MODULE_CODE" = permission_def.module_code,
                   "S_ACTION_CODE" = permission_def.action_code,
                   "S_SCOPE_CLASS" = permission_def.scope_class,
                   "S_SECURITY_CLASS" = permission_def.security_class,
                   "N_READ_ONLY" = permission_def.read_only
             WHERE "N_PERMISSION_ID" = permission_id;
        ELSE
            LOOP
                permission_id := nextval('"PA_TICKETS_PERMISSION_SEQ"'::regclass);
                EXIT WHEN NOT EXISTS (
                    SELECT 1 FROM "PA_TICKETS_PERMISSION" WHERE "N_PERMISSION_ID" = permission_id);
            END LOOP;
            INSERT INTO "PA_TICKETS_PERMISSION"
                ("N_PERMISSION_ID", "S_PERMISSION_CODE", "S_PERMISSION_NAME", "S_DESCRIPTION",
                 "S_MODULE_CODE", "S_ACTION_CODE", "S_SCOPE_CLASS", "S_SECURITY_CLASS",
                 "N_READ_ONLY", "N_ACTIVE", "N_CREATED_BY")
            VALUES
                (permission_id, permission_def.code, permission_def.name, permission_def.name,
                 permission_def.module_code, permission_def.action_code, permission_def.scope_class,
                 permission_def.security_class, permission_def.read_only, true, 0);
        END IF;
    END LOOP;
END
$permissions$;

DO $grants$
DECLARE
    grant_row record;
    grant_id bigint;
    now_at timestamptz := CURRENT_TIMESTAMP;
BEGIN
    FOR grant_row IN
        SELECT role."N_ROLE_ID", permission."N_PERMISSION_ID"
          FROM "PA_TICKETS_ROLE" role
          JOIN "PA_TICKETS_PERMISSION" permission
            ON (role."S_ROLE_CODE" = 'SUPER_ADMIN' AND permission."N_ACTIVE")
            OR (role."S_ROLE_CODE" IN ('ADMIN', 'ADMIN_HEAD') AND permission."S_PERMISSION_CODE" IN
                ('TICKET_READ', 'TICKET_UPDATE', 'USER_ADMIN', 'ROLE_ADMIN'))
            OR (role."S_ROLE_CODE" = 'USER' AND permission."S_PERMISSION_CODE" IN
                ('TICKET_READ', 'TICKET_UPDATE'))
            OR (role."S_ROLE_CODE" = 'SPECTATOR' AND permission."N_READ_ONLY")
         WHERE NOT EXISTS (
             SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION" existing
              WHERE existing."N_ROLE_ID" = role."N_ROLE_ID"
                AND existing."N_PERMISSION_ID" = permission."N_PERMISSION_ID")
    LOOP
        LOOP
            grant_id := nextval('"PA_TICKETS_ROLE_PERMISSION_SEQ"'::regclass);
            EXIT WHEN NOT EXISTS (
                SELECT 1 FROM "PA_TICKETS_ROLE_PERMISSION" WHERE "N_ROLE_PERMISSION_ID" = grant_id);
        END LOOP;
        INSERT INTO "PA_TICKETS_ROLE_PERMISSION"
            ("N_ROLE_PERMISSION_ID", "N_ROLE_ID", "N_PERMISSION_ID", "D_CREATED_AT", "N_CREATED_BY",
             "D_VALID_FROM", "N_ACTIVE", "D_UPDATED_AT", "N_UPDATED_BY")
        VALUES
            (grant_id, grant_row."N_ROLE_ID", grant_row."N_PERMISSION_ID", now_at, 0,
             now_at, true, now_at, 0);
    END LOOP;
END
$grants$;

DO $scope_backfill$
DECLARE
    scope_row record;
    scope_id bigint;
BEGIN
    FOR scope_row IN
        SELECT membership."N_USER_ROLE_ID", 'DEPARTMENT'::varchar AS scope_type,
               user_row."N_DEPARTMENT_ID" AS department_id, NULL::bigint AS team_id,
               GREATEST(membership."D_VALID_FROM", CURRENT_TIMESTAMP) AS "D_VALID_FROM",
               membership."D_VALID_TO", membership."N_ACTIVE", membership."N_CREATED_BY"
          FROM "PA_TICKETS_USER_ROLE" membership
          JOIN "PA_TICKETS_ROLE" role ON role."N_ROLE_ID" = membership."N_ROLE_ID"
          JOIN "PA_TICKETS_USER" user_row ON user_row."N_USER_ID" = membership."N_USER_ID"
         WHERE role."S_ROLE_CODE" IN ('ADMIN', 'ADMIN_HEAD')
           AND membership."N_ACTIVE"
           AND (membership."D_VALID_TO" IS NULL OR membership."D_VALID_TO" > CURRENT_TIMESTAMP)
           AND user_row."N_DEPARTMENT_ID" IS NOT NULL
        UNION ALL
        SELECT membership."N_USER_ROLE_ID", 'TEAM'::varchar, user_row."N_DEPARTMENT_ID",
               user_row."N_TEAM_ID", GREATEST(membership."D_VALID_FROM", CURRENT_TIMESTAMP), membership."D_VALID_TO",
               membership."N_ACTIVE", membership."N_CREATED_BY"
          FROM "PA_TICKETS_USER_ROLE" membership
          JOIN "PA_TICKETS_ROLE" role ON role."N_ROLE_ID" = membership."N_ROLE_ID"
          JOIN "PA_TICKETS_USER" user_row ON user_row."N_USER_ID" = membership."N_USER_ID"
         WHERE role."S_ROLE_CODE" IN ('USER', 'SPECTATOR', 'OPERATIONS', 'ADMINISTRATOR', 'AUDITOR')
           AND membership."N_ACTIVE"
           AND (membership."D_VALID_TO" IS NULL OR membership."D_VALID_TO" > CURRENT_TIMESTAMP)
           AND user_row."N_DEPARTMENT_ID" IS NOT NULL AND user_row."N_TEAM_ID" IS NOT NULL
        UNION ALL
        SELECT membership."N_USER_ROLE_ID", 'DEPARTMENT'::varchar, user_row."N_DEPARTMENT_ID",
               NULL::bigint, GREATEST(membership."D_VALID_FROM", CURRENT_TIMESTAMP), membership."D_VALID_TO",
               membership."N_ACTIVE", membership."N_CREATED_BY"
          FROM "PA_TICKETS_USER_ROLE" membership
          JOIN "PA_TICKETS_ROLE" role ON role."N_ROLE_ID" = membership."N_ROLE_ID"
          JOIN "PA_TICKETS_USER" user_row ON user_row."N_USER_ID" = membership."N_USER_ID"
         WHERE role."S_ROLE_CODE" IN ('USER', 'SPECTATOR', 'OPERATIONS', 'ADMINISTRATOR', 'AUDITOR')
           AND membership."N_ACTIVE"
           AND (membership."D_VALID_TO" IS NULL OR membership."D_VALID_TO" > CURRENT_TIMESTAMP)
           AND user_row."N_DEPARTMENT_ID" IS NOT NULL AND user_row."N_TEAM_ID" IS NULL
        UNION ALL
        SELECT membership."N_USER_ROLE_ID", 'ADMIN_GROUP'::varchar, NULL::bigint, NULL::bigint,
               GREATEST(membership."D_VALID_FROM", CURRENT_TIMESTAMP), membership."D_VALID_TO",
               membership."N_ACTIVE", membership."N_CREATED_BY"
          FROM "PA_TICKETS_USER_ROLE" membership
          JOIN "PA_TICKETS_ROLE" role ON role."N_ROLE_ID" = membership."N_ROLE_ID"
         WHERE role."S_ROLE_CODE" = 'ADMIN_HEAD'
           AND membership."N_ACTIVE"
           AND (membership."D_VALID_TO" IS NULL OR membership."D_VALID_TO" > CURRENT_TIMESTAMP)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM "PA_TICKETS_USER_ROLE_SCOPE" existing
             WHERE existing."N_USER_ROLE_ID" = scope_row."N_USER_ROLE_ID"
               AND existing."S_SCOPE_TYPE" = scope_row.scope_type
               AND existing."N_DEPARTMENT_ID" IS NOT DISTINCT FROM scope_row.department_id
               AND existing."N_TEAM_ID" IS NOT DISTINCT FROM scope_row.team_id
               AND (
                   existing."D_VALID_FROM" = scope_row."D_VALID_FROM"
                   OR (existing."N_ACTIVE"
                       AND existing."D_VALID_FROM" <= CURRENT_TIMESTAMP
                       AND (existing."D_VALID_TO" IS NULL OR existing."D_VALID_TO" > CURRENT_TIMESTAMP))
               )) THEN
            LOOP
                scope_id := nextval('"PA_TICKETS_ROLE_SCOPE_SEQ"'::regclass);
                EXIT WHEN NOT EXISTS (
                    SELECT 1 FROM "PA_TICKETS_USER_ROLE_SCOPE" WHERE "N_ROLE_SCOPE_ID" = scope_id);
            END LOOP;
            INSERT INTO "PA_TICKETS_USER_ROLE_SCOPE"
                ("N_ROLE_SCOPE_ID", "N_USER_ROLE_ID", "S_SCOPE_TYPE", "N_DEPARTMENT_ID", "N_TEAM_ID",
                 "D_VALID_FROM", "D_VALID_TO", "N_ACTIVE", "D_CREATED_AT", "N_CREATED_BY")
            VALUES
                (scope_id, scope_row."N_USER_ROLE_ID", scope_row.scope_type, scope_row.department_id,
                 scope_row.team_id, scope_row."D_VALID_FROM", scope_row."D_VALID_TO",
                 scope_row."N_ACTIVE", CURRENT_TIMESTAMP, COALESCE(scope_row."N_CREATED_BY", 0));
        END IF;
    END LOOP;
END
$scope_backfill$;

COMMIT;
