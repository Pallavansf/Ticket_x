# Database deployment

`schema/001_PA_TICKETS_SCHEMA.sql` is the supplied Oracle baseline. Deploy it to a clean Oracle schema before ordered additive scripts in `migrations/`.

- `schema/`: baseline DDL
- `reference-data/`: separately deployable reference data
- `indexes/`: additional/performance indexes
- `migrations/`: ordered additive migrations
- `scripts/`: deployment helpers
