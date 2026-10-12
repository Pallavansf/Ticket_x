-- PA Tickets Phase 6 Slice 5 additive communication/notification administration schema.

create sequence if not exists "PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ";
create sequence if not exists "PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ";

alter table "PA_TICKETS_NOTIFICATION_RULE"
  add column if not exists "N_ROW_VERSION" integer not null default 0;

alter table "PA_TICKETS_NOTIFICATION_RULE_VERSION"
  add column if not exists "S_EVENT_TYPE" varchar(100),
  add column if not exists "N_PRIORITY" integer not null default 0,
  add column if not exists "S_STATUS" varchar(20) not null default 'PUBLISHED',
  add column if not exists "D_PUBLISHED_AT" timestamptz,
  add column if not exists "N_PUBLISHED_BY" bigint,
  add column if not exists "D_RETIRED_AT" timestamptz,
  add column if not exists "N_RETIRED_BY" bigint,
  add column if not exists "N_ROW_VERSION" integer not null default 0;

update "PA_TICKETS_NOTIFICATION_RULE_VERSION" v
   set "S_EVENT_TYPE" = r."S_EVENT_TYPE",
       "N_PRIORITY" = r."N_PRIORITY"
  from "PA_TICKETS_NOTIFICATION_RULE" r
 where v."N_RULE_ID" = r."N_NOTIFICATION_RULE_ID"
   and v."S_EVENT_TYPE" is null;

alter table "PA_TICKETS_COMMUNICATION_TEMPLATE"
  add column if not exists "D_CREATED_AT" timestamptz not null default now(),
  add column if not exists "N_CREATED_BY" bigint not null default 0,
  add column if not exists "D_PUBLISHED_AT" timestamptz,
  add column if not exists "N_PUBLISHED_BY" bigint,
  add column if not exists "D_RETIRED_AT" timestamptz,
  add column if not exists "N_RETIRED_BY" bigint,
  add column if not exists "N_ROW_VERSION" integer not null default 0;

