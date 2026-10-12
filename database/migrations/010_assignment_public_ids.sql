-- PA TICKETS additive migration 010: opaque public IDs, active assignment protection,
-- and request-bound idempotency. This migration is self-contained for existing rows.
ALTER TABLE PA_TICKETS_WORK_ITEM ADD (S_PUBLIC_WORK_ITEM_ID VARCHAR2(16 CHAR));
ALTER TABLE PA_TICKETS_IDEMPOTENCY ADD (S_REQUEST_FINGERPRINT VARCHAR2(128));

DECLARE
    l_alphabet CONSTANT VARCHAR2(32) := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    l_candidate VARCHAR2(16);
    l_hex VARCHAR2(32);
    l_exists NUMBER;
    l_attempts PLS_INTEGER;
BEGIN
    FOR r IN (SELECT N_WORK_ITEM_ID FROM PA_TICKETS_WORK_ITEM WHERE S_PUBLIC_WORK_ITEM_ID IS NULL FOR UPDATE) LOOP
        l_attempts := 0;
        LOOP
            l_attempts := l_attempts + 1;
            IF l_attempts > 16 THEN
                RAISE_APPLICATION_ERROR(-20100, 'Unable to allocate a unique public work-item ID');
            END IF;
            l_hex := RAWTOHEX(DBMS_CRYPTO.RANDOMBYTES(16));
            l_candidate := NULL;
            FOR i IN 1..16 LOOP
                l_candidate := l_candidate ||
                    SUBSTR(l_alphabet, MOD(TO_NUMBER(SUBSTR(l_hex, ((i - 1) * 2) + 1, 2), 'XX'), 32) + 1, 1);
            END LOOP;
            SELECT COUNT(*) INTO l_exists
              FROM PA_TICKETS_WORK_ITEM
             WHERE S_PUBLIC_WORK_ITEM_ID = l_candidate;
            EXIT WHEN l_exists = 0;
        END LOOP;
        UPDATE PA_TICKETS_WORK_ITEM
           SET S_PUBLIC_WORK_ITEM_ID = l_candidate
         WHERE N_WORK_ITEM_ID = r.N_WORK_ITEM_ID;
    END LOOP;
END;
/

DECLARE
    l_nulls NUMBER;
    l_duplicates NUMBER;
    l_invalid NUMBER;
BEGIN
    SELECT COUNT(*) INTO l_nulls FROM PA_TICKETS_WORK_ITEM WHERE S_PUBLIC_WORK_ITEM_ID IS NULL;
    SELECT COUNT(*) INTO l_duplicates FROM (
        SELECT S_PUBLIC_WORK_ITEM_ID FROM PA_TICKETS_WORK_ITEM
        GROUP BY S_PUBLIC_WORK_ITEM_ID HAVING COUNT(*) > 1
    );
    SELECT COUNT(*) INTO l_invalid FROM PA_TICKETS_WORK_ITEM
     WHERE NOT REGEXP_LIKE(S_PUBLIC_WORK_ITEM_ID, '^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{16}$');
    IF l_nulls > 0 OR l_duplicates > 0 OR l_invalid > 0 THEN
        RAISE_APPLICATION_ERROR(-20101, 'Public work-item ID backfill validation failed');
    END IF;
END;
/

CREATE UNIQUE INDEX UK_PA_TICKETS_WORK_ITEM_PUBLIC_ID
    ON PA_TICKETS_WORK_ITEM (S_PUBLIC_WORK_ITEM_ID);

ALTER TABLE PA_TICKETS_WORK_ITEM MODIFY (S_PUBLIC_WORK_ITEM_ID NOT NULL);

CREATE UNIQUE INDEX UK_PA_TICKETS_ACTIVE_ASSIGNMENT
    ON PA_TICKETS_ASSIGNMENT
    (N_WORK_ITEM_ID, CASE WHEN D_UNASSIGNED_AT IS NULL THEN 1 ELSE NULL END);

COMMIT;
