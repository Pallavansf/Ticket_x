create sequence if not exists "PA_TICKETS_SEQ";
create sequence if not exists "PA_TICKETS_TICKET_NO_SEQ";
create sequence if not exists "PA_TICKETS_WORK_ITEM_SEQ";
create sequence if not exists "PA_TICKETS_EVENT_SEQ";
create sequence if not exists "PA_TICKETS_AUDIT_SEQ";
create sequence if not exists "PA_TICKETS_ASSIGNMENT_SEQ";
create table if not exists "PA_TICKETS" (
  "N_TICKET_ID" bigint primary key default nextval('"PA_TICKETS_SEQ"'),
  "S_TICKET_NO" varchar(64) not null unique,
  "N_TICKET_TYPE_ID" integer not null,
  "N_SERVICE_ID" bigint,
  "N_CATEGORY_ID" bigint,
  "N_REQUEST_TYPE_ID" bigint,
  "N_CLIENT_ID" bigint,
  "N_REQUESTED_BY" bigint not null,
  "S_SUBJECT" varchar(500) not null,
  "S_DESCRIPTION" text,
  "N_PRIORITY_ID" bigint,
  "N_STATUS_ID" integer not null,
  "N_CURRENT_DEPARTMENT_ID" bigint,
  "N_CURRENT_TEAM_ID" bigint,
  "N_CURRENT_ASSIGNEE" bigint,
  "D_RESOLVED_AT" timestamptz,
  "D_CLOSED_AT" timestamptz,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  "N_EVIDENCE_COUNT" integer not null default 0,
  "N_APPROVAL_REQUIRED" integer not null default 0,
  "N_APPROVED" integer not null default 0,
  "N_LAST_EXECUTOR" bigint
);
create table if not exists "PA_TICKETS_WORK_ITEM" (
  "N_WORK_ITEM_ID" bigint primary key default nextval('"PA_TICKETS_WORK_ITEM_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "S_WORK_ITEM_NO" varchar(64) not null,
  "N_TO_DEPARTMENT_ID" bigint,
  "N_TO_TEAM_ID" bigint,
  "N_TO_USER_ID" bigint,
  "N_PRIORITY_ID" bigint,
  "S_STATUS_CODE" varchar(32) not null,
  "S_WAITING_FOR" varchar(200),
  "N_WAITING_MINUTES" integer not null default 0,
  "D_WAITING_STARTED_AT" timestamptz,
  "D_WAITING_RESUMED_AT" timestamptz,
  "D_STARTED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  "S_PUBLIC_WORK_ITEM_ID" varchar(16),
  constraint "UX_PA_TICKETS_WORK_ITEM_PUBLIC" unique ("S_PUBLIC_WORK_ITEM_ID")
);
create unique index if not exists "UX_PA_TICKETS_WORK_ITEM_TICKET_NO" on "PA_TICKETS_WORK_ITEM"("N_TICKET_ID","S_WORK_ITEM_NO");
alter table "PA_TICKETS_WORK_ITEM" add column if not exists "S_WAITING_COMMENT" text;
alter table "PA_TICKETS_WORK_ITEM" add column if not exists "S_WAITING_EVIDENCE" text;
alter table "PA_TICKETS_WORK_ITEM" add column if not exists "D_WAITING_EXPECTED_AT" timestamptz;
create sequence if not exists "PA_TICKETS_WAITING_REASON_SEQ";
create table if not exists "PA_TICKETS_WAITING_REASON" (
  "N_WAITING_REASON_ID" bigint primary key default nextval('"PA_TICKETS_WAITING_REASON_SEQ"'),
  "S_REASON_CODE" varchar(100) not null,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "N_PAUSE_SLA" boolean not null default true,
  "N_REQUIRE_COMMENT" boolean not null default false,
  "N_REQUIRE_EVIDENCE" boolean not null default false,
  "N_REQUIRE_EXPECTED_DATE" boolean not null default false,
  "S_ALLOWED_FROM_STATUSES" varchar(200) not null default 'InProgress'
);
create index if not exists "IX_PA_TICKETS_WAITING_REASON_EFFECTIVE" on "PA_TICKETS_WAITING_REASON"("S_REASON_CODE","D_EFFECTIVE_FROM","D_EFFECTIVE_TO");
insert into "PA_TICKETS_WAITING_REASON" ("S_REASON_CODE","D_EFFECTIVE_FROM","N_PAUSE_SLA","S_ALLOWED_FROM_STATUSES")
select 'WAITING_CUSTOMER', timestamptz '-infinity', true, 'InProgress'
where not exists (select 1 from "PA_TICKETS_WAITING_REASON" where "S_REASON_CODE" = 'WAITING_CUSTOMER');
insert into "PA_TICKETS_WAITING_REASON" ("S_REASON_CODE","D_EFFECTIVE_FROM","N_PAUSE_SLA","S_ALLOWED_FROM_STATUSES")
select 'CLIENT', timestamptz '-infinity', true, 'InProgress'
where not exists (select 1 from "PA_TICKETS_WAITING_REASON" where "S_REASON_CODE" = 'CLIENT');
insert into "PA_TICKETS_WAITING_REASON" ("S_REASON_CODE","D_EFFECTIVE_FROM","N_PAUSE_SLA","S_ALLOWED_FROM_STATUSES")
select 'WAITING_VENDOR', timestamptz '-infinity', true, 'InProgress'
where not exists (select 1 from "PA_TICKETS_WAITING_REASON" where "S_REASON_CODE" = 'WAITING_VENDOR');
create table if not exists "PA_TICKETS_ASSIGNMENT" (
  "N_ASSIGNMENT_ID" bigint primary key default nextval('"PA_TICKETS_ASSIGNMENT_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint references "PA_TICKETS_WORK_ITEM"("N_WORK_ITEM_ID"),
  "N_ASSIGNED_TO" bigint,
  "S_ASSIGNMENT_TYPE" varchar(32) not null,
  "D_ASSIGNED_AT" timestamptz not null,
  "N_ASSIGNED_BY" bigint not null,
  "S_REASON" varchar(1000),
  "D_UNASSIGNED_AT" timestamptz
);
create unique index if not exists "UX_PA_TICKETS_ASSIGNMENT_ACTIVE" on "PA_TICKETS_ASSIGNMENT"("N_WORK_ITEM_ID") where "D_UNASSIGNED_AT" is null;
create table if not exists "PA_TICKETS_EVENT" (
  "N_EVENT_ID" bigint primary key default nextval('"PA_TICKETS_EVENT_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORK_ITEM_ID" bigint,
  "S_EVENT_TYPE" varchar(100) not null,
  "S_EVENT_CATEGORY" varchar(50) not null,
  "N_ACTOR_USER_ID" bigint not null,
  "S_OLD_VALUE" text,
  "S_NEW_VALUE" text,
  "S_REASON" text,
  "D_EVENT_AT" timestamptz not null
);
create table if not exists "PA_TICKETS_AUDIT" (
  "N_AUDIT_ID" bigint primary key default nextval('"PA_TICKETS_AUDIT_SEQ"'),
  "S_ENTITY_TYPE" varchar(100) not null,
  "S_ENTITY_ID" varchar(200) not null,
  "S_ACTION" varchar(100) not null,
  "N_ACTOR_USER_ID" bigint not null,
  "D_AUDIT_AT" timestamptz not null,
  "S_OLD_VALUE" text,
  "S_NEW_VALUE" text,
  "S_REASON" text,
  "S_HASH" varchar(128) not null,
  "S_PREVIOUS_HASH" varchar(128)
);
create table if not exists "PA_TICKETS_AUDIT_CHAIN_LOCK" (
  "S_ENTITY_TYPE" varchar(100) not null,
  "S_ENTITY_ID" varchar(200) not null,
  primary key ("S_ENTITY_TYPE", "S_ENTITY_ID")
);
create table if not exists "PA_TICKETS_AUDIT_MAINTENANCE" (
  "N_MAINTENANCE_ID" bigint generated by default as identity primary key,
  "N_TICKET_ID" bigint not null,
  "S_ACTION" varchar(100) not null,
  "S_ACTOR" varchar(200) not null,
  "D_OCCURRED_AT" timestamptz not null,
  "N_DELETED_ROWS" integer not null
);
create table if not exists "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" (
  "N_TOKEN_ID" integer primary key,
  "S_TOKEN" varchar(128) not null
);
insert into "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" ("N_TOKEN_ID","S_TOKEN")
select 1, md5(random()::text || clock_timestamp()::text)
where not exists (select 1 from "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" where "N_TOKEN_ID" = 1);
revoke all on table "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" from public;
create or replace function pa_tickets_audit_guard() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare expected_token text;
begin
  select "S_TOKEN" into expected_token from "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" where "N_TOKEN_ID" = 1;
  if current_setting('pa_tickets.audit_maintenance_token', true) is distinct from expected_token then
    raise exception 'PA_TICKETS_AUDIT is append-only; maintenance authorization is required.' using errcode = '42501';
  end if;
  return coalesce(new, old);
end;
$$;
revoke all on function pa_tickets_audit_guard() from public;
drop trigger if exists "TR_PA_TICKETS_AUDIT_GUARD" on "PA_TICKETS_AUDIT";
create trigger "TR_PA_TICKETS_AUDIT_GUARD"
before update or delete on "PA_TICKETS_AUDIT"
for each row execute function pa_tickets_audit_guard();
create or replace function pa_tickets_maintenance_delete_audit_for_ticket(ticket_id bigint) returns void
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare maintenance_token text; deleted_count integer;
begin
  select "S_TOKEN" into maintenance_token from "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" where "N_TOKEN_ID" = 1;
  perform set_config('pa_tickets.audit_maintenance_token', maintenance_token, true);
  delete from "PA_TICKETS_AUDIT"
   where "S_ENTITY_ID" = ticket_id::text
      or "S_ENTITY_ID" in (select "N_WORK_ITEM_ID"::text from "PA_TICKETS_WORK_ITEM" where "N_TICKET_ID" = ticket_id);
  get diagnostics deleted_count = row_count;
  insert into "PA_TICKETS_AUDIT_MAINTENANCE" ("N_TICKET_ID","S_ACTION","S_ACTOR","D_OCCURRED_AT","N_DELETED_ROWS")
  values (ticket_id, 'AUDIT_DISPOSAL', session_user, clock_timestamp(), deleted_count);
  perform set_config('pa_tickets.audit_maintenance_token', '', true);
end;
$$;
revoke all on function pa_tickets_maintenance_delete_audit_for_ticket(bigint) from public;
create or replace function pa_tickets_maintenance_delete_audit_entity(entity_type text, entity_id text) returns void
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare maintenance_token text; deleted_count integer;
begin
  select "S_TOKEN" into maintenance_token from "PA_TICKETS_AUDIT_MAINTENANCE_TOKEN" where "N_TOKEN_ID" = 1;
  perform set_config('pa_tickets.audit_maintenance_token', maintenance_token, true);
  delete from "PA_TICKETS_AUDIT" where "S_ENTITY_TYPE" = entity_type and "S_ENTITY_ID" = entity_id;
  get diagnostics deleted_count = row_count;
  insert into "PA_TICKETS_AUDIT_MAINTENANCE" ("N_TICKET_ID","S_ACTION","S_ACTOR","D_OCCURRED_AT","N_DELETED_ROWS") values (0, 'AUDIT_DISPOSAL:' || entity_type || ':' || entity_id, session_user, clock_timestamp(), deleted_count);
  perform set_config('pa_tickets.audit_maintenance_token', '', true);
end;
$$;
revoke all on function pa_tickets_maintenance_delete_audit_entity(text,text) from public;



create sequence if not exists "PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_INSTANCE_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_HISTORY_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_DELEGATION_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_RULE_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_RULE_STEP_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_RULE_VERSION_SEQ";
create sequence if not exists "PA_TICKETS_SLA_PAUSE_SEQ";
create sequence if not exists "PA_TICKETS_IDEMPOTENCY_SEQ";
create sequence if not exists "PA_TICKETS_BACKGROUND_JOB_SEQ";

alter table "PA_TICKETS_WORK_ITEM" add column if not exists "N_SEQUENCE_NO" integer;
alter table "PA_TICKETS_WORK_ITEM" add column if not exists "N_WORKFLOW_STEP_ID" bigint;
alter table "PA_TICKETS_WORK_ITEM" add column if not exists "N_WORKFLOW_STEP_INSTANCE_ID" bigint;
alter table "PA_TICKETS_ASSIGNMENT" add column if not exists "N_DEPARTMENT_ID" bigint;
alter table "PA_TICKETS_ASSIGNMENT" add column if not exists "N_TEAM_ID" bigint;

create table if not exists "PA_TICKETS_WORKFLOW" (
  "N_WORKFLOW_ID" bigint primary key,
  "S_WORKFLOW_CODE" varchar(100) not null,
  "S_WORKFLOW_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null default clock_timestamp(),
  "N_CREATED_BY" bigint not null default 0
);
alter table "PA_TICKETS_WORKFLOW" add column if not exists "S_DESCRIPTION" text;
alter table "PA_TICKETS_WORKFLOW" add column if not exists "N_ACTIVE" boolean not null default true;
alter table "PA_TICKETS_WORKFLOW" add column if not exists "D_CREATED_AT" timestamptz not null default clock_timestamp();
alter table "PA_TICKETS_WORKFLOW" add column if not exists "N_CREATED_BY" bigint not null default 0;
create unique index if not exists "UX_PA_TICKETS_WORKFLOW_CODE" on "PA_TICKETS_WORKFLOW"("S_WORKFLOW_CODE");
create table if not exists "PA_TICKETS_WORKFLOW_VERSION" (
  "N_WORKFLOW_VERSION_ID" bigint primary key,
  "N_WORKFLOW_ID" bigint not null,
  "N_VERSION_NO" integer not null,
  "S_STATUS" varchar(32) not null,
  "S_DEFINITION" text,
  "D_CREATED_AT" timestamptz not null default clock_timestamp(),
  "N_CREATED_BY" bigint not null default 0,
  "D_PUBLISHED_AT" timestamptz,
  "N_PUBLISHED_BY" bigint,
  "D_RETIRED_AT" timestamptz,
  "N_RETIRED_BY" bigint,
  "N_ROW_VERSION" bigint not null default 0
);
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "D_CREATED_AT" timestamptz not null default clock_timestamp();
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "N_CREATED_BY" bigint not null default 0;
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "D_PUBLISHED_AT" timestamptz;
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "N_PUBLISHED_BY" bigint;
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "D_RETIRED_AT" timestamptz;
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "N_RETIRED_BY" bigint;
alter table "PA_TICKETS_WORKFLOW_VERSION" add column if not exists "N_ROW_VERSION" bigint not null default 0;
create unique index if not exists "UX_PA_TICKETS_WFV_CODE" on "PA_TICKETS_WORKFLOW_VERSION"("N_WORKFLOW_ID","N_VERSION_NO");
create unique index if not exists "UX_PA_TICKETS_WFV_ID_WF" on "PA_TICKETS_WORKFLOW_VERSION"("N_WORKFLOW_VERSION_ID","N_WORKFLOW_ID");
create table if not exists "PA_TICKETS_WORKFLOW_STEP" (
  "N_WORKFLOW_STEP_ID" bigint primary key,
  "N_WORKFLOW_VERSION_ID" bigint not null,
  "N_STEP_NO" integer not null,
  "S_STEP_CODE" varchar(100) not null,
  "S_STEP_NAME" varchar(200) not null,
  "S_STEP_TYPE" varchar(32) not null,
  "S_EXECUTION_MODE" varchar(32) not null,
  "N_REQUIRED" boolean not null default true,
  "N_DEPARTMENT_ID" bigint,
  "N_TEAM_ID" bigint
);
alter table "PA_TICKETS_WORKFLOW_STEP" add column if not exists "N_REQUIRED" boolean not null default true;
alter table "PA_TICKETS_WORKFLOW_STEP" add column if not exists "N_DEPARTMENT_ID" bigint;
alter table "PA_TICKETS_WORKFLOW_STEP" add column if not exists "N_TEAM_ID" bigint;
create unique index if not exists "UX_PA_TICKETS_WF_STEP_CODE" on "PA_TICKETS_WORKFLOW_STEP"("N_WORKFLOW_VERSION_ID","S_STEP_CODE");
create table if not exists "PA_TICKETS_WORKFLOW_TRANSITION" (
  "N_TRANSITION_ID" bigint primary key,
  "N_WORKFLOW_VERSION_ID" bigint not null,
  "N_FROM_STEP_ID" bigint,
  "N_TO_STEP_ID" bigint not null,
  "S_TRANSITION_CODE" varchar(100) not null,
  "S_TRANSITION_NAME" varchar(300),
  "S_CONDITION" text
);
alter table "PA_TICKETS_WORKFLOW_TRANSITION" add column if not exists "S_TRANSITION_NAME" varchar(300);
create unique index if not exists "UX_PA_TICKETS_WF_TRANSITION_CODE" on "PA_TICKETS_WORKFLOW_TRANSITION"("N_WORKFLOW_VERSION_ID","S_TRANSITION_CODE");

create table if not exists "PA_TICKETS_APPROVAL_RULE" (
  "N_APPROVAL_RULE_ID" bigint primary key,
  "S_APPROVAL_CODE" varchar(100) not null unique,
  "S_APPROVAL_NAME" varchar(300) not null,
  "S_DESCRIPTION" text,
  "S_TRIGGER_RULE" text,
  "S_CONFIGURATION" text,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null default clock_timestamp(),
  "N_CREATED_BY" bigint not null default 0,
  "D_UPDATED_AT" timestamptz,
  "N_UPDATED_BY" bigint
);
create table if not exists "PA_TICKETS_APPROVAL_RULE_VERSION" (
  "N_APPROVAL_RULE_VERSION_ID" bigint primary key default nextval('"PA_TICKETS_APPROVAL_RULE_VERSION_SEQ"'),
  "N_APPROVAL_RULE_ID" bigint not null references "PA_TICKETS_APPROVAL_RULE"("N_APPROVAL_RULE_ID"),
  "N_VERSION_NO" integer not null,
  "S_STATUS" varchar(20) not null default 'DRAFT',
  "S_MODE" varchar(20) not null,
  "S_REJECTION_BEHAVIOR" varchar(20) not null default 'REJECT',
  "N_EXPIRY_MINUTES" integer,
  "N_DELEGATION_ALLOWED" boolean not null default true,
  "N_REQUIRE_REQUESTER_SOD" boolean not null default true,
  "N_REQUIRE_CREATOR_SOD" boolean not null default true,
  "N_REQUIRE_EXECUTOR_SOD" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "D_CREATED_AT" timestamptz not null default clock_timestamp(),
  "N_CREATED_BY" bigint not null default 0,
  "D_PUBLISHED_AT" timestamptz,
  "N_PUBLISHED_BY" bigint,
  "D_RETIRED_AT" timestamptz,
  "N_RETIRED_BY" bigint,
  "N_ROW_VERSION" bigint not null default 0,
  unique ("N_APPROVAL_RULE_ID","N_VERSION_NO"),
  unique ("N_APPROVAL_RULE_VERSION_ID","N_APPROVAL_RULE_ID"),
  check ("S_STATUS" in ('DRAFT','PUBLISHED','RETIRED')),
  check ("S_MODE" in ('ANY_ONE','ALL_REQUIRED','SEQUENTIAL')),
  check ("S_REJECTION_BEHAVIOR" in ('REJECT','CANCEL')),
  check ("D_EFFECTIVE_TO" is null or "D_EFFECTIVE_TO" > "D_EFFECTIVE_FROM"),
  check ("N_EXPIRY_MINUTES" is null or "N_EXPIRY_MINUTES" > 0)
);
create table if not exists "PA_TICKETS_APPROVAL_RULE_STEP" (
  "N_APPROVAL_RULE_STEP_ID" bigint primary key default nextval('"PA_TICKETS_APPROVAL_RULE_STEP_SEQ"'),
  "N_APPROVAL_RULE_ID" bigint not null references "PA_TICKETS_APPROVAL_RULE"("N_APPROVAL_RULE_ID"),
  "N_APPROVAL_RULE_VERSION_ID" bigint,
  "N_STEP_NO" integer not null,
  "S_APPROVER_TYPE" varchar(50) not null,
  "N_ROLE_ID" bigint,
  "N_DEPARTMENT_ID" bigint,
  "N_TEAM_ID" bigint,
  "N_USER_ID" bigint,
  "S_RULE" text,
  "S_APPROVAL_MODE" varchar(30) not null default 'ALL',
  "N_REQUIRED" boolean not null default true,
  "N_ACTIVE" boolean not null default true,
  "S_TARGET_KEY" varchar(160),
  check ("S_APPROVER_TYPE" in ('USER','ROLE','DEPARTMENT','TEAM')),
  check ("N_APPROVAL_RULE_VERSION_ID" is null or "S_TARGET_KEY" is not null),
  foreign key ("N_APPROVAL_RULE_VERSION_ID","N_APPROVAL_RULE_ID") references "PA_TICKETS_APPROVAL_RULE_VERSION"("N_APPROVAL_RULE_VERSION_ID","N_APPROVAL_RULE_ID")
);
alter table "PA_TICKETS_APPROVAL_RULE_STEP" add column if not exists "N_APPROVAL_RULE_VERSION_ID" bigint;
alter table "PA_TICKETS_APPROVAL_RULE_STEP" add column if not exists "N_REQUIRED" boolean not null default true;
alter table "PA_TICKETS_APPROVAL_RULE_STEP" add column if not exists "S_TARGET_KEY" varchar(160);
create unique index if not exists "UX_PA_TICKETS_APRS_VERSION_TARGET" on "PA_TICKETS_APPROVAL_RULE_STEP"("N_APPROVAL_RULE_VERSION_ID","N_STEP_NO","S_TARGET_KEY") where "N_APPROVAL_RULE_VERSION_ID" is not null;

create table if not exists "PA_TICKETS_WORKFLOW_INSTANCE" (
  "N_WORKFLOW_INSTANCE_ID" bigint primary key,
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_WORKFLOW_VERSION_ID" bigint not null,
  "N_CURRENT_STEP_ID" bigint,
  "S_STATUS" varchar(32) not null,
  "N_ROW_VERSION" integer not null default 0
);
create table if not exists "PA_TICKETS_WORKFLOW_STEP_INSTANCE" (
  "N_STEP_INSTANCE_ID" bigint primary key,
  "N_WORKFLOW_INSTANCE_ID" bigint not null,
  "N_WORKFLOW_STEP_ID" bigint not null,
  "N_WORK_ITEM_ID" bigint,
  "N_APPROVAL_ID" bigint,
  "S_STATUS" varchar(32) not null,
  "S_JOIN_RULE" varchar(32),
  "D_STARTED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "N_ROW_VERSION" integer not null default 0
);
create unique index if not exists "UX_PA_TICKETS_STEP_INSTANCE_ONE" on "PA_TICKETS_WORKFLOW_STEP_INSTANCE"("N_WORKFLOW_INSTANCE_ID","N_WORKFLOW_STEP_ID");
create table if not exists "PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY" (
  "N_STEP_INSTANCE_HISTORY_ID" bigint primary key default nextval('"PA_TICKETS_WORKFLOW_STEP_INSTANCE_HISTORY_SEQ"'),
  "N_STEP_INSTANCE_ID" bigint not null,
  "S_ACTION" varchar(100) not null,
  "S_OLD_STATUS" varchar(32),
  "S_NEW_STATUS" varchar(32),
  "N_ACTOR_USER_ID" bigint,
  "D_EVENT_AT" timestamptz not null,
  "S_CORRELATION_ID" varchar(100),
  "S_IDEMPOTENCY_KEY" varchar(200)
);
create table if not exists "PA_TICKETS_APPROVAL_INSTANCE" (
  "N_APPROVAL_INSTANCE_ID" bigint primary key default nextval('"PA_TICKETS_APPROVAL_INSTANCE_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_STEP_INSTANCE_ID" bigint,
  "S_MODE" varchar(32) not null,
  "S_STATUS" varchar(32) not null,
  "N_REQUESTER_USER_ID" bigint not null,
  "N_CREATOR_USER_ID" bigint not null,
  "N_EXECUTOR_USER_ID" bigint,
  "N_APPROVAL_RULE_VERSION_ID" bigint,
  "N_DELEGATION_ALLOWED" boolean not null default true,
  "S_REJECTION_BEHAVIOR" varchar(32) not null default 'REJECT',
  "N_CURRENT_LEVEL" integer not null default 1,
  "D_CREATED_AT" timestamptz not null,
  "D_COMPLETED_AT" timestamptz,
  "N_ROW_VERSION" integer not null default 0
);
alter table "PA_TICKETS_APPROVAL_INSTANCE" add column if not exists "N_APPROVAL_RULE_VERSION_ID" bigint;
alter table "PA_TICKETS_APPROVAL_INSTANCE" add column if not exists "N_DELEGATION_ALLOWED" boolean not null default true;
do $$ begin
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_AI_RULE_VER') then
    alter table "PA_TICKETS_APPROVAL_INSTANCE" add constraint "FK_PA_TICKETS_AI_RULE_VER" foreign key ("N_APPROVAL_RULE_VERSION_ID") references "PA_TICKETS_APPROVAL_RULE_VERSION"("N_APPROVAL_RULE_VERSION_ID");
  end if;
end $$;
create table if not exists "PA_TICKETS_APPROVAL" (
  "N_APPROVAL_ID" bigint primary key,
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "S_STATUS" varchar(32) not null,
  "S_DECISION" varchar(32),
  "S_COMMENTS" text,
  "N_APPROVER_USER_ID" bigint,
  "D_DECIDED_AT" timestamptz,
  "N_ROW_VERSION" integer not null default 0
);
create table if not exists "PA_TICKETS_APPROVAL_HISTORY" (
  "N_APPROVAL_HISTORY_ID" bigint primary key default nextval('"PA_TICKETS_APPROVAL_HISTORY_SEQ"'),
  "N_APPROVAL_ID" bigint not null,
  "S_ACTION" varchar(100) not null,
  "S_DECISION" varchar(32),
  "N_ACTOR_USER_ID" bigint not null,
  "S_COMMENT" text,
  "D_CREATED_AT" timestamptz not null,
  "S_CORRELATION_ID" varchar(100),
  "S_IDEMPOTENCY_KEY" varchar(200)
);
alter table "PA_TICKETS_APPROVAL" add column if not exists "N_APPROVAL_INSTANCE_ID" bigint;
alter table "PA_TICKETS_APPROVAL" add column if not exists "N_APPROVAL_LEVEL" integer not null default 1;
alter table "PA_TICKETS_APPROVAL" add column if not exists "N_REQUIRED" boolean not null default true;
alter table "PA_TICKETS_APPROVAL" add column if not exists "N_ACTIVE" boolean not null default true;
alter table "PA_TICKETS_APPROVAL" add column if not exists "D_EXPIRES_AT" timestamptz;
create index if not exists "IX_PA_TICKETS_APPROVAL_INSTANCE" on "PA_TICKETS_APPROVAL"("N_APPROVAL_INSTANCE_ID");
do $$ begin
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_APPROVAL_INSTANCE') then
    alter table "PA_TICKETS_APPROVAL" add constraint "FK_PA_TICKETS_APPROVAL_INSTANCE"
      foreign key ("N_APPROVAL_INSTANCE_ID") references "PA_TICKETS_APPROVAL_INSTANCE"("N_APPROVAL_INSTANCE_ID");
  end if;
end $$;
alter table "PA_TICKETS_APPROVAL_HISTORY" add column if not exists "S_COMMENT" text;
create table if not exists "PA_TICKETS_APPROVAL_DELEGATION" (
  "N_APPROVAL_DELEGATION_ID" bigint primary key default nextval('"PA_TICKETS_APPROVAL_DELEGATION_SEQ"'),
  "N_APPROVAL_ID" bigint not null,
  "N_ORIGINAL_APPROVER" bigint not null,
  "N_DELEGATE_USER_ID" bigint not null,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "S_REASON" text,
  "N_CREATED_BY" bigint not null,
  "D_CREATED_AT" timestamptz not null
);
create table if not exists "PA_TICKETS_SLA" (
  "N_SLA_ID" bigint primary key,
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "S_STATUS" varchar(32) not null,
  "D_PAUSED_AT" timestamptz,
  "D_COMPLETED_AT" timestamptz,
  "D_BREACHED_AT" timestamptz,
  "N_ROW_VERSION" integer not null default 0
);
alter table "PA_TICKETS_SLA" add column if not exists "N_POLICY_ID" bigint;
alter table "PA_TICKETS_SLA" add column if not exists "N_POLICY_VERSION" integer;
alter table "PA_TICKETS_SLA" add column if not exists "S_CALENDAR_CODE" varchar(100);
alter table "PA_TICKETS_SLA" add column if not exists "D_STARTED_AT" timestamptz;
alter table "PA_TICKETS_SLA" add column if not exists "D_RESPONSE_DUE_AT" timestamptz;
alter table "PA_TICKETS_SLA" add column if not exists "D_RESOLUTION_DUE_AT" timestamptz;
create table if not exists "PA_TICKETS_SLA_POLICY" (
  "N_SLA_POLICY_VERSION_ID" bigint primary key,
  "N_POLICY_ID" bigint not null,
  "S_POLICY_CODE" varchar(100) not null,
  "N_VERSION_NO" integer not null,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "N_SERVICE_ID" bigint,
  "N_REQUEST_TYPE_ID" bigint,
  "N_PRIORITY_ID" bigint,
  "N_CLIENT_ID" bigint,
  "N_DEPARTMENT_ID" bigint,
  "N_TEAM_ID" bigint,
  "N_TICKET_TYPE" integer,
  "N_RESPONSE_TARGET_MINUTES" integer,
  "N_RESOLUTION_TARGET_MINUTES" integer,
  "S_CALENDAR_CODE" varchar(100) not null,
  "S_WAITING_RULES" text,
  "S_PAUSE_RULES" text,
  "S_ESCALATION_RULES" text,
  "S_BREACH_RULES" text,
  "S_NOTIFICATION_RULES" text
);
create index if not exists "IX_PA_TICKETS_SLA_POLICY_TARGET" on "PA_TICKETS_SLA_POLICY"("N_SERVICE_ID","N_REQUEST_TYPE_ID","N_PRIORITY_ID","N_CLIENT_ID","N_DEPARTMENT_ID","N_TEAM_ID","N_TICKET_TYPE");
create table if not exists "PA_TICKETS_SLA_CALENDAR" (
  "N_SLA_CALENDAR_ID" bigint primary key,
  "S_CALENDAR_CODE" varchar(100) not null unique,
  "N_ACTIVE" boolean not null default true,
  "S_TIME_ZONE_ID" varchar(100) not null,
  "S_WORKING_HOURS_JSON" text not null,
  "S_HOLIDAYS_JSON" text not null
);
create table if not exists "PA_TICKETS_SLA_PAUSE" (
  "N_SLA_PAUSE_ID" bigint primary key default nextval('"PA_TICKETS_SLA_PAUSE_SEQ"'),
  "N_SLA_ID" bigint not null,
  "S_REASON_CODE" varchar(100) not null,
  "S_REASON" text,
  "D_STARTED_AT" timestamptz not null,
  "D_ENDED_AT" timestamptz,
  "N_STARTED_BY" bigint not null,
  "N_ENDED_BY" bigint
);
create or replace function pa_tickets_sync_waiting_sla() returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public
as $$
declare pause_sla boolean; current_sla record;
begin
  if new."S_STATUS_CODE" = 'WAITING' and old."S_STATUS_CODE" is distinct from new."S_STATUS_CODE" then
    select "N_PAUSE_SLA" into pause_sla
      from "PA_TICKETS_WAITING_REASON"
     where "S_REASON_CODE" = new."S_WAITING_FOR"
       and "N_ACTIVE" = true
       and "D_EFFECTIVE_FROM" <= clock_timestamp()
       and ("D_EFFECTIVE_TO" is null or "D_EFFECTIVE_TO" > clock_timestamp());
    if coalesce(pause_sla, false) then
      for current_sla in select * from "PA_TICKETS_SLA" where "N_TICKET_ID" = new."N_TICKET_ID" and "S_STATUS" = 'RUNNING' for update loop
        update "PA_TICKETS_SLA" set "S_STATUS" = 'PAUSED', "D_PAUSED_AT" = clock_timestamp(), "N_ROW_VERSION" = "N_ROW_VERSION" + 1 where "N_SLA_ID" = current_sla."N_SLA_ID";
        insert into "PA_TICKETS_SLA_PAUSE" ("N_SLA_ID","S_REASON_CODE","S_REASON","D_STARTED_AT","N_STARTED_BY") values (current_sla."N_SLA_ID",new."S_WAITING_FOR",new."S_WAITING_COMMENT",clock_timestamp(),new."N_CREATED_BY");
      end loop;
    end if;
  elsif old."S_STATUS_CODE" = 'WAITING' and new."S_STATUS_CODE" = 'IN_PROGRESS' then
    for current_sla in select * from "PA_TICKETS_SLA" where "N_TICKET_ID" = new."N_TICKET_ID" and "S_STATUS" = 'PAUSED' for update loop
      update "PA_TICKETS_SLA" set "S_STATUS" = 'RUNNING', "D_PAUSED_AT" = null, "N_ROW_VERSION" = "N_ROW_VERSION" + 1 where "N_SLA_ID" = current_sla."N_SLA_ID";
      update "PA_TICKETS_SLA_PAUSE" set "D_ENDED_AT" = clock_timestamp(), "N_ENDED_BY" = new."N_CREATED_BY" where "N_SLA_ID" = current_sla."N_SLA_ID" and "D_ENDED_AT" is null;
    end loop;
  end if;
  return new;
end;
$$;
revoke all on function pa_tickets_sync_waiting_sla() from public;
drop trigger if exists "TR_PA_TICKETS_WAITING_SLA" on "PA_TICKETS_WORK_ITEM";
create trigger "TR_PA_TICKETS_WAITING_SLA"
after update of "S_STATUS_CODE" on "PA_TICKETS_WORK_ITEM"
for each row execute function pa_tickets_sync_waiting_sla();
create table if not exists "PA_TICKETS_IDEMPOTENCY" (
  "N_IDEMPOTENCY_ID" bigint primary key default nextval('"PA_TICKETS_IDEMPOTENCY_SEQ"'),
  "S_SCOPE" varchar(100) not null,
  "S_IDEMPOTENCY_KEY" varchar(200) not null,
  "S_REQUEST_FINGERPRINT" varchar(500),
  "S_STATUS" varchar(32) not null,
  "N_ACTOR_USER_ID" bigint,
  "N_RESULT_TICKET_ID" bigint,
  "D_CREATED_AT" timestamptz not null,
  "D_COMPLETED_AT" timestamptz
);
create unique index if not exists "UX_PA_TICKETS_IDEMPOTENCY_SCOPE_ACTOR_KEY" on "PA_TICKETS_IDEMPOTENCY"("S_SCOPE","N_ACTOR_USER_ID","S_IDEMPOTENCY_KEY") where "N_ACTOR_USER_ID" is not null;
create unique index if not exists "UX_PA_TICKETS_IDEMPOTENCY_SCOPE_KEY" on "PA_TICKETS_IDEMPOTENCY"("S_SCOPE","S_IDEMPOTENCY_KEY") where "N_ACTOR_USER_ID" is null;
create table if not exists "PA_TICKETS_BACKGROUND_JOB" (
  "N_BACKGROUND_JOB_ID" bigint primary key default nextval('"PA_TICKETS_BACKGROUND_JOB_SEQ"'),
  "S_JOB_TYPE" varchar(100) not null,
  "S_DEDUPE_KEY" varchar(200) not null,
  "S_PAYLOAD" text,
  "S_STATUS" varchar(32) not null,
  "N_ATTEMPT_COUNT" integer not null default 0,
  "D_SCHEDULED_AT" timestamptz not null,
  "D_NEXT_ATTEMPT_AT" timestamptz,
  "D_LOCKED_AT" timestamptz,
  "S_LOCKED_BY" varchar(200),
  "S_LAST_ERROR" text,
  "D_COMPLETED_AT" timestamptz
);
create unique index if not exists "UX_PA_TICKETS_BACKGROUND_JOB_DEDUPE" on "PA_TICKETS_BACKGROUND_JOB"("S_JOB_TYPE","S_DEDUPE_KEY");

create sequence if not exists "PA_TICKETS_WORKFLOW_SEQ";
create sequence if not exists "PA_TICKETS_WORKFLOW_VERSION_SEQ";
create sequence if not exists "PA_TICKETS_WORKFLOW_STEP_SEQ";
create sequence if not exists "PA_TICKETS_WORKFLOW_TRANSITION_SEQ";
create sequence if not exists "PA_TICKETS_WORKFLOW_INSTANCE_SEQ";
create sequence if not exists "PA_TICKETS_WORKFLOW_STEP_INSTANCE_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_SEQ";
create sequence if not exists "PA_TICKETS_APPROVAL_INSTANCE_SEQ";
create sequence if not exists "PA_TICKETS_SLA_SEQ";
alter table "PA_TICKETS_WORKFLOW" alter column "N_WORKFLOW_ID" set default nextval('"PA_TICKETS_WORKFLOW_SEQ"');
alter table "PA_TICKETS_WORKFLOW_VERSION" alter column "N_WORKFLOW_VERSION_ID" set default nextval('"PA_TICKETS_WORKFLOW_VERSION_SEQ"');
alter table "PA_TICKETS_WORKFLOW_STEP" alter column "N_WORKFLOW_STEP_ID" set default nextval('"PA_TICKETS_WORKFLOW_STEP_SEQ"');
alter table "PA_TICKETS_WORKFLOW_TRANSITION" alter column "N_TRANSITION_ID" set default nextval('"PA_TICKETS_WORKFLOW_TRANSITION_SEQ"');
alter table "PA_TICKETS_WORKFLOW_INSTANCE" alter column "N_WORKFLOW_INSTANCE_ID" set default nextval('"PA_TICKETS_WORKFLOW_INSTANCE_SEQ"');
alter table "PA_TICKETS_WORKFLOW_STEP_INSTANCE" alter column "N_STEP_INSTANCE_ID" set default nextval('"PA_TICKETS_WORKFLOW_STEP_INSTANCE_SEQ"');
alter table "PA_TICKETS_APPROVAL" alter column "N_APPROVAL_ID" set default nextval('"PA_TICKETS_APPROVAL_SEQ"');
alter table "PA_TICKETS_SLA" alter column "N_SLA_ID" set default nextval('"PA_TICKETS_SLA_SEQ"');


