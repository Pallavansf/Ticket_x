-- Problem management additive PostgreSQL schema.
create sequence if not exists "PA_TICKETS_PROBLEM_SEQ";

create table if not exists "PA_TICKETS_PROBLEM" (
  "N_PROBLEM_ID" bigint primary key default nextval('"PA_TICKETS_PROBLEM_SEQ"'),
  "N_TICKET_ID" bigint not null unique references "PA_TICKETS"("N_TICKET_ID"),
  "S_PROBLEM_STATEMENT" text not null,
  "S_ROOT_CAUSE" text,
  "S_WORKAROUND" text,
  "S_PERMANENT_FIX" text,
  "S_PROBLEM_STATUS" varchar(40) not null default 'OPEN',
  "D_IDENTIFIED_AT" timestamptz,
  "N_IDENTIFIED_BY" bigint,
  "D_ROOT_CAUSE_IDENTIFIED_AT" timestamptz,
  "N_ROOT_CAUSE_IDENTIFIED_BY" bigint,
  "N_KNOWN_ERROR" boolean not null default false,
  "D_KNOWN_ERROR_DECLARED_AT" timestamptz,
  "N_KNOWN_ERROR_DECLARED_BY" bigint,
  "D_RESOLVED_AT" timestamptz,
  "N_RESOLVED_BY" bigint,
  "S_RESOLUTION_SUMMARY" text,
  "D_CLOSED_AT" timestamptz,
  "N_CLOSED_BY" bigint,
  "D_REOPENED_AT" timestamptz,
  "N_REOPENED_BY" bigint,
  "N_CREATED_BY" bigint not null,
  "D_CREATED_AT" timestamptz not null,
  "D_UPDATED_AT" timestamptz not null,
  "S_IDEMPOTENCY_KEY" varchar(200)
);
create index if not exists "IX_PA_TICKETS_PROBLEM_STATUS" on "PA_TICKETS_PROBLEM"("S_PROBLEM_STATUS");
create unique index if not exists "UX_PA_TICKETS_PROBLEM_IDEMPOTENCY" on "PA_TICKETS_PROBLEM"("S_IDEMPOTENCY_KEY") where "S_IDEMPOTENCY_KEY" is not null;
create unique index if not exists "UX_PA_TICKETS_RELATION_UNIQUE" on "PA_TICKETS_RELATION"("N_TICKET_ID","N_RELATED_TICKET_ID","S_RELATION_TYPE");