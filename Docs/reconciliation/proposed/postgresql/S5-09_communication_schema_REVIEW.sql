-- S5-09 communication schema candidate; review only, not an executable migration.
-- Source basis: tests/scripts/postgresql-phase5-schema.sql, postgresql-slice3-schema.sql,
-- postgresql-slice5-schema.sql, database/migrations/013_notification_orchestration.sql,
-- and the EF mappings in TicketsDbContext.cs / Phase5Rows.cs.
-- No target, migration history, baseline adoption, or operation is authorized.
-- This stop guard deliberately aborts before any candidate DDL.

DO $$ BEGIN RAISE EXCEPTION 'S5-09 communication review artifact; execution is prohibited'; END $$;

-- Review prerequisites, not an approved ordering:
-- * The selected baseline must contain PA_TICKETS, PA_TICKETS_WORK_ITEM, and PA_TICKETS_EVENT.
-- * Notification foreign keys require the PostgreSQL definitions from migration 013
--   (PA_TICKETS_NOTIFICATION_RULE, _VERSION, _EXECUTION). The current proposed
--   build order does not place root migration 013; its adoption/order is unresolved.
-- * Relations are unqualified to match current EF mappings and source scripts; they
--   resolve through PostgreSQL search_path (normally public). Schema qualification
--   and the effective search_path require target-side review.
-- * Sequence start/current/ownership state is unknown. No setval, owner transfer,
--   data seed, migration-ledger write, or destructive cleanup is included.
-- * Source scripts use PostgreSQL's implicit sequence defaults. Those describe a
--   newly created sequence only, not the state of any existing sequence.
-- * EF nullable-reference metadata is the nullability source for these mappings:
--   PublicId is required; Channel, Recipient, and IdempotencyKey are optional.
--   Mapped strings have no MaxLength/HasMaxLength configuration, so Npgsql maps
--   them to text. Status initializers are QUEUED and DRAFT; SQL defaults below
--   match those source defaults. These are source/model conclusions, not provider
--   compatibility evidence.
-- * CommunicationAttemptRow.Id now explicitly uses its cataloged sequence in EF.
--   No sequence ownership, existing state, start value, or reset is asserted.

CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_COMMUNICATION_SEQ";
CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ";
CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ";
CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ";
CREATE SEQUENCE IF NOT EXISTS "PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ";

CREATE TABLE "PA_TICKETS_COMMUNICATION" (
    "N_COMMUNICATION_ID" bigint PRIMARY KEY DEFAULT nextval('"PA_TICKETS_COMMUNICATION_SEQ"'),
    "N_TICKET_ID" bigint NOT NULL REFERENCES "PA_TICKETS" ("N_TICKET_ID"),
    "N_WORK_ITEM_ID" bigint REFERENCES "PA_TICKETS_WORK_ITEM" ("N_WORK_ITEM_ID"),
    "S_PUBLIC_ID" text NOT NULL,
    "S_COMMUNICATION_TYPE" text NOT NULL,
    "S_CHANNEL" text,
    "S_PROVIDER_CODE" text,
    "S_TEMPLATE_CODE" text,
    "N_TEMPLATE_VERSION_ID" bigint,
    "S_STATUS" text NOT NULL DEFAULT 'QUEUED',
    "S_TO_ADDRESS" text,
    "S_SUBJECT" text,
    "S_BODY" text,
    "S_STRUCTURED_PAYLOAD" text,
    "S_EXTERNAL_REFERENCE" text,
    "N_RETRY_COUNT" integer NOT NULL DEFAULT 0,
    "D_NEXT_RETRY_AT" timestamptz,
    "S_LEASE_OWNER" text,
    "D_LEASE_UNTIL" timestamptz,
    "S_FAILURE_CODE" text,
    "S_FAILURE_CLASSIFICATION" text,
    "S_FAILURE_REASON" text,
    "D_SENT_AT" timestamptz,
    "D_DELIVERED_AT" timestamptz,
    "D_FAILED_AT" timestamptz,
    "D_SCHEDULED_AT" timestamptz,
    "D_COMMUNICATION_AT" timestamptz NOT NULL,
    "D_RECORDED_AT" timestamptz NOT NULL,
    "N_RECORDED_BY" bigint NOT NULL,
    "S_CORRELATION_ID" text,
    "S_IDEMPOTENCY_KEY" text,
    "N_NOTIFICATION_EXECUTION_ID" bigint,
    "N_NOTIFICATION_RULE_ID" bigint,
    "N_NOTIFICATION_RULE_VERSION_ID" bigint,
    "N_EVENT_ID" bigint,
    CONSTRAINT "FK_PA_TICKETS_COMM_NOTIFY_EVENT" FOREIGN KEY ("N_EVENT_ID")
        REFERENCES "PA_TICKETS_EVENT" ("N_EVENT_ID")
);
CREATE UNIQUE INDEX "UX_PA_TICKETS_COMM_IDEMPOTENCY" ON "PA_TICKETS_COMMUNICATION" ("S_IDEMPOTENCY_KEY");
CREATE UNIQUE INDEX "UX_PA_TICKETS_COMM_PUBLIC_ID" ON "PA_TICKETS_COMMUNICATION" ("S_PUBLIC_ID");
CREATE INDEX "IX_PA_TICKETS_COMM_STATUS" ON "PA_TICKETS_COMMUNICATION" ("S_STATUS", "D_SCHEDULED_AT");
CREATE INDEX "IX_PA_TICKETS_COMM_LEASE" ON "PA_TICKETS_COMMUNICATION" ("S_STATUS", "D_NEXT_RETRY_AT", "D_LEASE_UNTIL");
CREATE INDEX "IX_PA_TICKETS_COMM_NOTIFICATION" ON "PA_TICKETS_COMMUNICATION" ("N_NOTIFICATION_EXECUTION_ID", "N_NOTIFICATION_RULE_VERSION_ID", "N_EVENT_ID");

