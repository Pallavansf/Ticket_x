-- Oracle SQL*Plus deployment preflight for PA Tickets on Oracle 19c.
-- This script is intentionally non-destructive and does not create users/grants.
-- Run as the deployment owner after migrations 001 through 010.
WHENEVER SQLERROR EXIT SQL.SQLCODE
SET SERVEROUTPUT ON
DECLARE
  l_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO l_count FROM user_tables WHERE table_name = 'PA_TICKETS';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20001, 'PA_TICKETS baseline table is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_tables WHERE table_name = 'PA_TICKETS_WORKFLOW_STEP_INSTANCE';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20002, 'Hardening migration 002 is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_sequences WHERE sequence_name = 'PA_TICKETS_WORK_ITEM_SEQ';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20003, 'Work-item sequence is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_indexes WHERE index_name = 'UX_PA_TICKETS_ATTACHMENT_CURRENT';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20004, 'Attachment current-version index is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_tables WHERE table_name = 'PA_TICKETS_MFA_ENROLLMENT';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20005, 'Foundation migration 005 is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_indexes WHERE index_name = 'UK_PA_TICKETS_WF_STEP_INSTANCE_JOIN';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20006, 'Concurrency migration 008 is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_indexes WHERE index_name = 'UK_PA_TICKETS_WI_STEP_INSTANCE';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20007, 'Work-item linkage migration 009 is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_indexes WHERE index_name = 'UK_PA_TICKETS_WORK_ITEM_PUBLIC_ID';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20008, 'Assignment/public-ID migration 010 is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_tab_columns WHERE table_name = 'PA_TICKETS_WORK_ITEM' AND column_name = 'S_PUBLIC_WORK_ITEM_ID' AND data_type = 'VARCHAR2' AND char_length = 16 AND nullable = 'N';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20009, 'Public work-item ID column is not VARCHAR2(16 CHAR) NOT NULL'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_indexes WHERE index_name = 'UK_PA_TICKETS_ACTIVE_ASSIGNMENT';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20010, 'Active assignment protection is missing'); END IF;
  SELECT COUNT(*) INTO l_count FROM PA_TICKETS_WORK_ITEM WHERE S_PUBLIC_WORK_ITEM_ID IS NULL;
  IF l_count <> 0 THEN RAISE_APPLICATION_ERROR(-20011, 'Null public work-item IDs remain'); END IF;
  SELECT COUNT(*) INTO l_count FROM user_tab_columns WHERE table_name = 'PA_TICKETS_IDEMPOTENCY' AND column_name = 'S_REQUEST_FINGERPRINT' AND data_type = 'VARCHAR2';
  IF l_count <> 1 THEN RAISE_APPLICATION_ERROR(-20012, 'Idempotency fingerprint column is missing'); END IF;  DBMS_OUTPUT.PUT_LINE('PA Tickets deployment preflight checks passed on the connected Oracle schema.');
END;
/
-- Review least-privilege grants separately. The application account requires only
-- SELECT/INSERT/UPDATE on business tables and sequence usage required by its services;
-- audit deletion and DDL privileges must not be granted.
EXIT SUCCESS






