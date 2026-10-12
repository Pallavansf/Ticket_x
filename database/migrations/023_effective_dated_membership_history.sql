-- PA TICKETS additive migration 023: allow effective-dated membership history.
-- Existing membership rows and audit history are retained. Application writes
-- serialize overlapping grants and authorization checks the effective window.

DECLARE
    PROCEDURE drop_constraint_if_present(p_table VARCHAR2, p_constraint VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM USER_CONSTRAINTS
        WHERE TABLE_NAME = UPPER(p_table)
          AND CONSTRAINT_NAME = UPPER(p_constraint);
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE ' || p_table || ' DROP CONSTRAINT ' || p_constraint;
        END IF;
    END;

    PROCEDURE create_index_if_missing(p_name VARCHAR2, p_ddl CLOB) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM USER_INDEXES
        WHERE INDEX_NAME = UPPER(p_name);
        IF v_count = 0 THEN
            EXECUTE IMMEDIATE p_ddl;
        END IF;
    END;
BEGIN
    drop_constraint_if_present('PA_TICKETS_USER_CLIENT', 'UK_PA_TICKETS_UC');

    create_index_if_missing(
        'IX_PA_TICKETS_UR_EFFECTIVE',
        'CREATE INDEX IX_PA_TICKETS_UR_EFFECTIVE ON PA_TICKETS_USER_ROLE (N_USER_ID, N_ROLE_ID, N_ACTIVE, D_VALID_FROM, D_VALID_TO)'
    );
    create_index_if_missing(
        'IX_PA_TICKETS_UC_EFFECTIVE',
        'CREATE INDEX IX_PA_TICKETS_UC_EFFECTIVE ON PA_TICKETS_USER_CLIENT (N_USER_ID, N_CLIENT_ID, N_ACTIVE, D_VALID_FROM, D_VALID_TO)'
    );
END;
/
