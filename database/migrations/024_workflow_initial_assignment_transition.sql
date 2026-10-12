-- PA TICKETS additive migration 024.
-- Catalogue-bound workflows may route a new ticket directly to its first work
-- item because request-type selection already performed classification.
-- Specialized Incident/Problem lifecycles retain their existing root-status
-- transitions, and workflow work-item handoffs may return to assigned.

MERGE INTO PA_TICKETS_STATUS_TRANSITION target
USING (
    SELECT 'NEW' src, 'ASSIGNED' dst FROM DUAL UNION ALL
    SELECT 'NEW', 'RESOLVED' FROM DUAL UNION ALL
    SELECT 'NEW', 'CLOSED' FROM DUAL UNION ALL
    SELECT 'IN_PROGRESS', 'ASSIGNED' FROM DUAL UNION ALL
    SELECT 'REOPENED', 'RESOLVED' FROM DUAL UNION ALL
    SELECT 'REOPENED', 'CLOSED' FROM DUAL
) source
ON (target.S_FROM_STATUS_CODE = source.src AND target.S_TO_STATUS_CODE = source.dst)
WHEN NOT MATCHED THEN INSERT
    (N_STATUS_TRANSITION_ID, S_FROM_STATUS_CODE, S_TO_STATUS_CODE, N_ALLOWED, N_CREATED_BY)
    VALUES (PA_TICKETS_STATUS_TRANSITION_SEQ.NEXTVAL, source.src, source.dst, 1, 0);