CREATE TABLE "PA_TICKETS_COMMUNICATION_TEMPLATE" (
    "N_TEMPLATE_VERSION_ID" bigint PRIMARY KEY DEFAULT nextval('"PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ"'),
    "S_TEMPLATE_CODE" text NOT NULL,
    "S_CHANNEL" text NOT NULL,
    "N_VERSION" integer NOT NULL,
    "S_SUBJECT" text,
    "S_BODY" text NOT NULL,
    "S_STATUS" text NOT NULL DEFAULT 'DRAFT',
    "D_EFFECTIVE_FROM" timestamptz NOT NULL,
    "D_EFFECTIVE_TO" timestamptz,
    "D_CREATED_AT" timestamptz NOT NULL DEFAULT now(),
    "N_CREATED_BY" bigint NOT NULL DEFAULT 0,
    "D_PUBLISHED_AT" timestamptz,
    "N_PUBLISHED_BY" bigint,
    "D_RETIRED_AT" timestamptz,
    "N_RETIRED_BY" bigint,
    "N_ROW_VERSION" integer NOT NULL DEFAULT 0,
    CONSTRAINT "UX_PA_TICKETS_COMM_TEMPLATE_VERSION" UNIQUE ("S_TEMPLATE_CODE", "S_CHANNEL", "N_VERSION"),
    CONSTRAINT "CK_PA_TICKETS_COMM_TEMPLATE_STATUS" CHECK ("S_STATUS" IN ('DRAFT','PUBLISHED','RETIRED','ACTIVE'))
);
CREATE INDEX "IX_PA_TICKETS_COMM_TEMPLATE_ACTIVE" ON "PA_TICKETS_COMMUNICATION_TEMPLATE" ("S_TEMPLATE_CODE", "S_CHANNEL", "S_STATUS", "D_EFFECTIVE_FROM");

CREATE TABLE "PA_TICKETS_COMMUNICATION_ATTEMPT" (
    "N_ATTEMPT_ID" bigint PRIMARY KEY DEFAULT nextval('"PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ"'),
    "N_COMMUNICATION_ID" bigint NOT NULL,
    "N_ATTEMPT_NO" integer NOT NULL,
    "D_STARTED_AT" timestamptz NOT NULL,
    "D_COMPLETED_AT" timestamptz NOT NULL,
    "S_PROVIDER" text NOT NULL,
    "S_PROVIDER_MESSAGE_ID" text,
    "S_RESULT" text NOT NULL,
    "S_FAILURE_CLASSIFICATION" text,
    "S_FAILURE_CODE" text,
    "S_FAILURE_REASON" text,
    "S_CORRELATION_ID" text,
    FOREIGN KEY ("N_COMMUNICATION_ID")
        REFERENCES "PA_TICKETS_COMMUNICATION" ("N_COMMUNICATION_ID") ON DELETE CASCADE,
    UNIQUE ("N_COMMUNICATION_ID", "N_ATTEMPT_NO")
);
CREATE INDEX "IX_PA_TICKETS_COMM_ATTEMPT_COMM" ON "PA_TICKETS_COMMUNICATION_ATTEMPT" ("N_COMMUNICATION_ID", "N_ATTEMPT_NO");

