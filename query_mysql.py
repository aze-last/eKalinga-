import mysql.connector

def query_local_mysql():
    config = {
        'host': '127.0.0.1',
        'port': 3306,
        'user': 'root',
        'password': 'codenameHylux122818',
        'database': 'attendance_shifting_db'
    }
    
    print("=================== Inspecting Local MySQL (attendance_shifting_db) ===================")
    try:
        conn = mysql.connector.connect(**config)
        cursor = conn.cursor()

        print("--- Staging Records for Bianca ---")
        cursor.execute("SELECT id, first_name, middle_name, last_name, full_name, beneficiary_id, civil_registry_id, verification_status FROM beneficiary_staging WHERE full_name LIKE '%Bianca%'")
        s_rows = cursor.fetchall()
        for r in s_rows:
            print(f"StagingID: {r[0]}, Name: {r[1]} {r[2]} {r[3]} ({r[4]}), BenID: {r[5]}, CivilRegID: {r[6]}, Status: {r[7]}")

        print("\n--- Digital ID Records for Bianca's StagingIDs ---")
        for r in s_rows:
            sid = r[0]
            cursor.execute("SELECT id, beneficiary_staging_id, card_number, qr_payload, is_active FROM beneficiary_digital_ids WHERE beneficiary_staging_id = ?", (sid,))
            d_rows = cursor.fetchall()
            for d in d_rows:
                print(f"DigitalID PK: {d[0]}, StagingID: {d[1]}, Card: {d[2]}, Payload: {d[3]}, Active: {d[4]}")

        print("\n--- Project Beneficiary Records for Bianca ---")
        cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries WHERE full_name LIKE '%Bianca%'")
        pb_rows = cursor.fetchall()
        for r in pb_rows:
            print(f"ProjBen PK: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

        conn.close()
    except Exception as e:
        print(f"Error querying local MySQL: {e}")

def query_remote_mysql():
    config = {
        'host': '194.59.164.58',
        'port': 3306,
        'user': 'u621755393_ams_user',
        'password': 'Ams@2026',
        'database': 'u621755393_ams'
    }
    
    print("\n=================== Inspecting Remote MySQL (u621755393_ams) ===================")
    try:
        conn = mysql.connector.connect(**config)
        cursor = conn.cursor()

        print("--- Staging Records for Bianca ---")
        cursor.execute("SELECT id, first_name, middle_name, last_name, full_name, beneficiary_id, civil_registry_id, verification_status FROM beneficiary_staging WHERE full_name LIKE '%Bianca%'")
        s_rows = cursor.fetchall()
        for r in s_rows:
            print(f"StagingID: {r[0]}, Name: {r[1]} {r[2]} {r[3]} ({r[4]}), BenID: {r[5]}, CivilRegID: {r[6]}, Status: {r[7]}")

        print("\n--- Digital ID Records for Bianca's StagingIDs ---")
        for r in s_rows:
            sid = r[0]
            cursor.execute("SELECT id, beneficiary_staging_id, card_number, qr_payload, is_active FROM beneficiary_digital_ids WHERE beneficiary_staging_id = ?", (sid,))
            d_rows = cursor.fetchall()
            for d in d_rows:
                print(f"DigitalID PK: {d[0]}, StagingID: {d[1]}, Card: {d[2]}, Payload: {d[3]}, Active: {d[4]}")

        print("\n--- Project Beneficiary Records for Bianca ---")
        cursor.execute("SELECT id, ayuda_program_id, beneficiary_staging_id, beneficiary_id, civil_registry_id, full_name, status FROM ayuda_project_beneficiaries WHERE full_name LIKE '%Bianca%'")
        pb_rows = cursor.fetchall()
        for r in pb_rows:
            print(f"ProjBen PK: {r[0]}, ProjID: {r[1]}, StagingID: {r[2]}, BenID: {r[3]}, CivilRegID: {r[4]}, Name: {r[5]}, Status: {r[6]}")

        conn.close()
    except Exception as e:
        print(f"Error querying remote MySQL: {e}")

if __name__ == '__main__':
    query_local_mysql()
    query_remote_mysql()
