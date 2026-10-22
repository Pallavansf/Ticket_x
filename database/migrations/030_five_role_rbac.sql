-- SUPERSEDED DRAFT — DO NOT EXECUTE.
-- The fixed permission allowlist does not implement configurable module/action RBAC.
-- See Docs/PA_TICKETS_CONFIGURABLE_RBAC_DESIGN.md for the replacement design and migration plan.
-- Additive Oracle migration: five-role RBAC and effective-dated Admin Head delegation.
-- Existing role IDs, memberships, role-permission rows, and audit history are retained.

DECLARE
    v_count NUMBER;
    v_id NUMBER;
    PROCEDURE add_role(p_code VARCHAR2, p_name VARCHAR2, p_description VARCHAR2) IS
        v_role_id NUMBER;
        v_exists NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_exists FROM PA_TICKETS_ROLE WHERE S_ROLE_CODE = p_code;
        IF v_exists = 0 THEN
            LOOP
                v_role_id := PA_TICKETS_ROLE_SEQ.NEXTVAL;
                SELECT COUNT(*) INTO v_exists FROM PA_TICKETS_ROLE WHERE N_ROLE_ID = v_role_id;
                EXIT WHEN v_exists = 0;
            END LOOP;
            INSERT INTO PA_TICKETS_ROLE
                (N_ROLE_ID, S_ROLE_CODE, S_ROLE_NAME, S_DESCRIPTION, N_SYSTEM_ROLE, N_ACTIVE, N_CREATED_BY)
            VALUES
                (v_role_id, p_code, p_name, p_description, 1, 1, 0);
        END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_count
      FROM PA_TICKETS_PERMISSION
     WHERE N_ACTIVE = 1 AND S_PERMISSION_CODE IN (
        'APPROVAL_ADMIN', 'APPROVAL_PUBLISH', 'APPROVAL_READ', 'AUDIT_EXPORT', 'AUDITOR_VIEW',
        'CATALOGUE_PUBLISH', 'CATEGORY_ADMIN', 'CATEGORY_READ', 'CLIENT_ADMIN',
        'COMMUNICATION_ADMIN', 'COMMUNICATION_PUBLISH', 'COMMUNICATION_READ', 'COMMUNICATION_RETIRE',
        'CONFIG_ADMIN', 'NOTIFICATION_ADMIN', 'NOTIFICATION_PUBLISH', 'NOTIFICATION_READ',
        'NOTIFICATION_RETIRE', 'REQUEST_TYPE_ADMIN', 'REQUEST_TYPE_READ', 'ROLE_ADMIN',
        'SERVICE_ADMIN', 'SERVICE_READ', 'SLA_ADMIN', 'SLA_PUBLISH', 'SLA_READ', 'SLA_RETIRE',
        'TICKET_ADMIN', 'TICKET_READ', 'TICKET_UPDATE', 'USER_ADMIN', 'WORKFLOW_ADMIN',
        'WORKFLOW_PUBLISH', 'WORKFLOW_READ');
    IF v_count <> 34 THEN
        RAISE_APPLICATION_ERROR(-20030, 'RBAC migration requires the reviewed active permission catalog.');
    END IF;

    add_role('USER', 'User', 'Standard ticket operations within authorized scope.');
    add_role('ADMIN', 'Admin', 'Department-scoped administrative operations.');
    add_role('SUPER_ADMIN', 'Super Admin', 'Organization-wide administration.');
    add_role('ADMIN_HEAD', 'Admin Head', 'Admin capabilities and explicitly delegated Admin management.');
    add_role('SPECTATOR', 'Spectator', 'Read-only access within authorized scope.');

    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'PA_TICKETS_ADMIN_DELEGATION_SEQ';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE PA_TICKETS_ADMIN_DELEGATION_SEQ START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;

    SELECT COUNT(*) INTO v_count FROM USER_TABLES WHERE TABLE_NAME = 'PA_TICKETS_ADMIN_HEAD_DELEGATION';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE q'[
            CREATE TABLE PA_TICKETS_ADMIN_HEAD_DELEGATION (
                N_ADMIN_DELEGATION_ID NUMBER(18) NOT NULL,
                N_ADMIN_HEAD_USER_ID NUMBER(18) NOT NULL,
                N_ADMIN_USER_ID NUMBER(18) NOT NULL,
                D_VALID_FROM TIMESTAMP WITH TIME ZONE NOT NULL,
                D_VALID_TO TIMESTAMP WITH TIME ZONE,
                N_ACTIVE NUMBER(1) DEFAULT 1 NOT NULL,
                D_CREATED_AT TIMESTAMP WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
                N_CREATED_BY NUMBER(18) NOT NULL,
                CONSTRAINT PK_PA_TICKETS_ADMIN_DELEG PRIMARY KEY (N_ADMIN_DELEGATION_ID),
                CONSTRAINT FK_PA_TICKETS_AHD_HEAD FOREIGN KEY (N_ADMIN_HEAD_USER_ID) REFERENCES PA_TICKETS_USER(N_USER_ID),
                CONSTRAINT FK_PA_TICKETS_AHD_ADMIN FOREIGN KEY (N_ADMIN_USER_ID) REFERENCES PA_TICKETS_USER(N_USER_ID),
                CONSTRAINT CK_PA_TICKETS_AHD_ACTIVE CHECK (N_ACTIVE IN (0,1)),
                CONSTRAINT CK_PA_TICKETS_AHD_DATES CHECK (D_VALID_TO IS NULL OR D_VALID_TO > D_VALID_FROM),
                CONSTRAINT UK_PA_TICKETS_AHD_START UNIQUE (N_ADMIN_HEAD_USER_ID, N_ADMIN_USER_ID, D_VALID_FROM)
            )
        ]';
    END IF;

    FOR grant_row IN (
        SELECT r.N_ROLE_ID, p.N_PERMISSION_ID
          FROM PA_TICKETS_ROLE r
          JOIN PA_TICKETS_PERMISSION p
            ON (r.S_ROLE_CODE = 'SUPER_ADMIN' AND p.N_ACTIVE = 1)
            OR (r.S_ROLE_CODE IN ('ADMIN', 'ADMIN_HEAD') AND p.S_PERMISSION_CODE IN
                ('TICKET_READ', 'TICKET_UPDATE', 'USER_ADMIN', 'ROLE_ADMIN'))
            OR (r.S_ROLE_CODE = 'USER' AND p.S_PERMISSION_CODE IN ('TICKET_READ', 'TICKET_UPDATE'))
            OR (r.S_ROLE_CODE = 'SPECTATOR' AND p.S_PERMISSION_CODE IN
                ('APPROVAL_READ', 'AUDITOR_VIEW', 'CATEGORY_READ', 'COMMUNICATION_READ',
                 'NOTIFICATION_READ', 'REQUEST_TYPE_READ', 'SERVICE_READ', 'SLA_READ',
                 'TICKET_READ', 'WORKFLOW_READ'))
         WHERE NOT EXISTS (
             SELECT 1 FROM PA_TICKETS_ROLE_PERMISSION rp
              WHERE rp.N_ROLE_ID = r.N_ROLE_ID AND rp.N_PERMISSION_ID = p.N_PERMISSION_ID)
    ) LOOP
        LOOP
            v_id := PA_TICKETS_ROLE_PERMISSION_SEQ.NEXTVAL;
            SELECT COUNT(*) INTO v_count FROM PA_TICKETS_ROLE_PERMISSION WHERE N_ROLE_PERMISSION_ID = v_id;
            EXIT WHEN v_count = 0;
        END LOOP;
        INSERT INTO PA_TICKETS_ROLE_PERMISSION (N_ROLE_PERMISSION_ID, N_ROLE_ID, N_PERMISSION_ID, N_CREATED_BY)
        VALUES (v_id, grant_row.N_ROLE_ID, grant_row.N_PERMISSION_ID, 0);
    END LOOP;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_PA_TICKETS_AHD_EFFECTIVE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_PA_TICKETS_AHD_EFFECTIVE ON PA_TICKETS_ADMIN_HEAD_DELEGATION (N_ADMIN_HEAD_USER_ID, N_ADMIN_USER_ID, N_ACTIVE, D_VALID_FROM, D_VALID_TO)';
    END IF;
END;
/

COMMIT;
