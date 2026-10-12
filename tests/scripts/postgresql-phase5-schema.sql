-- PA TICKETS additive PostgreSQL Phase 5 foundation schema.
-- This file is provider-specific infrastructure only; no Phase 5 business behavior.

create sequence if not exists "PA_TICKETS_COMMUNICATION_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_RULE_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_RULE_VERSION_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_EXECUTION_SEQ";
create sequence if not exists "PA_TICKETS_ATTACHMENT_SEQ";
create sequence if not exists "PA_TICKETS_ATTACHMENT_VERSION_SEQ";
create sequence if not exists "PA_TICKETS_EVIDENCE_SEQ";
create sequence if not exists "PA_TICKETS_INCIDENT_SEQ";
create sequence if not exists "PA_TICKETS_PROBLEM_SEQ";
create sequence if not exists "PA_TICKETS_CHANGE_SEQ";
create sequence if not exists "PA_TICKETS_CHANGE_IMPLEMENTATION_SEQ";
create sequence if not exists "PA_TICKETS_ESCALATION_SEQ";
create sequence if not exists "PA_TICKETS_RELATION_SEQ";

create table if not exists "PA_TICKETS_COMMUNICATION" (
  "N_COMMUNICATION_ID" bigint primary key default nextval('"PA_TICKETS_COMMUNICATION_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "S_COMMUNICATION_TYPE" varchar(30) not null,
  "S_CHANNEL" varchar(30) not null,
  "S_PROVIDER_CODE" varchar(100),
  "S_TEMPLATE_CODE" varchar(200),
  "S_STATUS" varchar(30) not null default 'PENDING',
  "S_TO_ADDRESS" varchar(2000) not null,
  "S_SUBJECT" varchar(1000),
  "S_BODY" text,
  "S_STRUCTURED_PAYLOAD" text,
  "S_EXTERNAL_REFERENCE" varchar(500),
  "N_RETRY_COUNT" integer not null default 0,
  "S_FAILURE_REASON" text,
  "D_SCHEDULED_AT" timestamptz,
  "D_COMMUNICATION_AT" timestamptz not null,
  "D_RECORDED_AT" timestamptz not null,
  "N_RECORDED_BY" bigint not null,
  "S_CORRELATION_ID" varchar(100),
  "S_IDEMPOTENCY_KEY" varchar(200) not null
);
create unique index if not exists "UX_PA_TICKETS_COMM_IDEMPOTENCY" on "PA_TICKETS_COMMUNICATION"("S_IDEMPOTENCY_KEY");
create index if not exists "IX_PA_TICKETS_COMM_STATUS" on "PA_TICKETS_COMMUNICATION"("S_STATUS","D_SCHEDULED_AT");

create table if not exists "PA_TICKETS_NOTIFICATION" (
  "N_NOTIFICATION_ID" bigint primary key default nextval('"PA_TICKETS_NOTIFICATION_SEQ"'),
  "N_TICKET_ID" bigint references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "N_USER_ID" bigint,
  "S_NOTIFICATION_TYPE" varchar(50) not null,
  "S_CHANNEL" varchar(30) not null,
  "S_SUBJECT" varchar(1000),
  "S_MESSAGE" text,
  "S_STATUS" varchar(30) not null default 'PENDING',
  "D_SCHEDULED_AT" timestamptz,
  "D_SENT_AT" timestamptz,
  "D_READ_AT" timestamptz,
  "D_CREATED_AT" timestamptz not null,
  "S_CORRELATION_ID" varchar(100),
  "S_IDEMPOTENCY_KEY" varchar(200)
);
create unique index if not exists "UX_PA_TICKETS_NOTIFICATION_IDEMPOTENCY" on "PA_TICKETS_NOTIFICATION"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;
create index if not exists "IX_PA_TICKETS_NOTIFICATION_USER_STATUS" on "PA_TICKETS_NOTIFICATION"("N_USER_ID","S_STATUS","D_CREATED_AT");

create table if not exists "PA_TICKETS_ATTACHMENT" (
  "N_ATTACHMENT_ID" bigint primary key default nextval('"PA_TICKETS_ATTACHMENT_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "S_ATTACHMENT_TYPE" varchar(50) not null,
  "S_FILE_NAME" varchar(500) not null,
  "S_CONTENT_TYPE" varchar(200),
  "N_FILE_SIZE" bigint,
  "S_STORAGE_PROVIDER" varchar(50),
  "S_STORAGE_REFERENCE" varchar(2000),
  "S_SHA256_HASH" varchar(128) not null,
  "S_CLASSIFICATION" varchar(50),
  "S_DESCRIPTION" varchar(2000),
  "N_CURRENT_VERSION" integer not null default 1,
  "N_ACTIVE" boolean not null default true,
  "D_UPLOADED_AT" timestamptz not null,
  "N_UPLOADED_BY" bigint not null
);
alter table "PA_TICKETS_ATTACHMENT" add column if not exists "D_RETENTION_UNTIL" timestamptz;
alter table "PA_TICKETS_ATTACHMENT" add column if not exists "S_DISPOSAL_REASON" text;
alter table "PA_TICKETS_ATTACHMENT" add column if not exists "S_IDEMPOTENCY_KEY" varchar(200);
create unique index if not exists "UX_PA_TICKETS_ATTACHMENT_IDEMPOTENCY" on "PA_TICKETS_ATTACHMENT"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;
create index if not exists "IX_PA_TICKETS_ATTACHMENT_TICKET" on "PA_TICKETS_ATTACHMENT"("N_TICKET_ID");

create table if not exists "PA_TICKETS_ATTACHMENT_VERSION" (
  "N_ATTACHMENT_VERSION_ID" bigint primary key default nextval('"PA_TICKETS_ATTACHMENT_VERSION_SEQ"'),
  "N_ATTACHMENT_ID" bigint not null references "PA_TICKETS_ATTACHMENT"("N_ATTACHMENT_ID"),
  "N_VERSION_NO" integer not null,
  "S_FILE_NAME" varchar(500) not null,
  "S_CONTENT_TYPE" varchar(200),
  "N_FILE_SIZE" bigint,
  "S_STORAGE_PROVIDER" varchar(50),
  "S_STORAGE_REFERENCE" varchar(2000),
  "S_SHA256_HASH" varchar(128) not null,
  "S_VERSION_STATUS" varchar(30) not null default 'CURRENT',
  "S_CHANGE_REASON" varchar(2000),
  "D_UPLOADED_AT" timestamptz not null,
  "N_UPLOADED_BY" bigint not null,
  unique ("N_ATTACHMENT_ID","N_VERSION_NO"),
  check ("S_VERSION_STATUS" in ('CURRENT','SUPERSEDED','RETIRED'))
);
create unique index if not exists "UX_PA_TICKETS_ATTACHMENT_CURRENT" on "PA_TICKETS_ATTACHMENT_VERSION"("N_ATTACHMENT_ID") where "S_VERSION_STATUS" = 'CURRENT';

create table if not exists "PA_TICKETS_EVIDENCE" (
  "N_EVIDENCE_ID" bigint primary key default nextval('"PA_TICKETS_EVIDENCE_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "N_ATTACHMENT_ID" bigint references "PA_TICKETS_ATTACHMENT"("N_ATTACHMENT_ID"),
  "S_EVIDENCE_CODE" varchar(100) not null unique,
  "S_EVIDENCE_TYPE" varchar(100) not null,
  "S_DESCRIPTION" varchar(2000),
  "S_CONTROL_REFERENCE" varchar(500),
  "S_STATUS" varchar(50) not null default 'VALID',
  "D_VALID_FROM" timestamptz,
  "D_REVIEW_DUE_AT" timestamptz,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null
);

create table if not exists "PA_TICKETS_INCIDENT" (
  "N_INCIDENT_ID" bigint primary key default nextval('"PA_TICKETS_INCIDENT_SEQ"'),
  "N_TICKET_ID" bigint not null unique references "PA_TICKETS"("N_TICKET_ID"),
  "S_IMPACT" varchar(30) not null default 'MEDIUM',
  "S_URGENCY" varchar(30) not null default 'MEDIUM',
  "N_MAJOR_INCIDENT" boolean not null default false,
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
  "D_CREATED_AT" timestamptz not null,
  "D_UPDATED_AT" timestamptz not null,
  "S_IDEMPOTENCY_KEY" varchar(200)
);
create index if not exists "IX_PA_TICKETS_INCIDENT_IMPACT_URGENCY" on "PA_TICKETS_INCIDENT"("S_IMPACT","S_URGENCY");
create index if not exists "IX_PA_TICKETS_INCIDENT_COMMANDER" on "PA_TICKETS_INCIDENT"("N_INCIDENT_COMMANDER_USER_ID");
create unique index if not exists "UX_PA_TICKETS_INCIDENT_IDEMPOTENCY" on "PA_TICKETS_INCIDENT"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;

alter table "PA_TICKETS_EVIDENCE" add column if not exists "N_REVIEWED_BY" bigint;
alter table "PA_TICKETS_EVIDENCE" add column if not exists "D_REVIEWED_AT" timestamptz;
alter table "PA_TICKETS_EVIDENCE" add column if not exists "S_REVIEW_COMMENT" text;
alter table "PA_TICKETS_EVIDENCE" add column if not exists "D_RETENTION_UNTIL" timestamptz;
alter table "PA_TICKETS_EVIDENCE" add column if not exists "S_DISPOSAL_REASON" text;
alter table "PA_TICKETS_ATTACHMENT_VERSION" add column if not exists "S_IDEMPOTENCY_KEY" varchar(200);
create unique index if not exists "UX_PA_TICKETS_ATTACHMENT_VERSION_IDEMPOTENCY" on "PA_TICKETS_ATTACHMENT_VERSION"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;
create index if not exists "IX_PA_TICKETS_EVIDENCE_TICKET" on "PA_TICKETS_EVIDENCE"("N_TICKET_ID");

create table if not exists "PA_TICKETS_PROBLEM" (
  "N_PROBLEM_ID" bigint primary key default nextval('"PA_TICKETS_PROBLEM_SEQ"'),
  "N_TICKET_ID" bigint not null unique references "PA_TICKETS"("N_TICKET_ID"),
  "S_PROBLEM_STATEMENT" text,
  "S_ROOT_CAUSE" text,
  "S_WORKAROUND" text,
  "S_PERMANENT_FIX" text,
  "S_PROBLEM_STATUS" varchar(50) not null default 'OPEN',
  "D_IDENTIFIED_AT" timestamptz,
  "D_ROOT_CAUSE_IDENTIFIED_AT" timestamptz,
  "D_RESOLVED_AT" timestamptz,
  "N_CREATED_BY" bigint not null,
  "D_CREATED_AT" timestamptz not null
);
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_IDENTIFIED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_ROOT_CAUSE_IDENTIFIED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_KNOWN_ERROR" boolean not null default false;
alter table "PA_TICKETS_PROBLEM" add column if not exists "D_KNOWN_ERROR_DECLARED_AT" timestamptz;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_KNOWN_ERROR_DECLARED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_RESOLVED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "S_RESOLUTION_SUMMARY" text;
alter table "PA_TICKETS_PROBLEM" add column if not exists "D_CLOSED_AT" timestamptz;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_CLOSED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "D_REOPENED_AT" timestamptz;
alter table "PA_TICKETS_PROBLEM" add column if not exists "N_REOPENED_BY" bigint;
alter table "PA_TICKETS_PROBLEM" add column if not exists "D_UPDATED_AT" timestamptz;
alter table "PA_TICKETS_PROBLEM" add column if not exists "S_IDEMPOTENCY_KEY" varchar(200);
update "PA_TICKETS_PROBLEM" set "D_UPDATED_AT" = coalesce("D_UPDATED_AT", "D_CREATED_AT") where "D_UPDATED_AT" is null;
alter table "PA_TICKETS_PROBLEM" alter column "D_UPDATED_AT" set not null;
create unique index if not exists "UX_PA_TICKETS_PROBLEM_TICKET" on "PA_TICKETS_PROBLEM"("N_TICKET_ID");
create unique index if not exists "UX_PA_TICKETS_PROBLEM_IDEMPOTENCY" on "PA_TICKETS_PROBLEM"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;

create table if not exists "PA_TICKETS_CHANGE" (
  "N_CHANGE_ID" bigint primary key default nextval('"PA_TICKETS_CHANGE_SEQ"'),
  "N_TICKET_ID" bigint not null unique references "PA_TICKETS"("N_TICKET_ID"),
  "S_CHANGE_REASON" text,
  "S_BUSINESS_IMPACT" text,
  "S_RISK_ASSESSMENT" text,
  "S_IMPLEMENTATION_PLAN" text,
  "S_TESTING_PLAN" text,
  "S_ROLLBACK_PLAN" text,
  "S_VERIFICATION_PLAN" text,
  "S_CHANGE_STATUS" varchar(50) not null default 'DRAFT',
  "D_PLANNED_START_AT" timestamptz,
  "D_PLANNED_END_AT" timestamptz,
  "D_ACTUAL_START_AT" timestamptz,
  "D_ACTUAL_END_AT" timestamptz,
  "N_CREATED_BY" bigint not null,
  "D_CREATED_AT" timestamptz not null,
  "D_UPDATED_AT" timestamptz,
  "N_UPDATED_BY" bigint
);

create table if not exists "PA_TICKETS_CHANGE_IMPLEMENTATION" (
  "N_CHANGE_IMPLEMENTATION_ID" bigint primary key default nextval('"PA_TICKETS_CHANGE_IMPLEMENTATION_SEQ"'),
  "N_CHANGE_ID" bigint not null references "PA_TICKETS_CHANGE"("N_CHANGE_ID"),
  "N_STEP_NO" integer not null,
  "S_STEP_NAME" varchar(300) not null,
  "S_DESCRIPTION" text,
  "S_STATUS" varchar(50) not null default 'PENDING',
  "N_EXECUTED_BY" bigint,
  "D_STARTED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "S_RESULT" text,
  unique ("N_CHANGE_ID","N_STEP_NO")
);

create table if not exists "PA_TICKETS_ESCALATION" (
  "N_ESCALATION_ID" bigint primary key default nextval('"PA_TICKETS_ESCALATION_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "S_ESCALATION_TYPE" varchar(50) not null,
  "S_REASON" varchar(2000),
  "N_ESCALATED_FROM_USER" bigint,
  "N_ESCALATED_TO_USER" bigint,
  "N_ESCALATED_TO_DEPARTMENT" bigint,
  "D_ESCALATED_AT" timestamptz not null,
  "D_RESOLVED_AT" timestamptz,
  "N_CREATED_BY" bigint not null
);
alter table "PA_TICKETS_ESCALATION" add column if not exists "D_ESCALATED_AT" timestamptz;
create index if not exists "IX_PA_TICKETS_ESCALATION_TICKET" on "PA_TICKETS_ESCALATION"("N_TICKET_ID","D_ESCALATED_AT");

create table if not exists "PA_TICKETS_RELATION" (
  "N_RELATION_ID" bigint primary key default nextval('"PA_TICKETS_RELATION_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_RELATED_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "S_RELATION_TYPE" varchar(50) not null,
  "S_DESCRIPTION" varchar(1000),
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  check ("N_TICKET_ID" <> "N_RELATED_TICKET_ID"),
  unique ("N_TICKET_ID","N_RELATED_TICKET_ID","S_RELATION_TYPE")
);
create unique index if not exists "UX_PA_TICKETS_RELATION_UNIQUE" on "PA_TICKETS_RELATION"("N_TICKET_ID","N_RELATED_TICKET_ID","S_RELATION_TYPE");
create index if not exists "IX_PA_TICKETS_RELATION_RELATED" on "PA_TICKETS_RELATION"("N_RELATED_TICKET_ID","S_RELATION_TYPE");

create table if not exists "PA_TICKETS_NOTIFICATION_RULE" (
  "N_NOTIFICATION_RULE_ID" bigint primary key default nextval('"PA_TICKETS_NOTIFICATION_RULE_SEQ"'),
  "S_RULE_CODE" varchar(200) not null unique,
  "S_NAME" varchar(300) not null,
  "S_DESCRIPTION" text,
  "S_EVENT_TYPE" varchar(100) not null,
  "N_ENABLED" boolean not null default true,
  "N_PRIORITY" integer not null default 0,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "D_CREATED_AT" timestamptz not null,
  "D_UPDATED_AT" timestamptz,
  "N_VERSION" integer not null default 1
);
create index if not exists "IX_PA_TICKETS_NOTIFICATION_RULE_EVENT" on "PA_TICKETS_NOTIFICATION_RULE"("S_EVENT_TYPE","N_ENABLED","D_EFFECTIVE_FROM");

create table if not exists "PA_TICKETS_NOTIFICATION_RULE_VERSION" (
  "N_NOTIFICATION_RULE_VERSION_ID" bigint primary key default nextval('"PA_TICKETS_NOTIFICATION_RULE_VERSION_SEQ"'),
  "N_RULE_ID" bigint not null references "PA_TICKETS_NOTIFICATION_RULE"("N_NOTIFICATION_RULE_ID") on delete cascade,
  "N_VERSION_NO" integer not null,
  "S_CONDITION" text,
  "S_RECIPIENT_DEFINITION" varchar(200),
  "S_CHANNEL_DEFINITION" varchar(200),
  "S_TEMPLATE_CODE" varchar(200),
  "S_DEDUPLICATION_SCOPE" varchar(50) not null default 'PER_EVENT',
  "N_ENABLED" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "N_CREATED_BY" bigint not null,
  "D_CREATED_AT" timestamptz not null,
  unique ("N_RULE_ID","N_VERSION_NO")
);
create index if not exists "IX_PA_TICKETS_NOTIFICATION_RULE_VERSION_ACTIVE" on "PA_TICKETS_NOTIFICATION_RULE_VERSION"("N_RULE_ID","N_ENABLED","D_EFFECTIVE_FROM");

create table if not exists "PA_TICKETS_NOTIFICATION_EXECUTION" (
  "N_NOTIFICATION_EXECUTION_ID" bigint primary key default nextval('"PA_TICKETS_NOTIFICATION_EXECUTION_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_EVENT_ID" bigint not null references "PA_TICKETS_EVENT"("N_EVENT_ID"),
  "N_RULE_ID" bigint references "PA_TICKETS_NOTIFICATION_RULE"("N_NOTIFICATION_RULE_ID"),
  "N_RULE_VERSION_ID" bigint references "PA_TICKETS_NOTIFICATION_RULE_VERSION"("N_NOTIFICATION_RULE_VERSION_ID"),
  "S_STATUS" varchar(30) not null,
  "D_EVALUATED_AT" timestamptz not null,
  "N_MATCHED" boolean not null default false,
  "S_SKIP_REASON" varchar(200),
  "N_CREATED_COMMUNICATION_COUNT" integer not null default 0,
  "S_CORRELATION_ID" varchar(200),
  "D_CREATED_AT" timestamptz not null,
  "D_COMPLETED_AT" timestamptz,
  "S_FAILURE_CODE" varchar(100),
  "S_FAILURE_REASON" text,
  "S_RULE_CODE" varchar(200),
  "S_TEMPLATE_CODE" varchar(200),
  "S_SELECTED_CHANNEL" varchar(200),
  "S_DEDUPE_KEY" varchar(200),
  "S_NOTIFICATION_CONTEXT" text
);
create unique index if not exists "UX_PA_TICKETS_NOTIFICATION_EXECUTION_EVENT_RULE" on "PA_TICKETS_NOTIFICATION_EXECUTION"("N_EVENT_ID","N_RULE_VERSION_ID") where "N_RULE_VERSION_ID" is not null;
create index if not exists "IX_PA_TICKETS_NOTIFICATION_EXECUTION_TICKET" on "PA_TICKETS_NOTIFICATION_EXECUTION"("N_TICKET_ID","D_EVALUATED_AT");

alter table "PA_TICKETS_COMMUNICATION" add column if not exists "N_NOTIFICATION_EXECUTION_ID" bigint;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "N_NOTIFICATION_RULE_ID" bigint;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "N_NOTIFICATION_RULE_VERSION_ID" bigint;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "N_EVENT_ID" bigint;
create index if not exists "IX_PA_TICKETS_COMM_NOTIFICATION" on "PA_TICKETS_COMMUNICATION"("N_NOTIFICATION_EXECUTION_ID","N_NOTIFICATION_RULE_VERSION_ID","N_EVENT_ID");
