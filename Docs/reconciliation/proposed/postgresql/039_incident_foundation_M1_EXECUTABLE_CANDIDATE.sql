-- Minimal Incident persistence slice needed by the selected Incident tests.
-- NOT APPROVED / NOT EXECUTED. It deliberately does not create Problem, Change,
-- Change Implementation, or generic Relation objects. Do not record migration
-- 039 as applied if this slice is ever separately adopted.

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
        RAISE EXCEPTION 'Incident candidate target guard failed';
    END IF;

    IF to_regclass('public."PA_TICKETS"') IS NULL THEN
        RAISE EXCEPTION 'Incident candidate precondition failed: public.PA_TICKETS is absent';
    END IF;
    IF to_regclass('public."PA_TICKETS_INCIDENT"') IS NOT NULL
       OR to_regclass('public."PA_TICKETS_INCIDENT_SEQ"') IS NOT NULL THEN
        RAISE EXCEPTION 'Incident candidate precondition failed: partial or existing Incident objects require reconciliation';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_attribute a
        WHERE a.attrelid = to_regclass('public."PA_TICKETS"')
          AND a.attname = 'N_TICKET_ID' AND a.atttypid = 'bigint'::regtype
          AND a.attnotnull AND NOT a.attisdropped
    ) THEN
        RAISE EXCEPTION 'Incident candidate precondition failed: PA_TICKETS.N_TICKET_ID must be NOT NULL bigint';
    END IF;
END
$$;

CREATE SEQUENCE public."PA_TICKETS_INCIDENT_SEQ" START WITH 1 INCREMENT BY 1 CACHE 1;
CREATE TABLE public."PA_TICKETS_INCIDENT" (
    "N_INCIDENT_ID" bigint NOT NULL DEFAULT nextval('public."PA_TICKETS_INCIDENT_SEQ"'::regclass),
    "N_TICKET_ID" bigint NOT NULL,
    "S_IMPACT" varchar(30) NOT NULL DEFAULT 'MEDIUM',
    "S_URGENCY" varchar(30) NOT NULL DEFAULT 'MEDIUM',
    "N_MAJOR_INCIDENT" boolean NOT NULL DEFAULT false,
    "N_INCIDENT_COMMANDER_USER_ID" bigint,
    "D_ACKNOWLEDGED_AT" timestamptz,
    "N_ACKNOWLEDGED_BY" bigint,
    "D_RESTORED_AT" timestamptz,
    "N_RESTORED_BY" bigint,
    "D_RESOLVED_AT" timestamptz,
    "N_RESOLVED_BY" bigint,
    "D_CLOSED_AT" timestamptz,
    "N_CLOSED_BY" bigint,
    "D_MAJOR_INCIDENT_DECLARED_AT" timestamptz,
    "N_MAJOR_INCIDENT_DECLARED_BY" bigint,
    "D_MAJOR_INCIDENT_RESOLVED_AT" timestamptz,
    "S_ROOT_CAUSE_SUMMARY" text,
    "S_WORKAROUND_SUMMARY" text,
    "S_RESOLUTION_SUMMARY" text,
    "S_CUSTOMER_IMPACT" text,
    "S_BUSINESS_IMPACT" text,
    "S_SERVICE_IMPACT" text,
    "D_OUTAGE_START_AT" timestamptz,
    "D_OUTAGE_END_AT" timestamptz,
    "D_CREATED_AT" timestamptz NOT NULL,
    "D_UPDATED_AT" timestamptz NOT NULL,
    "S_IDEMPOTENCY_KEY" varchar(200),
    CONSTRAINT "PK_PA_TICKETS_INCIDENT" PRIMARY KEY ("N_INCIDENT_ID"),
    CONSTRAINT "UK_PA_TICKETS_INCIDENT_TICKET" UNIQUE ("N_TICKET_ID"),
    CONSTRAINT "FK_PA_TICKETS_INCIDENT_TICKET" FOREIGN KEY ("N_TICKET_ID")
        REFERENCES public."PA_TICKETS" ("N_TICKET_ID")
);
ALTER SEQUENCE public."PA_TICKETS_INCIDENT_SEQ"
    OWNED BY public."PA_TICKETS_INCIDENT"."N_INCIDENT_ID";
CREATE INDEX "IX_PA_TICKETS_INCIDENT_IMPACT_URGENCY"
    ON public."PA_TICKETS_INCIDENT" ("S_IMPACT", "S_URGENCY");
CREATE INDEX "IX_PA_TICKETS_INCIDENT_COMMANDER"
    ON public."PA_TICKETS_INCIDENT" ("N_INCIDENT_COMMANDER_USER_ID");
CREATE UNIQUE INDEX "UX_PA_TICKETS_INCIDENT_IDEMPOTENCY"
    ON public."PA_TICKETS_INCIDENT" ("S_IDEMPOTENCY_KEY")
    WHERE "S_IDEMPOTENCY_KEY" IS NOT NULL;

COMMIT;