CREATE TABLE "PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG" (
    "N_PROVIDER_CONFIG_ID" bigint PRIMARY KEY DEFAULT nextval('"PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ"'),
    "S_PROVIDER_CODE" text NOT NULL,
    "S_CHANNEL" text NOT NULL,
    "N_ENABLED" boolean NOT NULL DEFAULT true,
    "N_MAX_ATTEMPTS" integer NOT NULL DEFAULT 3,
    "N_RETRY_DELAY_SECONDS" integer NOT NULL DEFAULT 5,
    "D_EFFECTIVE_FROM" timestamptz NOT NULL,
    "D_EFFECTIVE_TO" timestamptz,
    "D_UPDATED_AT" timestamptz NOT NULL,
    "N_UPDATED_BY" bigint NOT NULL,
    "N_ROW_VERSION" integer NOT NULL DEFAULT 0,
    CONSTRAINT "CK_PA_TICKETS_COMM_PROVIDER_RETRY" CHECK ("N_MAX_ATTEMPTS" BETWEEN 1 AND 20 AND "N_RETRY_DELAY_SECONDS" BETWEEN 0 AND 86400),
    CONSTRAINT "UX_PA_TICKETS_COMM_PROVIDER_CONFIG" UNIQUE ("S_PROVIDER_CODE", "S_CHANNEL"),
    CONSTRAINT "CK_PA_TICKETS_COMM_PROVIDER_CHANNEL" CHECK ("S_CHANNEL" IN ('EMAIL','WHATSAPP','PUSH','INAPP'))
);
CREATE INDEX "IX_PA_TICKETS_COMM_PROVIDER_ACTIVE" ON "PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG" ("S_PROVIDER_CODE", "S_CHANNEL", "N_ENABLED", "D_EFFECTIVE_FROM");

CREATE TABLE "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG" (
    "N_CHANNEL_CONFIG_ID" bigint PRIMARY KEY DEFAULT nextval('"PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ"'),
    "S_CHANNEL" text NOT NULL,
    "N_ENABLED" boolean NOT NULL DEFAULT true,
    "D_EFFECTIVE_FROM" timestamptz NOT NULL,
    "D_EFFECTIVE_TO" timestamptz,
    "D_UPDATED_AT" timestamptz NOT NULL,
    "N_UPDATED_BY" bigint NOT NULL,
    "N_ROW_VERSION" integer NOT NULL DEFAULT 0,
    UNIQUE ("S_CHANNEL"),
    CONSTRAINT "CK_PA_TICKETS_COMM_CHANNEL_NAME" CHECK ("S_CHANNEL" IN ('EMAIL','WHATSAPP','PUSH','INAPP'))
);
CREATE INDEX "IX_PA_TICKETS_COMM_CHANNEL_ACTIVE" ON "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG" ("S_CHANNEL", "N_ENABLED", "D_EFFECTIVE_FROM");

-- This candidate intentionally does not choose migration numbering/order. In
-- particular, it depends on unresolved baseline selection and root 013 adoption.
-- Post-013 follow-up constraints, shown for review and intentionally commented
-- out because their placement is not established by the proposed build order:
-- ALTER TABLE "PA_TICKETS_COMMUNICATION"
--     ADD CONSTRAINT "FK_PA_TICKETS_COMM_NOTIFY_EXEC" FOREIGN KEY ("N_NOTIFICATION_EXECUTION_ID")
--         REFERENCES "PA_TICKETS_NOTIFICATION_EXECUTION" ("N_NOTIFICATION_EXECUTION_ID");
-- ALTER TABLE "PA_TICKETS_COMMUNICATION"
--     ADD CONSTRAINT "FK_PA_TICKETS_COMM_NOTIFY_RULE" FOREIGN KEY ("N_NOTIFICATION_RULE_ID")
--         REFERENCES "PA_TICKETS_NOTIFICATION_RULE" ("N_NOTIFICATION_RULE_ID");
-- ALTER TABLE "PA_TICKETS_COMMUNICATION"
--     ADD CONSTRAINT "FK_PA_TICKETS_COMM_NOTIFY_VERSION" FOREIGN KEY ("N_NOTIFICATION_RULE_VERSION_ID")
--         REFERENCES "PA_TICKETS_NOTIFICATION_RULE_VERSION" ("N_NOTIFICATION_RULE_VERSION_ID");
