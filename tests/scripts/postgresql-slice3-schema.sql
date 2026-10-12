-- PA Tickets Phase 5 Slice 3 PostgreSQL-only additive communication schema.
create sequence if not exists "PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ";
create sequence if not exists "PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ";

alter table "PA_TICKETS_COMMUNICATION" add column if not exists "S_PUBLIC_ID" varchar(64);
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "N_TEMPLATE_VERSION_ID" bigint;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "D_NEXT_RETRY_AT" timestamptz;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "S_LEASE_OWNER" varchar(200);
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "D_LEASE_UNTIL" timestamptz;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "S_FAILURE_CODE" varchar(100);
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "S_FAILURE_CLASSIFICATION" varchar(30);
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "D_SENT_AT" timestamptz;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "D_DELIVERED_AT" timestamptz;
alter table "PA_TICKETS_COMMUNICATION" add column if not exists "D_FAILED_AT" timestamptz;
create unique index if not exists "UX_PA_TICKETS_COMM_PUBLIC_ID" on "PA_TICKETS_COMMUNICATION"("S_PUBLIC_ID") where "S_PUBLIC_ID" is not null;
create index if not exists "IX_PA_TICKETS_COMM_LEASE" on "PA_TICKETS_COMMUNICATION"("S_STATUS","D_NEXT_RETRY_AT","D_LEASE_UNTIL");

create table if not exists "PA_TICKETS_COMMUNICATION_TEMPLATE" (
  "N_TEMPLATE_VERSION_ID" bigint primary key default nextval('"PA_TICKETS_COMMUNICATION_TEMPLATE_SEQ"'),
  "S_TEMPLATE_CODE" varchar(200) not null,
  "S_CHANNEL" varchar(30) not null,
  "N_VERSION" integer not null,
  "S_SUBJECT" varchar(1000),
  "S_BODY" text not null,
  "S_STATUS" varchar(30) not null default 'ACTIVE',
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  unique ("S_TEMPLATE_CODE","S_CHANNEL","N_VERSION")
);
create index if not exists "IX_PA_TICKETS_COMM_TEMPLATE_ACTIVE" on "PA_TICKETS_COMMUNICATION_TEMPLATE"("S_TEMPLATE_CODE","S_CHANNEL","S_STATUS","D_EFFECTIVE_FROM");

create table if not exists "PA_TICKETS_COMMUNICATION_ATTEMPT" (
  "N_ATTEMPT_ID" bigint primary key default nextval('"PA_TICKETS_COMMUNICATION_ATTEMPT_SEQ"'),
  "N_COMMUNICATION_ID" bigint not null references "PA_TICKETS_COMMUNICATION"("N_COMMUNICATION_ID"),
  "N_ATTEMPT_NO" integer not null,
  "D_STARTED_AT" timestamptz not null,
  "D_COMPLETED_AT" timestamptz not null,
  "S_PROVIDER" varchar(100) not null,
  "S_PROVIDER_MESSAGE_ID" varchar(500),
  "S_RESULT" varchar(50) not null,
  "S_FAILURE_CLASSIFICATION" varchar(30),
  "S_FAILURE_CODE" varchar(100),
  "S_FAILURE_REASON" text,
  "S_CORRELATION_ID" varchar(100),
  unique ("N_COMMUNICATION_ID","N_ATTEMPT_NO")
);
create index if not exists "IX_PA_TICKETS_COMM_ATTEMPT_COMM" on "PA_TICKETS_COMMUNICATION_ATTEMPT"("N_COMMUNICATION_ID","N_ATTEMPT_NO");
alter table "PA_TICKETS_COMMUNICATION_ATTEMPT" drop constraint if exists "PA_TICKETS_COMMUNICATION_ATTEMPT_N_COMMUNICATION_ID_fkey";
alter table "PA_TICKETS_COMMUNICATION_ATTEMPT" add constraint "PA_TICKETS_COMMUNICATION_ATTEMPT_N_COMMUNICATION_ID_fkey" foreign key ("N_COMMUNICATION_ID") references "PA_TICKETS_COMMUNICATION"("N_COMMUNICATION_ID") on delete cascade;
