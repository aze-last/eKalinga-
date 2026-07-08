import sqlite3

def query_db():
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()

    cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries")
    rows = cursor.fetchall()
    print("Project Beneficiaries in Root DB:")
    for r in rows:
        print(f"ID: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

    conn.close()

if __name__ == '__main__':
    query_db()
