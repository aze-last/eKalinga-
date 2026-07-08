import sqlite3

db_path = 'bin/Debug/net9.0-windows/ams.db'

def query_db():
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    print("--- Searching BeneficiaryStaging for Bianca ---")
    cursor.execute("SELECT StagingID, FirstName, MiddleName, LastName, FullName, BeneficiaryId, CivilRegistryId, VerificationStatus FROM BeneficiaryStaging WHERE FullName LIKE '%Bianca%'")
    staging_rows = cursor.fetchall()
    for row in staging_rows:
        print(f"StagingID: {row[0]}, Name: {row[1]} {row[2]} {row[3]} ({row[4]}), BeneficiaryId: {row[5]}, CivilRegistryId: {row[6]}, Status: {row[7]}")

    print("\n--- Columns in beneficiary_digital_ids ---")
    cursor.execute("PRAGMA table_info(beneficiary_digital_ids)")
    cols = [c[1] for c in cursor.fetchall()]
    print(cols)

    card_col = next((c for c in cols if 'card' in c.lower()), None)
    payload_col = next((c for c in cols if 'payload' in c.lower()), None)
    staging_col = next((c for c in cols if 'staging' in c.lower()), None)
    active_col = next((c for c in cols if 'active' in c.lower()), None)

    print("\n--- Searching BeneficiaryDigitalIds for Bianca's StagingIDs ---")
    for row in staging_rows:
        staging_id = row[0]
        cursor.execute(f"SELECT {card_col}, {payload_col}, {active_col} FROM beneficiary_digital_ids WHERE {staging_col} = ?", (staging_id,))
        dig_rows = cursor.fetchall()
        for d in dig_rows:
            print(f"StagingID: {staging_id}, Card: {d[0]}, Payload: {d[1]}, Active: {d[2]}")

    print("\n--- Columns in ayuda_project_beneficiaries ---")
    cursor.execute("PRAGMA table_info(ayuda_project_beneficiaries)")
    pb_cols = [c[1] for c in cursor.fetchall()]
    print(pb_cols)

    print("\n--- Searching ayuda_project_beneficiaries for Bianca ---")
    cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries WHERE full_name LIKE '%Bianca%'")
    rows = cursor.fetchall()
    for r in rows:
        print(f"ID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

    conn.close()

if __name__ == '__main__':
    query_db()
