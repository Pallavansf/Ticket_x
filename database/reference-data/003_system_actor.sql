-- PA TICKETS additive migration 003: technical system actor
-- Run after 001 and 002. This is reference data, not a business user.
MERGE INTO PA_TICKETS_USER target
USING (SELECT 0 AS N_USER_ID, 'SYSTEM' AS S_USER_CODE, 'system' AS S_LOGIN_NAME,
              'PA Tickets System' AS S_DISPLAY_NAME FROM DUAL) source
ON (target.N_USER_ID = source.N_USER_ID)
WHEN NOT MATCHED THEN INSERT
 (N_USER_ID, S_USER_CODE, S_LOGIN_NAME, S_DISPLAY_NAME, N_ACTIVE, N_CREATED_BY)
 VALUES (source.N_USER_ID, source.S_USER_CODE, source.S_LOGIN_NAME, source.S_DISPLAY_NAME, 1, 0);
COMMIT;
