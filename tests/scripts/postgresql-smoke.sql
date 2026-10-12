begin;
insert into "PA_TICKETS" ("S_TICKET_NO","N_TICKET_TYPE_ID","N_REQUESTED_BY","S_SUBJECT","N_STATUS_ID","D_CREATED_AT","N_CREATED_BY") values ('PG-SMOKE-' || to_char(clock_timestamp(),'YYYYMMDDHH24MISSMS'),1,900001,'PostgreSQL persistence smoke',1,clock_timestamp(),900001) returning "N_TICKET_ID";
commit;
begin;
insert into "PA_TICKETS" ("S_TICKET_NO","N_TICKET_TYPE_ID","N_REQUESTED_BY","S_SUBJECT","N_STATUS_ID","D_CREATED_AT","N_CREATED_BY") values ('PG-ROLLBACK-' || to_char(clock_timestamp(),'YYYYMMDDHH24MISSMS'),1,900002,'PostgreSQL rollback smoke',1,clock_timestamp(),900002);
rollback;
select 'POSTGRESQL PERSISTENCE SMOKE: PASS' where exists (select 1 from "PA_TICKETS" where "S_SUBJECT"='PostgreSQL persistence smoke');
select 'POSTGRESQL TRANSACTION SMOKE: PASS' where not exists (select 1 from "PA_TICKETS" where "S_SUBJECT"='PostgreSQL rollback smoke');
do $$
begin
  if to_regclass('"PA_TICKETS_COMMUNICATION_PROVIDER_CONFIG"') is null
     or to_regclass('"PA_TICKETS_COMMUNICATION_CHANNEL_CONFIG"') is null
     or to_regclass('"PA_TICKETS_COMMUNICATION_TEMPLATE"') is null
     or to_regclass('"PA_TICKETS_NOTIFICATION_RULE_VERSION"') is null then
    raise exception 'Phase 6 Slice 5 administration schema is incomplete.';
  end if;
  if to_regclass('"PA_TICKETS_STATUS"') is null
     or to_regclass('"PA_TICKETS_STATUS_TRANSITION"') is null
     or to_regclass('"PA_TICKETS_PRIORITY"') is null then
    raise exception 'Phase 6 Slice 6 operational metadata schema is incomplete.';
  end if;
  if (
      select count(*) from pg_constraint
       where conname in ('FK_PA_TICKETS_ST_FROM','FK_PA_TICKETS_ST_TO','FK_PA_TICKETS_STATUS',
                         'FK_PA_TICKETS_PRIORITY','FK_PA_TICKETS_WORK_ITEM_PRIORITY')
         and contype = 'f')
     <> 5 then
    raise exception 'Phase 6 Slice 6 status/priority foreign keys are incomplete.';
  end if;
  if (
      select count(*) from "PA_TICKETS_STATUS_TRANSITION"
       where "S_FROM_STATUS_CODE" = 'NEW' and "S_TO_STATUS_CODE" = 'TRIAGE' and "N_ALLOWED")
     <> 1
     or not exists (select 1 from "PA_TICKETS_PRIORITY" where "S_PRIORITY_CODE" = 'MEDIUM' and "N_IS_DEFAULT" and "N_ACTIVE") then
    raise exception 'Phase 6 Slice 6 operational metadata reference data is incomplete.';
  end if;
  if not exists (
      select 1 from information_schema.columns
       where table_name = 'PA_TICKETS_NOTIFICATION_RULE_VERSION'
         and column_name = 'S_STATUS')
     or not exists (
      select 1 from information_schema.columns
       where table_name = 'PA_TICKETS_COMMUNICATION_TEMPLATE'
         and column_name = 'N_ROW_VERSION') then
    raise exception 'Phase 6 Slice 5 administration columns are incomplete.';
  end if;
  if (
      select count(*) from pg_constraint
       where conrelid = '"PA_TICKETS_COMMUNICATION"'::regclass
         and contype = 'f'
         and conname in (
             'FK_PA_TICKETS_COMM_NOTIFY_EXEC',
             'FK_PA_TICKETS_COMM_NOTIFY_RULE',
             'FK_PA_TICKETS_COMM_NOTIFY_VERSION',
             'FK_PA_TICKETS_COMM_NOTIFY_EVENT'))
     <> 4 then
    raise exception 'Phase 6 Slice 5 communication notification foreign keys are incomplete.';
  end if;
end $$;
select 'POSTGRESQL SLICE 5/6 SCHEMA SMOKE: PASS';
