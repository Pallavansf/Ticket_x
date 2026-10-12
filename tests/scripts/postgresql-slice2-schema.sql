create sequence if not exists "PA_TICKETS_SERVICE_SEQ";
create sequence if not exists "PA_TICKETS_CATEGORY_SEQ";
create sequence if not exists "PA_TICKETS_SUB_CATEGORY_SEQ";
create sequence if not exists "PA_TICKETS_REQUEST_TYPE_SEQ";
create sequence if not exists "PA_TICKETS_FIELD_SEQ";
create sequence if not exists "PA_TICKETS_FIELD_OPTION_SEQ";
create sequence if not exists "PA_TICKETS_REQUEST_FIELD_SEQ";
create sequence if not exists "PA_TICKETS_VALUE_SEQ";

alter table "PA_TICKETS" add column if not exists "N_REQUEST_TYPE_VERSION" integer;

create table if not exists "PA_TICKETS_SERVICE" (
  "N_SERVICE_ID" bigint primary key default nextval('"PA_TICKETS_SERVICE_SEQ"'),
  "S_SERVICE_CODE" varchar(50) not null unique,
  "S_SERVICE_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_OWNER_DEPARTMENT_ID" bigint,
  "N_OWNER_TEAM_ID" bigint,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz
);
create table if not exists "PA_TICKETS_CATEGORY" (
  "N_CATEGORY_ID" bigint primary key default nextval('"PA_TICKETS_CATEGORY_SEQ"'),
  "N_SERVICE_ID" bigint not null references "PA_TICKETS_SERVICE"("N_SERVICE_ID"),
  "S_CATEGORY_CODE" varchar(50) not null,
  "S_CATEGORY_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  unique ("N_SERVICE_ID", "S_CATEGORY_CODE")
);
create unique index if not exists "UK_PA_TICKETS_CAT_SVC_ID" on "PA_TICKETS_CATEGORY"("N_SERVICE_ID", "N_CATEGORY_ID");
create table if not exists "PA_TICKETS_SUB_CATEGORY" (
  "N_SUB_CATEGORY_ID" bigint primary key default nextval('"PA_TICKETS_SUB_CATEGORY_SEQ"'),
  "N_CATEGORY_ID" bigint not null references "PA_TICKETS_CATEGORY"("N_CATEGORY_ID"),
  "S_SUB_CATEGORY_CODE" varchar(50) not null,
  "S_SUB_CATEGORY_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  check ("D_EFFECTIVE_TO" is null or "D_EFFECTIVE_TO" > "D_EFFECTIVE_FROM")
);
create unique index if not exists "UK_PA_TICKETS_SUBCAT_PARENT_ID" on "PA_TICKETS_SUB_CATEGORY"("N_CATEGORY_ID", "N_SUB_CATEGORY_ID");
create unique index if not exists "UK_PA_TICKETS_SUBCAT_CODE" on "PA_TICKETS_SUB_CATEGORY"("N_CATEGORY_ID", "S_SUB_CATEGORY_CODE");
create unique index if not exists "UK_PA_TICKETS_SUBCAT_NAME" on "PA_TICKETS_SUB_CATEGORY"("N_CATEGORY_ID", "S_SUB_CATEGORY_NAME");
create table if not exists "PA_TICKETS_REQUEST_TYPE" (
  "N_REQUEST_TYPE_ID" bigint primary key default nextval('"PA_TICKETS_REQUEST_TYPE_SEQ"'),
  "N_SERVICE_ID" bigint not null references "PA_TICKETS_SERVICE"("N_SERVICE_ID"),
  "N_CATEGORY_ID" bigint not null references "PA_TICKETS_CATEGORY"("N_CATEGORY_ID"),
  "N_TICKET_TYPE_ID" integer not null,
  "S_REQUEST_TYPE_CODE" varchar(80) not null,
  "S_REQUEST_TYPE_NAME" varchar(300) not null,
  "S_DESCRIPTION" text,
  "N_DEFAULT_PRIORITY_ID" bigint,
  "N_DEFAULT_DEPARTMENT_ID" bigint,
  "N_DEFAULT_TEAM_ID" bigint,
  "N_WORKFLOW_ID" bigint,
  "N_WORKFLOW_VERSION_ID" bigint,
  "N_APPROVAL_RULE_VERSION_ID" bigint,
  "N_SLA_POLICY_ID" bigint,
  "N_SLA_POLICY_VERSION" integer,
  "N_EVIDENCE_REQUIRED" boolean not null default false,
  "N_EVIDENCE_MIN_COUNT" integer not null default 0,
  "N_VERSION_NO" integer not null default 1,
  "S_CONFIGURATION_STATUS" varchar(20) not null default 'DRAFT',
  "N_ACTIVE" boolean not null default false,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "D_PUBLISHED_AT" timestamptz,
  unique ("N_SERVICE_ID", "S_REQUEST_TYPE_CODE", "N_VERSION_NO")
);
alter table "PA_TICKETS_REQUEST_TYPE" add column if not exists "N_SUB_CATEGORY_ID" bigint;
alter table "PA_TICKETS_REQUEST_TYPE" add column if not exists "N_WORKFLOW_VERSION_ID" bigint;
alter table "PA_TICKETS_REQUEST_TYPE" add column if not exists "N_APPROVAL_RULE_VERSION_ID" bigint;
alter table "PA_TICKETS" add column if not exists "N_SUB_CATEGORY_ID" bigint;
alter table "PA_TICKETS" drop constraint if exists "FK_PA_TICKETS_TICKET_RT_BASE";
drop index if exists "UK_PA_TICKETS_RT_BASE_PATH";
create unique index if not exists "UK_PA_TICKETS_RT_SC_PATH" on "PA_TICKETS_REQUEST_TYPE"("N_REQUEST_TYPE_ID", "N_CATEGORY_ID", "N_SUB_CATEGORY_ID");
do $$ begin
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_RT_SVC_CAT') then
    alter table "PA_TICKETS_REQUEST_TYPE" add constraint "FK_PA_TICKETS_RT_SVC_CAT" foreign key ("N_SERVICE_ID", "N_CATEGORY_ID") references "PA_TICKETS_CATEGORY"("N_SERVICE_ID", "N_CATEGORY_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_RT_SUBCAT') then
    alter table "PA_TICKETS_REQUEST_TYPE" add constraint "FK_PA_TICKETS_RT_SUBCAT" foreign key ("N_CATEGORY_ID", "N_SUB_CATEGORY_ID") references "PA_TICKETS_SUB_CATEGORY"("N_CATEGORY_ID", "N_SUB_CATEGORY_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_RT_WFVER') then
    alter table "PA_TICKETS_REQUEST_TYPE" add constraint "FK_PA_TICKETS_RT_WFVER" foreign key ("N_WORKFLOW_VERSION_ID", "N_WORKFLOW_ID") references "PA_TICKETS_WORKFLOW_VERSION"("N_WORKFLOW_VERSION_ID", "N_WORKFLOW_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_RT_APVER') then
    alter table "PA_TICKETS_REQUEST_TYPE" add constraint "FK_PA_TICKETS_RT_APVER" foreign key ("N_APPROVAL_RULE_VERSION_ID") references "PA_TICKETS_APPROVAL_RULE_VERSION"("N_APPROVAL_RULE_VERSION_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_TICKET_SUBCAT') then
    alter table "PA_TICKETS" add constraint "FK_PA_TICKETS_TICKET_SUBCAT" foreign key ("N_CATEGORY_ID", "N_SUB_CATEGORY_ID") references "PA_TICKETS_SUB_CATEGORY"("N_CATEGORY_ID", "N_SUB_CATEGORY_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'CK_PA_TICKETS_SC_REQUIRES_RT') then
    alter table "PA_TICKETS" add constraint "CK_PA_TICKETS_SC_REQUIRES_RT" check ("N_SUB_CATEGORY_ID" is null or "N_REQUEST_TYPE_ID" is not null);
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_TICKET_RT_SC') then
    alter table "PA_TICKETS" add constraint "FK_PA_TICKETS_TICKET_RT_SC" foreign key ("N_REQUEST_TYPE_ID", "N_CATEGORY_ID", "N_SUB_CATEGORY_ID") references "PA_TICKETS_REQUEST_TYPE"("N_REQUEST_TYPE_ID", "N_CATEGORY_ID", "N_SUB_CATEGORY_ID");
  end if;
end $$;
create table if not exists "PA_TICKETS_FIELD" (
  "N_FIELD_ID" bigint primary key default nextval('"PA_TICKETS_FIELD_SEQ"'),
  "S_FIELD_CODE" varchar(100) not null unique,
  "S_FIELD_NAME" varchar(300) not null,
  "S_DATA_TYPE" varchar(30) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz
);
create table if not exists "PA_TICKETS_FIELD_OPTION" (
  "N_FIELD_OPTION_ID" bigint primary key default nextval('"PA_TICKETS_FIELD_OPTION_SEQ"'),
  "N_FIELD_ID" bigint not null references "PA_TICKETS_FIELD"("N_FIELD_ID"),
  "S_OPTION_CODE" varchar(100) not null,
  "S_OPTION_NAME" varchar(300) not null,
  "N_DISPLAY_ORDER" integer not null default 0,
  "N_ACTIVE" boolean not null default true,
  unique ("N_FIELD_ID", "S_OPTION_CODE")
);
create table if not exists "PA_TICKETS_REQUEST_FIELD" (
  "N_REQUEST_FIELD_ID" bigint primary key default nextval('"PA_TICKETS_REQUEST_FIELD_SEQ"'),
  "N_REQUEST_TYPE_ID" bigint not null references "PA_TICKETS_REQUEST_TYPE"("N_REQUEST_TYPE_ID"),
  "N_FIELD_ID" bigint not null references "PA_TICKETS_FIELD"("N_FIELD_ID"),
  "N_REQUIRED" boolean not null default false,
  "N_DISPLAY_ORDER" integer not null default 0,
  "N_ACTIVE" boolean not null default true,
  unique ("N_REQUEST_TYPE_ID", "N_FIELD_ID")
);
create table if not exists "PA_TICKETS_VALUE" (
  "N_TICKET_VALUE_ID" bigint primary key default nextval('"PA_TICKETS_VALUE_SEQ"'),
  "N_TICKET_ID" bigint not null references "PA_TICKETS"("N_TICKET_ID"),
  "N_FIELD_ID" bigint not null references "PA_TICKETS_FIELD"("N_FIELD_ID"),
  "S_VALUE_STRING" varchar(4000),
  "N_VALUE_NUMBER" numeric,
  "D_VALUE_DATE" timestamptz,
  "S_VALUE_TEXT" text,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  unique ("N_TICKET_ID", "N_FIELD_ID")
);

insert into "PA_TICKETS_PERMISSION" ("N_PERMISSION_ID","S_PERMISSION_CODE","S_PERMISSION_NAME","N_ACTIVE") values
 (9,'SERVICE_READ','Read services',true),(10,'SERVICE_ADMIN','Administer services',true),
 (11,'CATEGORY_READ','Read categories',true),(12,'CATEGORY_ADMIN','Administer categories',true),
 (13,'REQUEST_TYPE_READ','Read request types',true),(14,'REQUEST_TYPE_ADMIN','Administer request types',true),
 (15,'CATALOGUE_PUBLISH','Publish catalogue configuration',true),
 (16,'WORKFLOW_READ','Read workflow configuration',true),(17,'WORKFLOW_ADMIN','Administer workflow configuration',true),(18,'WORKFLOW_PUBLISH','Publish workflow configuration',true),
 (19,'APPROVAL_READ','Read approval configuration',true),(20,'APPROVAL_ADMIN','Administer approval configuration',true),(21,'APPROVAL_PUBLISH','Publish approval configuration',true)
on conflict ("N_PERMISSION_ID") do nothing;
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select 2,"N_PERMISSION_ID" from "PA_TICKETS_PERMISSION" where "S_PERMISSION_CODE" in ('SERVICE_READ','SERVICE_ADMIN','CATEGORY_READ','CATEGORY_ADMIN','REQUEST_TYPE_READ','REQUEST_TYPE_ADMIN','CATALOGUE_PUBLISH','WORKFLOW_READ','WORKFLOW_ADMIN','WORKFLOW_PUBLISH','APPROVAL_READ','APPROVAL_ADMIN','APPROVAL_PUBLISH')
on conflict do nothing;
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select 3,"N_PERMISSION_ID" from "PA_TICKETS_PERMISSION" where "S_PERMISSION_CODE" in ('WORKFLOW_READ','APPROVAL_READ')
on conflict do nothing;

alter table "PA_TICKETS_IDEMPOTENCY" add column if not exists "N_ACTOR_USER_ID" bigint;
alter table "PA_TICKETS_IDEMPOTENCY" add column if not exists "N_RESULT_TICKET_ID" bigint;
drop index if exists "UX_PA_TICKETS_IDEMPOTENCY_SCOPE_KEY";
create unique index if not exists "UX_PA_TICKETS_IDEMPOTENCY_SCOPE_ACTOR_KEY" on "PA_TICKETS_IDEMPOTENCY"("S_SCOPE","N_ACTOR_USER_ID","S_IDEMPOTENCY_KEY") where "N_ACTOR_USER_ID" is not null;
create unique index if not exists "UX_PA_TICKETS_IDEMPOTENCY_SCOPE_KEY" on "PA_TICKETS_IDEMPOTENCY"("S_SCOPE","S_IDEMPOTENCY_KEY") where "N_ACTOR_USER_ID" is null;
