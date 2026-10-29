-- Candidate for the schema objects required by the current authorization reader.
-- NOT APPROVED / NOT EXECUTED. No scope or delegation rows are created.
-- No client foreign key is introduced because client identity semantics remain
-- deferred. This is schema-only and does not establish effective authorization.

BEGIN;
SET LOCAL search_path TO public, pg_catalog;
SET LOCAL lock_timeout = '3s';
SET LOCAL statement_timeout = '30s';

DO $$
DECLARE
    object_name text;
BEGIN
    IF current_database() <> 'pa_tickets_codex_e2e'
       OR current_user <> 'pa_tickets_codex'
       OR inet_server_addr() IS DISTINCT FROM '::1'::inet
       OR inet_server_port() <> 5432 THEN
        RAISE EXCEPTION '035 candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS_USER_ROLE"') IS NULL
       OR to_regclass('public."PA_TICKETS_ROLE"') IS NULL
       OR to_regclass('public."PA_TICKETS_USER"') IS NULL THEN
        RAISE EXCEPTION '035 candidate precondition failed: parent user, role, and membership relations are required';
    END IF;
    FOREACH object_name IN ARRAY ARRAY[
        'PA_TICKETS_USER_ROLE_SCOPE','PA_TICKETS_ADMIN_HEAD_DELEGATION',
        'PA_TICKETS_ROLE_SCOPE_SEQ','PA_TICKETS_ADMIN_DELEGATION_SEQ'
    ] LOOP
        IF to_regclass(format('public.%I', object_name)) IS NOT NULL THEN
            RAISE EXCEPTION '035 candidate precondition failed: object already exists and requires definition reconciliation: %', object_name;
        END IF;
    END LOOP;
END
$$;

CREATE SEQUENCE public."PA_TICKETS_ROLE_SCOPE_SEQ" START WITH 1 INCREMENT BY 1 CACHE 1;
CREATE SEQUENCE public."PA_TICKETS_ADMIN_DELEGATION_SEQ" START WITH 1 INCREMENT BY 1 CACHE 1;

CREATE TABLE public."PA_TICKETS_USER_ROLE_SCOPE" (
    "N_ROLE_SCOPE_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_ROLE_SCOPE_SEQ"'::regclass),
    "N_USER_ROLE_ID" bigint NOT NULL,
    "S_SCOPE_TYPE" varchar(30) NOT NULL,
    "N_ORGANIZATION_ID" bigint,
    "N_DEPARTMENT_ID" bigint,
    "N_TEAM_ID" bigint,
    "N_CLIENT_ID" bigint,
    "S_RESOURCE_TYPE" varchar(100),
    "S_RESOURCE_ID" varchar(300),
    "D_VALID_FROM" timestamptz NOT NULL,
    "D_VALID_TO" timestamptz,
    "N_ACTIVE" boolean NOT NULL DEFAULT true,
    "D_CREATED_AT" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "N_CREATED_BY" bigint NOT NULL,
    CONSTRAINT "PK_PA_TICKETS_ROLE_SCOPE" PRIMARY KEY ("N_ROLE_SCOPE_ID"),
    CONSTRAINT "FK_PA_TICKETS_ROLE_SCOPE_MEMBERSHIP" FOREIGN KEY ("N_USER_ROLE_ID")
        REFERENCES public."PA_TICKETS_USER_ROLE" ("N_USER_ROLE_ID"),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_TYPE" CHECK
        ("S_SCOPE_TYPE" IN ('ORGANIZATION','DEPARTMENT','TEAM','CLIENT','ADMIN_GROUP','RECORD')),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_ORG_SENTINEL" CHECK
        ("S_SCOPE_TYPE" <> 'ORGANIZATION' OR "N_ORGANIZATION_ID" IS NULL),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_DATES" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM")
);
ALTER SEQUENCE public."PA_TICKETS_ROLE_SCOPE_SEQ"
    OWNED BY public."PA_TICKETS_USER_ROLE_SCOPE"."N_ROLE_SCOPE_ID";
CREATE INDEX "IX_PA_TICKETS_ROLE_SCOPE_EFFECTIVE"
    ON public."PA_TICKETS_USER_ROLE_SCOPE"
       ("N_USER_ROLE_ID", "S_SCOPE_TYPE", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

CREATE TABLE public."PA_TICKETS_ADMIN_HEAD_DELEGATION" (
    "N_ADMIN_DELEGATION_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_ADMIN_DELEGATION_SEQ"'::regclass),
    "N_ADMIN_HEAD_USER_ID" bigint NOT NULL,
    "N_ADMIN_USER_ID" bigint NOT NULL,
    "D_VALID_FROM" timestamptz NOT NULL,
    "D_VALID_TO" timestamptz,
    "N_ACTIVE" boolean NOT NULL DEFAULT true,
    "D_CREATED_AT" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "N_CREATED_BY" bigint NOT NULL,
    CONSTRAINT "PK_PA_TICKETS_ADMIN_HEAD_DELEGATION" PRIMARY KEY ("N_ADMIN_DELEGATION_ID"),
    CONSTRAINT "FK_PA_TICKETS_ADMIN_DELEGATION_HEAD" FOREIGN KEY ("N_ADMIN_HEAD_USER_ID")
        REFERENCES public."PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "FK_PA_TICKETS_ADMIN_DELEGATION_ADMIN" FOREIGN KEY ("N_ADMIN_USER_ID")
        REFERENCES public."PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "CK_PA_TICKETS_ADMIN_DELEGATION_DATES" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    CONSTRAINT "UK_PA_TICKETS_ADMIN_DELEGATION_START" UNIQUE
        ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "D_VALID_FROM")
);
ALTER SEQUENCE public."PA_TICKETS_ADMIN_DELEGATION_SEQ"
    OWNED BY public."PA_TICKETS_ADMIN_HEAD_DELEGATION"."N_ADMIN_DELEGATION_ID";
CREATE INDEX "IX_PA_TICKETS_ADMIN_DELEGATION_EFFECTIVE"
    ON public."PA_TICKETS_ADMIN_HEAD_DELEGATION"
       ("N_ADMIN_HEAD_USER_ID", "N_ADMIN_USER_ID", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

COMMIT;
