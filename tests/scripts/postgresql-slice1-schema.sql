create sequence if not exists "PA_TICKETS_USER_SEQ";
create sequence if not exists "PA_TICKETS_DEPARTMENT_SEQ";
create sequence if not exists "PA_TICKETS_TEAM_SEQ";
create sequence if not exists "PA_TICKETS_ROLE_SEQ";
create sequence if not exists "PA_TICKETS_PERMISSION_SEQ";
create sequence if not exists "PA_TICKETS_USER_ROLE_SEQ";
create sequence if not exists "PA_TICKETS_ROLE_PERMISSION_SEQ";
create sequence if not exists "PA_TICKETS_USER_CLIENT_SEQ";

create table if not exists "PA_TICKETS_DEPARTMENT" (
  "N_DEPARTMENT_ID" bigint primary key default nextval('"PA_TICKETS_DEPARTMENT_SEQ"'),
  "S_DEPARTMENT_CODE" varchar(50) not null unique,
  "S_DEPARTMENT_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null
);
create table if not exists "PA_TICKETS_TEAM" (
  "N_TEAM_ID" bigint primary key default nextval('"PA_TICKETS_TEAM_SEQ"'),
  "N_DEPARTMENT_ID" bigint not null references "PA_TICKETS_DEPARTMENT"("N_DEPARTMENT_ID"),
  "S_TEAM_CODE" varchar(50) not null,
  "S_TEAM_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  unique ("N_DEPARTMENT_ID", "S_TEAM_CODE")
);
create table if not exists "PA_TICKETS_USER" (
  "N_USER_ID" bigint primary key,
  "S_USER_CODE" varchar(100) not null unique,
  "S_LOGIN_NAME" varchar(200) not null unique,
  "S_DISPLAY_NAME" varchar(300) not null,
  "S_EMAIL" varchar(320),
  "N_DEPARTMENT_ID" bigint references "PA_TICKETS_DEPARTMENT"("N_DEPARTMENT_ID"),
  "N_TEAM_ID" bigint references "PA_TICKETS_TEAM"("N_TEAM_ID"),
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null,
  "D_UPDATED_AT" timestamptz,
  "N_UPDATED_BY" bigint
);
create table if not exists "PA_TICKETS_ROLE" (
  "N_ROLE_ID" bigint primary key default nextval('"PA_TICKETS_ROLE_SEQ"'),
  "S_ROLE_CODE" varchar(100) not null unique,
  "S_ROLE_NAME" varchar(200) not null,
  "S_DESCRIPTION" text,
  "N_SYSTEM_ROLE" boolean not null default false,
  "N_ACTIVE" boolean not null default true
);
create table if not exists "PA_TICKETS_PERMISSION" (
  "N_PERMISSION_ID" bigint primary key default nextval('"PA_TICKETS_PERMISSION_SEQ"'),
  "S_PERMISSION_CODE" varchar(150) not null unique,
  "S_PERMISSION_NAME" varchar(250) not null,
  "S_DESCRIPTION" text,
  "N_ACTIVE" boolean not null default true
);
create table if not exists "PA_TICKETS_USER_ROLE" (
  "N_USER_ROLE_ID" bigint primary key default nextval('"PA_TICKETS_USER_ROLE_SEQ"'),
  "N_USER_ID" bigint not null references "PA_TICKETS_USER"("N_USER_ID"),
  "N_ROLE_ID" bigint not null references "PA_TICKETS_ROLE"("N_ROLE_ID"),
  "D_VALID_FROM" timestamptz not null,
  "D_VALID_TO" timestamptz,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null
);
create table if not exists "PA_TICKETS_ROLE_PERMISSION" (
  "N_ROLE_PERMISSION_ID" bigint primary key default nextval('"PA_TICKETS_ROLE_PERMISSION_SEQ"'),
  "N_ROLE_ID" bigint not null references "PA_TICKETS_ROLE"("N_ROLE_ID"),
  "N_PERMISSION_ID" bigint not null references "PA_TICKETS_PERMISSION"("N_PERMISSION_ID"),
  unique ("N_ROLE_ID", "N_PERMISSION_ID")
);
create table if not exists "PA_TICKETS_USER_CLIENT" (
  "N_USER_CLIENT_ID" bigint primary key default nextval('"PA_TICKETS_USER_CLIENT_SEQ"'),
  "N_USER_ID" bigint not null references "PA_TICKETS_USER"("N_USER_ID"),
  "N_CLIENT_ID" bigint not null,
  "D_VALID_FROM" timestamptz not null,
  "D_VALID_TO" timestamptz,
  "N_ACTIVE" boolean not null default true,
  "D_CREATED_AT" timestamptz not null,
  "N_CREATED_BY" bigint not null
);
alter table "PA_TICKETS_USER_ROLE" drop constraint if exists "PA_TICKETS_USER_ROLE_N_USER_ID_N_ROLE_ID_key";
alter table "PA_TICKETS_USER_CLIENT" drop constraint if exists "PA_TICKETS_USER_CLIENT_N_USER_ID_N_CLIENT_ID_key";
create index if not exists "IX_PA_TICKETS_USER_ROLE_EFFECTIVE" on "PA_TICKETS_USER_ROLE" ("N_USER_ID","N_ROLE_ID","D_VALID_FROM","D_VALID_TO") where "N_ACTIVE";
create index if not exists "IX_PA_TICKETS_USER_CLIENT_EFFECTIVE" on "PA_TICKETS_USER_CLIENT" ("N_USER_ID","N_CLIENT_ID","D_VALID_FROM","D_VALID_TO") where "N_ACTIVE";

