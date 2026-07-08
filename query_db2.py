import sqlite3

def query_db():
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()

    print("--- Searching ayuda_project_beneficiaries for Bianca ---")
    cursor.execute("PRAGMA table_info(ayuda_project_beneficiaries)")
    columns = [c[1] for c in cursor.fetchall()]
    print("Columns:", columns)

    cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries WHERE full_name LIKE '%Bianca%'")
    rows = cursor.fetchall()
    for r in rows:
        print(f"ID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

    print("\n--- Searching ayuda_project_beneficiaries where beneficiary_id matches Bianca's Staging StagingID (2) BenID ---")
    # Bianca's staging BenID: e5a38eba-f907-41ad-9a59-f70364df773e
    # Note that in the output it was: e5a38eba-f907-41ad-9a59-f70364df773e
    cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries WHERE beneficiary_id = 'e5a38eba-f907-41ad-9a59-f70364df773e'")
    rows = cursor.fetchall()
    for r in rows:
        print(f"ID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

    print("\n--- Let's find all rows in ayuda_project_beneficiaries to see what's in there ---")
    cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries")
    rows = cursor.fetchall()
    for r in rows[:20]:
        print(f"ID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

    conn.close()

if __name__ == '__main__':
    query_db()
