-- Notification orchestration additive PostgreSQL schema.
create sequence if not exists "PA_TICKETS_NOTIFICATION_RULE_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_RULE_VERSION_SEQ";
create sequence if not exists "PA_TICKETS_NOTIFICATION_EXECUTION_SEQ";

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
