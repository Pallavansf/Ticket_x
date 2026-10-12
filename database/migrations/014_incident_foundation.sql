-- Incident additive PostgreSQL schema.
create sequence if not exists "PA_TICKETS_INCIDENT_SEQ";

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