do $$
begin
  if not exists (
      select 1 from pg_constraint
       where conname = 'CK_PA_TICKETS_COMM_TEMPLATE_STATUS'
         and conrelid = '"PA_TICKETS_COMMUNICATION_TEMPLATE"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION_TEMPLATE"
      add constraint "CK_PA_TICKETS_COMM_TEMPLATE_STATUS"
      check ("S_STATUS" in ('DRAFT','PUBLISHED','RETIRED','ACTIVE'));
  end if;
  if not exists (
      select 1 from pg_constraint
       where conname = 'CK_PA_TICKETS_NOTIFY_VERSION_STATUS'
         and conrelid = '"PA_TICKETS_NOTIFICATION_RULE_VERSION"'::regclass) then
    alter table "PA_TICKETS_NOTIFICATION_RULE_VERSION"
      add constraint "CK_PA_TICKETS_NOTIFY_VERSION_STATUS"
      check ("S_STATUS" in ('DRAFT','PUBLISHED','RETIRED'));
  end if;
end $$;

create table if not exists "PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG" (
  "N_PROVIDER_CONFIG_ID" bigint primary key default nextval('"PA_TICKETS_COMM_PROVIDER_CONFIG_SEQ"'),
  "S_PROVIDER_CODE" varchar(100) not null,
  "S_CHANNEL" varchar(30) not null,
  "N_ENABLED" boolean not null default true,
  "N_MAX_ATTEMPTS" integer not null default 3,
  "N_RETRY_DELAY_SECONDS" integer not null default 5,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "D_UPDATED_AT" timestamptz not null,
  "N_UPDATED_BY" bigint not null,
  "N_ROW_VERSION" integer not null default 0,
  constraint "CK_PA_TICKETS_COMM_PROVIDER_RETRY" check ("N_MAX_ATTEMPTS" between 1 and 20 and "N_RETRY_DELAY_SECONDS" between 0 and 86400),
  constraint "UX_PA_TICKETS_COMM_PROVIDER_CONFIG" unique ("S_PROVIDER_CODE","S_CHANNEL"),
  constraint "CK_PA_TICKETS_COMM_PROVIDER_CHANNEL" check ("S_CHANNEL" in ('EMAIL','WHATSAPP','PUSH','INAPP'))
);

create table if not exists "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG" (
  "N_CHANNEL_CONFIG_ID" bigint primary key default nextval('"PA_TICKETS_COMM_CHANNEL_CONFIG_SEQ"'),
  "S_CHANNEL" varchar(30) not null unique,
  "N_ENABLED" boolean not null default true,
  "D_EFFECTIVE_FROM" timestamptz not null,
  "D_EFFECTIVE_TO" timestamptz,
  "D_UPDATED_AT" timestamptz not null,
  "N_UPDATED_BY" bigint not null,
  "N_ROW_VERSION" integer not null default 0
);

do $$
begin
  if not exists (
      select 1 from pg_constraint
       where conname = 'CK_PA_TICKETS_COMM_CHANNEL_NAME'
         and conrelid = '"PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"
      add constraint "CK_PA_TICKETS_COMM_CHANNEL_NAME"
      check ("S_CHANNEL" in ('EMAIL','WHATSAPP','PUSH','INAPP'));
  end if;
end $$;

do $$
begin
  if not exists (
      select 1 from pg_constraint
       where conname = 'FK_PA_TICKETS_COMM_NOTIFY_EXEC'
         and conrelid = '"PA_TICKETS_COMMUNICATION"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION"
      add constraint "FK_PA_TICKETS_COMM_NOTIFY_EXEC"
      foreign key ("N_NOTIFICATION_EXECUTION_ID")
      references "PA_TICKETS_NOTIFICATION_EXECUTION"("N_NOTIFICATION_EXECUTION_ID");
  end if;
  if not exists (
      select 1 from pg_constraint
       where conname = 'FK_PA_TICKETS_COMM_NOTIFY_RULE'
         and conrelid = '"PA_TICKETS_COMMUNICATION"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION"
      add constraint "FK_PA_TICKETS_COMM_NOTIFY_RULE"
      foreign key ("N_NOTIFICATION_RULE_ID")
      references "PA_TICKETS_NOTIFICATION_RULE"("N_NOTIFICATION_RULE_ID");
  end if;
  if not exists (
      select 1 from pg_constraint
       where conname = 'FK_PA_TICKETS_COMM_NOTIFY_VERSION'
         and conrelid = '"PA_TICKETS_COMMUNICATION"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION"
      add constraint "FK_PA_TICKETS_COMM_NOTIFY_VERSION"
      foreign key ("N_NOTIFICATION_RULE_VERSION_ID")
      references "PA_TICKETS_NOTIFICATION_RULE_VERSION"("N_NOTIFICATION_RULE_VERSION_ID");
  end if;
  if not exists (
      select 1 from pg_constraint
       where conname = 'FK_PA_TICKETS_COMM_NOTIFY_EVENT'
         and conrelid = '"PA_TICKETS_COMMUNICATION"'::regclass) then
    alter table "PA_TICKETS_COMMUNICATION"
      add constraint "FK_PA_TICKETS_COMM_NOTIFY_EVENT"
      foreign key ("N_EVENT_ID")
      references "PA_TICKETS_EVENT"("N_EVENT_ID");
  end if;
end $$;

create unique index if not exists "UX_PA_TICKETS_COMM_TEMPLATE_VERSION"
  on "PA_TICKETS_COMMUNICATION_TEMPLATE"("S_TEMPLATE_CODE","S_CHANNEL","N_VERSION");
create unique index if not exists "UX_PA_TICKETS_NOTIFY_VERSION"
  on "PA_TICKETS_NOTIFICATION_RULE_VERSION"("N_RULE_ID","N_VERSION_NO");
create index if not exists "IX_PA_TICKETS_COMM_PROVIDER_ACTIVE"
  on "PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG"("S_PROVIDER_CODE","S_CHANNEL","N_ENABLED","D_EFFECTIVE_FROM");
create index if not exists "IX_PA_TICKETS_COMM_CHANNEL_ACTIVE"
  on "PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"("S_CHANNEL","N_ENABLED","D_EFFECTIVE_FROM");

insert into "PA_TICKETS_PERMISSION" ("N_PERMISSION_ID","S_PERMISSION_CODE","S_PERMISSION_NAME","N_ACTIVE")
select coalesce((select max("N_PERMISSION_ID") from "PA_TICKETS_PERMISSION"),0)
       + row_number() over (order by requested.code),
       requested.code, requested.name, true
  from (values
      ('COMMUNICATION_READ','Read communication configuration'),
      ('COMMUNICATION_ADMIN','Administer communication configuration'),
      ('COMMUNICATION_PUBLISH','Publish communication templates'),
      ('COMMUNICATION_RETIRE','Retire communication templates'),
      ('NOTIFICATION_READ','Read notification configuration'),
      ('NOTIFICATION_ADMIN','Administer notification configuration'),
      ('NOTIFICATION_PUBLISH','Publish notification rules'),
      ('NOTIFICATION_RETIRE','Retire notification rules')
  ) as requested(code,name)
 where not exists (
     select 1 from "PA_TICKETS_PERMISSION" p where p."S_PERMISSION_CODE" = requested.code
 );

insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_PERMISSION_ID","N_ROLE_ID","N_PERMISSION_ID")
select coalesce((select max("N_ROLE_PERMISSION_ID") from "PA_TICKETS_ROLE_PERMISSION"),0)
       + row_number() over (order by r."N_ROLE_ID", p."N_PERMISSION_ID"),
       r."N_ROLE_ID", p."N_PERMISSION_ID"
  from "PA_TICKETS_ROLE" r
 cross join "PA_TICKETS_PERMISSION" p
 where r."S_ROLE_CODE" = 'ADMINISTRATOR'
   and p."S_PERMISSION_CODE" in (
       'COMMUNICATION_READ','COMMUNICATION_ADMIN','COMMUNICATION_PUBLISH','COMMUNICATION_RETIRE',
       'NOTIFICATION_READ','NOTIFICATION_ADMIN','NOTIFICATION_PUBLISH','NOTIFICATION_RETIRE')
   and not exists (
       select 1 from "PA_TICKETS_ROLE_PERMISSION" rp
        where rp."N_ROLE_ID" = r."N_ROLE_ID" and rp."N_PERMISSION_ID" = p."N_PERMISSION_ID"
   );

insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_PERMISSION_ID","N_ROLE_ID","N_PERMISSION_ID")
select coalesce((select max("N_ROLE_PERMISSION_ID") from "PA_TICKETS_ROLE_PERMISSION"),0)
       + row_number() over (order by r."N_ROLE_ID", p."N_PERMISSION_ID"),
       r."N_ROLE_ID", p."N_PERMISSION_ID"
  from "PA_TICKETS_ROLE" r
 cross join "PA_TICKETS_PERMISSION" p
 where r."S_ROLE_CODE" = 'AUDITOR'
   and p."S_PERMISSION_CODE" in ('COMMUNICATION_READ','NOTIFICATION_READ')
   and not exists (
       select 1 from "PA_TICKETS_ROLE_PERMISSION" rp
        where rp."N_ROLE_ID" = r."N_ROLE_ID" and rp."N_PERMISSION_ID" = p."N_PERMISSION_ID"
   );
