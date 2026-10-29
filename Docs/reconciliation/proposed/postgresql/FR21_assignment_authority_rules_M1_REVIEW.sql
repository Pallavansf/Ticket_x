-- Minimal persisted FR-021 rule relation for effective assignment authority.
-- REVIEW ONLY / NOT APPROVED / NOT EXECUTED. No permission, grant, rule, or
-- history rows are seeded by this artifact.
-- The first source-supported evaluator handles ASSIGN + OWN_TEAM only and
-- fails closed for delegation, other relations, notification, or acceptance.

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
        RAISE EXCEPTION 'FR-021 assignment-rule candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS_USER"') IS NULL
       OR to_regclass('public."PA_TICKETS_USER_ROLE"') IS NULL
       OR to_regclass('public."PA_TICKETS_USER_ROLE_SCOPE"') IS NULL THEN
        RAISE EXCEPTION 'FR-021 assignment-rule candidate requires user, membership, and scope relations';
    END IF;

    IF to_regclass('public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE"') IS NOT NULL
       OR to_regclass('public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ"') IS NOT NULL THEN
        RAISE EXCEPTION 'FR-021 assignment-rule candidate found existing or partial objects';
    END IF;
END
$$;

CREATE SEQUENCE public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ"
    START WITH 1 INCREMENT BY 1 CACHE 1;

CREATE TABLE public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE" (
    "N_ASSIGNMENT_RULE_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ"'::regclass),
    "S_PERMISSION_CODE" varchar(80) NOT NULL,
    "S_TARGET_RELATION" varchar(30) NOT NULL,
    "S_MODE" varchar(20) NOT NULL,
    "N_NOTIFY_TARGET_MANAGER" boolean NOT NULL DEFAULT false,
    "N_REQUIRE_MANAGER_ACCEPTANCE" boolean NOT NULL DEFAULT false,
    "N_ENABLED" boolean NOT NULL DEFAULT false,
    "D_VALID_FROM" timestamptz NOT NULL,
    "D_VALID_TO" timestamptz,
    "N_VERSION" integer NOT NULL,
    "D_CREATED_AT" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "N_CREATED_BY" bigint NOT NULL,
    CONSTRAINT "PK_PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_M1" PRIMARY KEY ("N_ASSIGNMENT_RULE_ID"),
    CONSTRAINT "FK_PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_CREATOR_M1" FOREIGN KEY ("N_CREATED_BY")
        REFERENCES public."PA_TICKETS_USER" ("N_USER_ID"),
    CONSTRAINT "CK_PA_TICKETS_ASSIGNMENT_AUTHORITY_RELATION_M1" CHECK
        ("S_TARGET_RELATION" IN ('SELF','OWN_TEAM','GROUP_MANAGER','GROUP_TEAM_USER','ANY')),
    CONSTRAINT "CK_PA_TICKETS_ASSIGNMENT_AUTHORITY_MODE_M1" CHECK
        ("S_MODE" IN ('ASSIGN','DELEGATE')),
    CONSTRAINT "CK_PA_TICKETS_ASSIGNMENT_AUTHORITY_DATES_M1" CHECK
        ("D_VALID_TO" IS NULL OR "D_VALID_TO" > "D_VALID_FROM"),
    CONSTRAINT "CK_PA_TICKETS_ASSIGNMENT_AUTHORITY_VERSION_M1" CHECK ("N_VERSION" > 0)
);

ALTER SEQUENCE public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE_SEQ"
    OWNED BY public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE"."N_ASSIGNMENT_RULE_ID";

CREATE UNIQUE INDEX "UX_PA_TICKETS_ASSIGNMENT_AUTHORITY_VERSION_M1"
    ON public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE"
       ("S_PERMISSION_CODE", "S_TARGET_RELATION", "S_MODE", "N_VERSION");
CREATE INDEX "IX_PA_TICKETS_ASSIGNMENT_AUTHORITY_EFFECTIVE_M1"
    ON public."PA_TICKETS_ASSIGNMENT_AUTHORITY_RULE"
       ("S_PERMISSION_CODE", "N_ENABLED", "D_VALID_FROM", "D_VALID_TO", "N_VERSION");

COMMIT;
