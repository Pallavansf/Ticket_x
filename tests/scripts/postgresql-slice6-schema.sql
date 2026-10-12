-- PA TICKETS Slice 6 PostgreSQL adapter: status lifecycle and priority metadata.

create sequence if not exists "PA_TICKETS_STATUS_SEQ";
create sequence if not exists "PA_TICKETS_STATUS_TRANSITION_SEQ";
create sequence if not exists "PA_TICKETS_PRIORITY_SEQ";

create table if not exists "PA_TICKETS_STATUS" (
  "N_STATUS_ID" bigint primary key default nextval('"PA_TICKETS_STATUS_SEQ"'),
  "S_STATUS_CODE" varchar(50) not null unique,
  "S_STATUS_NAME" varchar(100) not null,
  "S_DESCRIPTION" varchar(1000),
  "N_IS_INITIAL" boolean not null default false,
  "N_IS_OPEN" boolean not null default true,
  "N_IS_CLOSED" boolean not null default false,
  "N_IS_CANCELLED" boolean not null default false,
  "N_DISPLAY_ORDER" integer not null default 0,
  "N_ACTIVE" boolean not null default true,
  "N_ROW_VERSION" integer not null default 0,
  "D_CREATED_AT" timestamptz not null default now(),
  "N_CREATED_BY" bigint not null,
  "D_UPDATED_AT" timestamptz,
  "N_UPDATED_BY" bigint,
  constraint "CK_PA_TICKETS_STATUS_FLAGS" check (
    "N_ROW_VERSION" >= 0
  )
);

create table if not exists "PA_TICKETS_STATUS_TRANSITION" (
  "N_STATUS_TRANSITION_ID" bigint primary key default nextval('"PA_TICKETS_STATUS_TRANSITION_SEQ"'),
  "S_FROM_STATUS_CODE" varchar(50) not null,
  "S_TO_STATUS_CODE" varchar(50) not null,
  "S_CONDITION" varchar(2000),
  "N_ALLOWED" boolean not null default true,
  "N_ROW_VERSION" integer not null default 0,
  "D_CREATED_AT" timestamptz not null default now(),
  "N_CREATED_BY" bigint not null,
  constraint "UK_PA_TICKETS_STATUS_TRANSITION" unique ("S_FROM_STATUS_CODE","S_TO_STATUS_CODE"),
  constraint "CK_PA_TICKETS_STATUS_TRANSITION_ROW_VERSION" check ("N_ROW_VERSION" >= 0),
  constraint "FK_PA_TICKETS_ST_FROM" foreign key ("S_FROM_STATUS_CODE") references "PA_TICKETS_STATUS"("S_STATUS_CODE"),
  constraint "FK_PA_TICKETS_ST_TO" foreign key ("S_TO_STATUS_CODE") references "PA_TICKETS_STATUS"("S_STATUS_CODE")
);

create table if not exists "PA_TICKETS_PRIORITY" (
  "N_PRIORITY_ID" bigint primary key default nextval('"PA_TICKETS_PRIORITY_SEQ"'),
  "S_PRIORITY_CODE" varchar(30) not null unique,
  "S_PRIORITY_NAME" varchar(100) not null,
  "S_DESCRIPTION" varchar(1000),
  "N_PRIORITY_LEVEL" integer not null,
  "N_DISPLAY_ORDER" integer not null default 0,
  "N_ACTIVE" boolean not null default true,
  "N_IS_DEFAULT" boolean not null default false,
  "N_ROW_VERSION" integer not null default 0,
  "D_CREATED_AT" timestamptz not null default now(),
  "N_CREATED_BY" bigint not null,
  "D_UPDATED_AT" timestamptz,
  "N_UPDATED_BY" bigint,
  constraint "CK_PA_TICKETS_PRIORITY_FLAGS" check ("N_ROW_VERSION" >= 0)
);

