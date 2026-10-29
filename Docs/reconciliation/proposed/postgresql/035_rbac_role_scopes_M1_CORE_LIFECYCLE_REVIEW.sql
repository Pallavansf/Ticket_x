-- M1 core-lifecycle scope-only successor to migration 035.
-- REVIEW ONLY / NOT APPROVED / NOT EXECUTED.
-- Creates the authorization scope relation required by DatabaseAuthorizationFactsReader.
-- It does not create delegation objects or scope rows, and does not add a client FK.

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
        RAISE EXCEPTION '035 scope candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS_USER"') IS NULL
       OR to_regclass('public."PA_TICKETS_ROLE"') IS NULL
       OR to_regclass('public."PA_TICKETS_USER_ROLE"') IS NULL THEN
        RAISE EXCEPTION '035 scope candidate precondition failed: parent relations are required';
    END IF;

    IF to_regclass('public."PA_TICKETS_USER_ROLE_SCOPE"') IS NOT NULL
       OR to_regclass('public."PA_TICKETS_ROLE_SCOPE_SEQ"') IS NOT NULL THEN
        RAISE EXCEPTION '035 scope candidate precondition failed: existing or partial objects require reconciliation';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS_USER_ROLE"')
          AND a.attname = 'N_USER_ROLE_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION '035 scope candidate precondition failed: membership key must be NOT NULL bigint';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_catalog.pg_constraint c
        JOIN pg_catalog.pg_attribute a
          ON a.attrelid = c.conrelid AND a.attname = 'N_USER_ROLE_ID'
        WHERE c.conrelid = to_regclass('public."PA_TICKETS_USER_ROLE"')
          AND c.contype IN ('p','u')
          AND c.conkey = ARRAY[a.attnum]::smallint[]
    ) THEN
        RAISE EXCEPTION '035 scope candidate precondition failed: membership key must have a single-column primary or unique constraint';
    END IF;
END
$$;

CREATE SEQUENCE public."PA_TICKETS_ROLE_SCOPE_SEQ"
    START WITH 1 INCREMENT BY 1 CACHE 1;

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
    CONSTRAINT "PK_PA_TICKETS_ROLE_SCOPE_M1" PRIMARY KEY ("N_ROLE_SCOPE_ID"),
    CONSTRAINT "FK_PA_TICKETS_ROLE_SCOPE_MEMBERSHIP_M1" FOREIGN KEY ("N_USER_ROLE_ID")
        REFERENCES public."PA_TICKETS_USER_ROLE" ("N_USER_ROLE_ID"),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_TYPE_M1" CHECK
        ("S_SCOPE_TYPE" IN ('ORGANIZATION','DEPARTMENT','TEAM','CLIENT','ADMIN_GROUP','RECORD')),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_ORG_SENTINEL_M1" CHECK
        ("S_SCOPE_TYPE" <> 'ORGANIZATION' OR "N_ORGANIZATION_ID" IS NULL),
    CONSTRAINT "CK_PA_TICKETS_ROLE_SCOPE_DATES_M1" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM")
);

ALTER SEQUENCE public."PA_TICKETS_ROLE_SCOPE_SEQ"
    OWNED BY public."PA_TICKETS_USER_ROLE_SCOPE"."N_ROLE_SCOPE_ID";

CREATE UNIQUE INDEX "UX_PA_TICKETS_ROLE_SCOPE_M1"
    ON public."PA_TICKETS_USER_ROLE_SCOPE"
       ("N_USER_ROLE_ID", "S_SCOPE_TYPE", "N_ORGANIZATION_ID", "N_DEPARTMENT_ID",
        "N_TEAM_ID", "N_CLIENT_ID", "S_RESOURCE_TYPE", "S_RESOURCE_ID", "D_VALID_FROM");
CREATE INDEX "IX_PA_TICKETS_ROLE_SCOPE_EFFECTIVE_M1"
    ON public."PA_TICKETS_USER_ROLE_SCOPE"
       ("N_USER_ROLE_ID", "S_SCOPE_TYPE", "N_ACTIVE", "D_VALID_FROM", "D_VALID_TO");

COMMIT;