insert into "PA_TICKETS_DEPARTMENT" ("N_DEPARTMENT_ID","S_DEPARTMENT_CODE","S_DEPARTMENT_NAME","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
values (501,'TEST-OPS','Test Operations',true,clock_timestamp(),0),
       (502,'TEST-DEPT-502','Test Department 502',true,clock_timestamp(),0),
       (503,'TEST-DEPT-503','Test Department 503',true,clock_timestamp(),0),
       (504,'TEST-DEPT-504','Test Department 504',true,clock_timestamp(),0)
on conflict ("N_DEPARTMENT_ID") do update set "N_ACTIVE" = true;
insert into "PA_TICKETS_TEAM" ("N_TEAM_ID","N_DEPARTMENT_ID","S_TEAM_CODE","S_TEAM_NAME","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
values (601,501,'TEST-OPS-TEAM','Test Operations Team',true,clock_timestamp(),0),
       (602,502,'TEST-TEAM-602','Test Team 602',true,clock_timestamp(),0),
       (603,503,'TEST-TEAM-603','Test Team 603',true,clock_timestamp(),0),
       (604,504,'TEST-TEAM-604','Test Team 604',true,clock_timestamp(),0)
on conflict ("N_TEAM_ID") do update
set "N_ACTIVE" = true,
    "N_DEPARTMENT_ID" = excluded."N_DEPARTMENT_ID";

insert into "PA_TICKETS_ROLE" ("N_ROLE_ID","S_ROLE_CODE","S_ROLE_NAME","N_SYSTEM_ROLE","N_ACTIVE")
values (1,'OPERATIONS','Operations',true,true),(2,'ADMINISTRATOR','Administrator',true,true),(3,'AUDITOR','Auditor',true,true)
on conflict ("N_ROLE_ID") do nothing;
insert into "PA_TICKETS_PERMISSION" ("N_PERMISSION_ID","S_PERMISSION_CODE","S_PERMISSION_NAME","N_ACTIVE")
values
 (1,'TICKET_READ','Read tickets',true),(2,'TICKET_UPDATE','Update tickets',true),(3,'USER_ADMIN','Administer users',true),
 (4,'ROLE_ADMIN','Administer roles',true),(5,'CLIENT_ADMIN','Administer client access',true),(6,'AUDITOR_VIEW','View audit data',true),
 (7,'AUDIT_EXPORT','Export audit data',true),(8,'CONFIG_ADMIN','Administer configuration',true)
on conflict ("N_PERMISSION_ID") do nothing;
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select 1,"N_PERMISSION_ID" from "PA_TICKETS_PERMISSION" where "S_PERMISSION_CODE" in ('TICKET_READ','TICKET_UPDATE')
on conflict do nothing;
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select 2,"N_PERMISSION_ID" from "PA_TICKETS_PERMISSION"
on conflict do nothing;
insert into "PA_TICKETS_ROLE_PERMISSION" ("N_ROLE_ID","N_PERMISSION_ID")
select 3,"N_PERMISSION_ID" from "PA_TICKETS_PERMISSION" where "S_PERMISSION_CODE" in ('TICKET_READ','AUDITOR_VIEW','AUDIT_EXPORT')
on conflict do nothing;

insert into "PA_TICKETS_USER" ("N_USER_ID","S_USER_CODE","S_LOGIN_NAME","S_DISPLAY_NAME","N_DEPARTMENT_ID","N_TEAM_ID","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
select id, 'TEST-' || id, 'test-' || id, 'Test User ' || id,
       case id when 7200 then 502 when 7300 then 503 when 7400 then 504 else 501 end,
       case id when 7200 then 602 when 7300 then 603 when 7400 then 604 else 601 end,
       true, clock_timestamp(), 0
from unnest(array[1001,1002,1003,1004,2001,2002,3001,4001,5001,7100,7150,7200,7300,7400,8100,8150,8200,8300,8400,9001,9999]::bigint[]) as ids(id)
on conflict ("N_USER_ID") do update
set "N_DEPARTMENT_ID" = excluded."N_DEPARTMENT_ID",
    "N_TEAM_ID" = excluded."N_TEAM_ID",
    "N_ACTIVE" = true;
insert into "PA_TICKETS_USER_ROLE" ("N_USER_ID","N_ROLE_ID","D_VALID_FROM","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
select "N_USER_ID", case when "N_USER_ID" = 9001 then 2 else 1 end, timestamptz '-infinity', true, clock_timestamp(), 0
from "PA_TICKETS_USER" u where u."N_USER_ID" in (1001,1002,1003,1004,2001,2002,3001,4001,5001,7100,7150,7200,7300,7400,8100,8150,8200,8300,8400,9001)
and not exists (select 1 from "PA_TICKETS_USER_ROLE" m where m."N_USER_ID"=u."N_USER_ID" and m."N_ROLE_ID"=case when u."N_USER_ID" = 9001 then 2 else 1 end and m."N_ACTIVE" and m."D_VALID_FROM"=timestamptz '-infinity' and m."D_VALID_TO" is null);
insert into "PA_TICKETS_USER_ROLE" ("N_USER_ID","N_ROLE_ID","D_VALID_FROM","N_ACTIVE","D_CREATED_AT","N_CREATED_BY")
select 9999,3,timestamptz '-infinity',true,clock_timestamp(),0
where exists (select 1 from "PA_TICKETS_USER" where "N_USER_ID"=9999)
and not exists (select 1 from "PA_TICKETS_USER_ROLE" where "N_USER_ID"=9999 and "N_ROLE_ID"=3 and "N_ACTIVE" and "D_VALID_FROM"=timestamptz '-infinity' and "D_VALID_TO" is null);

do $$ begin
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_APRS_ROLE') then
    alter table "PA_TICKETS_APPROVAL_RULE_STEP" add constraint "FK_PA_TICKETS_APRS_ROLE" foreign key ("N_ROLE_ID") references "PA_TICKETS_ROLE"("N_ROLE_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_APRS_DEPT') then
    alter table "PA_TICKETS_APPROVAL_RULE_STEP" add constraint "FK_PA_TICKETS_APRS_DEPT" foreign key ("N_DEPARTMENT_ID") references "PA_TICKETS_DEPARTMENT"("N_DEPARTMENT_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_APRS_TEAM') then
    alter table "PA_TICKETS_APPROVAL_RULE_STEP" add constraint "FK_PA_TICKETS_APRS_TEAM" foreign key ("N_TEAM_ID") references "PA_TICKETS_TEAM"("N_TEAM_ID");
  end if;
  if not exists (select 1 from pg_constraint where conname = 'FK_PA_TICKETS_APRS_USER') then
    alter table "PA_TICKETS_APPROVAL_RULE_STEP" add constraint "FK_PA_TICKETS_APRS_USER" foreign key ("N_USER_ID") references "PA_TICKETS_USER"("N_USER_ID");
  end if;
end $$;
