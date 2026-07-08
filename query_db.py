import sqlite3

def query_db():
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()

    # List all tables first to confirm
    cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
    tables = [t[0] for t in cursor.fetchall()]
    print("Tables:", tables)

    # Let's find staging table name. In EF Core it might be BeneficiaryStaging or beneficiary_staging
    staging_tbl = next((t for t in tables if 'staging' in t.lower()), None)
    dig_tbl = next((t for t in tables if 'digitalid' in t.lower() or 'digital_id' in t.lower()), None)
    proj_ben_tbl = next((t for t in tables if 'projectbeneficiary' in t.lower() or 'project_beneficiary' in t.lower() or 'ayudaprojectbeneficiaries' in t.lower()), None)

    print(f"Staging table: {staging_tbl}")
    print(f"Digital ID table: {dig_tbl}")
    print(f"Project Beneficiary table: {proj_ben_tbl}")

    if staging_tbl:
        print("\n--- Searching staging table for Bianca ---")
        # Check column names
        cursor.execute(f"PRAGMA table_info({staging_tbl})")
        columns = [c[1] for c in cursor.fetchall()]
        print("Staging Columns:", columns)
        
        # Build query dynamically based on casing of staging ID
        staging_id_col = next((c for c in columns if 'stagingid' in c.lower() or 'id' == c.lower()), None)
        full_name_col = next((c for c in columns if 'fullname' in c.lower() or 'full_name' in c.lower()), None)
        ben_id_col = next((c for c in columns if 'beneficiaryid' in c.lower() or 'beneficiary_id' in c.lower()), None)
        civil_id_col = next((c for c in columns if 'civilregistry' in c.lower() or 'civil_registry' in c.lower()), None)
        
        cursor.execute(f"SELECT {staging_id_col}, {full_name_col}, {ben_id_col}, {civil_id_col} FROM {staging_tbl} WHERE {full_name_col} LIKE '%Bianca%'")
        staging_rows = cursor.fetchall()
        for r in staging_rows:
            print(f"StagingID: {r[0]}, Name: {r[1]}, BenID: {r[2]}, CivilRegID: {r[3]}")

    if dig_tbl and staging_tbl:
        print("\n--- Searching digital IDs for Bianca's staging rows ---")
        cursor.execute(f"PRAGMA table_info({dig_tbl})")
        columns = [c[1] for c in cursor.fetchall()]
        print("DigitalID Columns:", columns)
        
        staging_fk_col = next((c for c in columns if 'staging' in c.lower()), None)
        payload_col = next((c for c in columns if 'payload' in c.lower()), None)
        card_col = next((c for c in columns if 'card' in c.lower() or 'number' in c.lower()), None)
        active_col = next((c for c in columns if 'active' in c.lower()), None)
        
        for r in staging_rows:
            staging_id = r[0]
            cursor.execute(f"SELECT {card_col}, {payload_col}, {active_col} FROM {dig_tbl} WHERE {staging_fk_col} = ?", (staging_id,))
            dig_rows = cursor.fetchall()
            for d in dig_rows:
                print(f"StagingID: {staging_id}, Card: {d[0]}, Payload: {d[1]}, Active: {d[2]}")

    if proj_ben_tbl:
        print("\n--- Searching project beneficiaries table for Bianca ---")
        cursor.execute(f"PRAGMA table_info({proj_ben_tbl})")
        columns = [c[1] for c in cursor.fetchall()]
        print("Project Beneficiary Columns:", columns)
        
        id_col = next((c for c in columns if c.lower() == 'id'), None)
        prog_id_col = next((c for c in columns if 'program' in c.lower() or 'project' in c.lower()), None)
        staging_fk_col = next((c for c in columns if 'staging' in c.lower()), None)
        ben_id_col = next((c for c in columns if 'beneficiary' in c.lower() and 'staging' not in c.lower()), None)
        full_name_col = next((c for c in columns if 'name' in c.lower()), None)
        status_col = next((c for c in columns if 'status' in c.lower()), None)
        
        cursor.execute(f"SELECT {id_col}, {prog_id_col}, {staging_fk_col}, {ben_id_col}, {full_name_col}, {status_col} FROM {proj_ben_tbl} WHERE {full_name_col} LIKE '%Bianca%'")
        pb_rows = cursor.fetchall()
        for r in pb_rows:
            print(f"ProjBenID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, Name: {r[4]}, Status: {r[5]}")

    conn.close()

if __name__ == '__main__':
    query_db()
