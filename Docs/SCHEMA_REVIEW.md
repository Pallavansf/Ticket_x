# Baseline schema review

## Result
The supplied schema is a strong normalized baseline: it contains 58 tables covering organization access, configurable catalogue/forms, a common ticket model, work routing/history, versioned workflow, approvals, SLA/calendar, communications, evidence, changes/problems, notification/escalation, KPI events, and audit packages. It follows the required object/column naming convention and uses `TIMESTAMP WITH TIME ZONE` for business/audit events.

This was a static review only. An Oracle deployment test is pending an Oracle connection and target database version.

## Required additive follow-ups
1. **Primary-key allocation:** only four sequences are present while most tables have numeric primary keys without an identity/default. Select and document one safe allocation strategy (per-table sequences, centrally managed sequence allocation, or application-generated values) before persistence is added. Never use `MAX(id)+1`.
2. **Parallel workflow state:** `PA_TICKETS_WORKFLOW_INSTANCE.N_CURRENT_STEP_ID` represents one current step, whereas the requirements permit parallel steps. Add a versioned active-step/step-instance table in a migration so concurrent state can be represented explicitly.
3. **Attachment current version:** `N_CURRENT_VERSION` and `S_VERSION_STATUS` need a transactionally enforced single-current-version rule. Add a database constraint/index approach where practical and enforce it in the application transaction.
4. **Audit hash lifecycle:** `S_HASH` and `S_PREVIOUS_HASH` exist, but the canonical hash serialization, chain scope, and write authority must be defined before audit services are implemented.
5. **Deployment preflight:** create a dedicated migration/reference-data script for the technical system actor if it is required by future foreign keys, then test DDL, reference data, rollback, and least-privilege database grants on the supported Oracle version.

No baseline object was renamed or altered. These items are intentionally recorded for additive migrations rather than silently changing the supplied schema.