insert into "PA_TICKETS_STATUS"
  ("N_STATUS_ID","S_STATUS_CODE","S_STATUS_NAME","N_IS_INITIAL","N_IS_OPEN","N_IS_CLOSED","N_IS_CANCELLED","N_DISPLAY_ORDER","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
values
  (1,'NEW','New',true,true,false,false,1,true,timestamptz '-infinity',0),
  (2,'TRIAGE','Triage',false,true,false,false,2,true,timestamptz '-infinity',0),
  (3,'ASSIGNED','Assigned',false,true,false,false,3,true,timestamptz '-infinity',0),
  (4,'IN_PROGRESS','In Progress',false,true,false,false,4,true,timestamptz '-infinity',0),
  (5,'WAITING','Waiting',false,true,false,false,5,true,timestamptz '-infinity',0),
  (6,'PENDING_APPROVAL','Pending Approval',false,true,false,false,6,true,timestamptz '-infinity',0),
  (7,'RESOLVED','Resolved',false,true,false,false,7,true,timestamptz '-infinity',0),
  (8,'CLOSED','Closed',false,false,true,false,8,true,timestamptz '-infinity',0),
  (9,'CANCELLED','Cancelled',false,false,false,true,9,true,timestamptz '-infinity',0),
  (10,'REOPENED','Reopened',false,true,false,false,10,true,timestamptz '-infinity',0)
on conflict ("S_STATUS_CODE") do nothing;

insert into "PA_TICKETS_PRIORITY"
  ("N_PRIORITY_ID","S_PRIORITY_CODE","S_PRIORITY_NAME","S_DESCRIPTION","N_PRIORITY_LEVEL","N_DISPLAY_ORDER","N_ACTIVE","N_IS_DEFAULT","D_CREATED_AT","N_CREATED_BY")
values
  (1,'LOW','Low','Low business impact',1,1,true,false,timestamptz '-infinity',0),
  (2,'MEDIUM','Medium','Normal business priority',2,2,true,true,timestamptz '-infinity',0),
  (3,'HIGH','High','High business impact',3,3,true,false,timestamptz '-infinity',0),
  (4,'CRITICAL','Critical','Critical business impact requiring immediate action',4,4,true,false,timestamptz '-infinity',0)
on conflict ("S_PRIORITY_CODE") do nothing;

update "PA_TICKETS_PRIORITY"
   set "N_IS_DEFAULT" = false,
       "N_ROW_VERSION" = "N_ROW_VERSION" + 1,
       "D_UPDATED_AT" = now(),
       "N_UPDATED_BY" = 0
 where "N_IS_DEFAULT" and "S_PRIORITY_CODE" <> 'MEDIUM';
update "PA_TICKETS_PRIORITY"
   set "N_IS_DEFAULT" = true,
       "N_ROW_VERSION" = "N_ROW_VERSION" + 1,
       "D_UPDATED_AT" = now(),
       "N_UPDATED_BY" = 0
 where "S_PRIORITY_CODE" = 'MEDIUM'
   and not exists (select 1 from "PA_TICKETS_PRIORITY" where "N_IS_DEFAULT");

insert into "PA_TICKETS_STATUS_TRANSITION"
  ("S_FROM_STATUS_CODE","S_TO_STATUS_CODE","N_ALLOWED","D_CREATED_AT","N_CREATED_BY")
values
  ('NEW','TRIAGE',true,timestamptz '-infinity',0),
  ('NEW','ASSIGNED',true,timestamptz '-infinity',0),
  ('NEW','RESOLVED',true,timestamptz '-infinity',0),
  ('NEW','CLOSED',true,timestamptz '-infinity',0),
  ('NEW','CANCELLED',true,timestamptz '-infinity',0),
  ('TRIAGE','ASSIGNED',true,timestamptz '-infinity',0),
  ('TRIAGE','CANCELLED',true,timestamptz '-infinity',0),
  ('ASSIGNED','IN_PROGRESS',true,timestamptz '-infinity',0),
  ('ASSIGNED','PENDING_APPROVAL',true,timestamptz '-infinity',0),
  ('ASSIGNED','CANCELLED',true,timestamptz '-infinity',0),
  ('IN_PROGRESS','ASSIGNED',true,timestamptz '-infinity',0),
  ('PENDING_APPROVAL','ASSIGNED',true,timestamptz '-infinity',0),
  ('PENDING_APPROVAL','CANCELLED',true,timestamptz '-infinity',0),
  ('IN_PROGRESS','WAITING',true,timestamptz '-infinity',0),
  ('IN_PROGRESS','RESOLVED',true,timestamptz '-infinity',0),
  ('IN_PROGRESS','CANCELLED',true,timestamptz '-infinity',0),
  ('WAITING','IN_PROGRESS',true,timestamptz '-infinity',0),
  ('WAITING','RESOLVED',true,timestamptz '-infinity',0),
  ('WAITING','CANCELLED',true,timestamptz '-infinity',0),
  ('RESOLVED','CLOSED',true,timestamptz '-infinity',0),
  ('RESOLVED','REOPENED',true,timestamptz '-infinity',0),
  ('CLOSED','REOPENED',true,timestamptz '-infinity',0),
  ('REOPENED','TRIAGE',true,timestamptz '-infinity',0),
  ('REOPENED','ASSIGNED',true,timestamptz '-infinity',0),
  ('REOPENED','RESOLVED',true,timestamptz '-infinity',0),
  ('REOPENED','CLOSED',true,timestamptz '-infinity',0),
  ('REOPENED','CANCELLED',true,timestamptz '-infinity',0)
on conflict ("S_FROM_STATUS_CODE","S_TO_STATUS_CODE") do nothing;

select setval('"PA_TICKETS_STATUS_SEQ"', greatest(
  coalesce((select max("N_STATUS_ID") from "PA_TICKETS_STATUS"),1),
  (select last_value from "PA_TICKETS_STATUS_SEQ"),
  coalesce((select max("S_ENTITY_ID"::bigint) from "PA_TICKETS_AUDIT" where "S_ENTITY_TYPE" = 'STATUS_DEFINITION' and "S_ENTITY_ID" ~ '^[0-9]+$'),1)
), true);
select setval('"PA_TICKETS_STATUS_TRANSITION_SEQ"', greatest(coalesce((select max("N_STATUS_TRANSITION_ID") from "PA_TICKETS_STATUS_TRANSITION"),1), (select last_value from "PA_TICKETS_STATUS_TRANSITION_SEQ")), true);
select setval('"PA_TICKETS_PRIORITY_SEQ"', greatest(
  coalesce((select max("N_PRIORITY_ID") from "PA_TICKETS_PRIORITY"),1),
  (select last_value from "PA_TICKETS_PRIORITY_SEQ"),
  coalesce((select max("S_ENTITY_ID"::bigint) from "PA_TICKETS_AUDIT" where "S_ENTITY_TYPE" = 'PRIORITY_DEFINITION' and "S_ENTITY_ID" ~ '^[0-9]+$'),1)
), true);

create index if not exists "IX_PA_TICKETS_STATUS_ACTIVE_ORDER"
  on "PA_TICKETS_STATUS"("N_ACTIVE","N_DISPLAY_ORDER");
create index if not exists "IX_PA_TICKETS_TRANSITION_TO"
  on "PA_TICKETS_STATUS_TRANSITION"("S_TO_STATUS_CODE","N_ALLOWED");
create index if not exists "IX_PA_TICKETS_PRIORITY_ACTIVE_ORDER"
  on "PA_TICKETS_PRIORITY"("N_ACTIVE","N_DISPLAY_ORDER");
create unique index if not exists "UX_PA_TICKETS_PRIORITY_DEFAULT"
  on "PA_TICKETS_PRIORITY"("N_IS_DEFAULT") where "N_IS_DEFAULT";

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_STATUS' and conrelid = '"PA_TICKETS"'::regclass) then
    alter table "PA_TICKETS" add constraint "FK_PA_TICKETS_STATUS"
      foreign key ("N_STATUS_ID") references "PA_TICKETS_STATUS"("N_STATUS_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_PRIORITY' and conrelid = '"PA_TICKETS"'::regclass) then
    alter table "PA_TICKETS" add constraint "FK_PA_TICKETS_PRIORITY"
      foreign key ("N_PRIORITY_ID") references "PA_TICKETS_PRIORITY"("N_PRIORITY_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_WORK_ITEM_PRIORITY' and conrelid = '"PA_TICKETS_WORK_ITEM"'::regclass) then
    alter table "PA_TICKETS_WORK_ITEM" add constraint "FK_PA_TICKETS_WORK_ITEM_PRIORITY"
      foreign key ("N_PRIORITY_ID") references "PA_TICKETS_PRIORITY"("N_PRIORITY_ID");
  end if;
end $$;

insert into "PA_TICKETS_PERMISSION" ("N_PERMISSION_ID","S_PERMISSION_CODE","S_PERMISSION_NAME","N_ACTIVE")
select coalesce((select max("N_PERMISSION_ID") from "PA_TICKETS_PERMISSION"),0)+1,
       'TICKET_ADMIN','Administer ticket status and priority configuration',true
where not exists (select 1 from "PA_TICKETS_PERMISSION" where "S_PERMISSION_CODE" = 'TICKET_ADMIN');
select setval(
  '"PA_TICKETS_ROLE_PERMISSION_SEQ"',
  greatest(
    coalesce((select max("N_ROLE_PERMISSION_ID") from "PA_TICKETS_ROLE_PERMISSION"),1),
    (select last_value from "PA_TICKETS_ROLE_PERMISSION_SEQ")
  ),
  true
);
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select r."N_ROLE_ID", p."N_PERMISSION_ID"
  from "PA_TICKETS_ROLE" r cross join "PA_TICKETS_PERMISSION" p
 where r."S_ROLE_CODE" = 'ADMINISTRATOR' and p."S_PERMISSION_CODE" = 'TICKET_ADMIN'
on conflict do nothing;
