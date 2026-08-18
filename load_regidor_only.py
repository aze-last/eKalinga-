import sqlite3
import uuid
import secrets
from datetime import datetime
import mysql.connector

NOW = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

REGIDOR_HOUSEHOLD = {
    "code": "HH-0005",
    "head_name": "Bienvinido M. Regidor",
    "address": "Regidor Residence, Purok 5",
    "purok": "Purok 5",
    "contact": "0900-000-0005",
    "members": [
        {
            "surname": "Regidor",
            "given": "Bienvinido M.",
            "full_name": "Bienvinido M. Regidor",
            "relationship": "Father",
            "sex": "Male",
            "dob": "1961-01-01",
            "age": "65",
            "is_senior": 1,
            "marital": "Married",
            "res_id": 950001,
            "ben_id": "BEN-0001",
            "crn": "CRN-0001"
        },
        {
            "surname": "Regidor",
            "given": "Maria Jocelyn G.",
            "full_name": "Maria Jocelyn G. Regidor",
            "relationship": "Mother",
            "sex": "Female",
            "dob": "1965-05-15",
            "age": "61",
            "is_senior": 1,
            "marital": "Married",
            "res_id": 950002,
            "ben_id": "BEN-0002",
            "crn": "CRN-0002"
        },
        {
            "surname": "Regidor",
            "given": "Bien Josef G.",
            "full_name": "Bien Josef G. Regidor",
            "relationship": "Son",
            "sex": "Male",
            "dob": "2000-08-20",
            "age": "26",
            "is_senior": 0,
            "marital": "Single",
            "res_id": 950003,
            "ben_id": "BEN-0003",
            "crn": "CRN-0003"
        }
    ]
}

def seed_sqlite(db_path):
    import os
    if not os.path.exists(db_path):
        print(f"File {db_path} does not exist, skipping.")
        return

    print(f"\n=== Seeding SQLite: {db_path} ===")
    con = sqlite3.connect(db_path)
    cur = con.cursor()

    # Check tables
    cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
    tables = [r[0] for r in cur.fetchall()]

    admin = cur.execute("SELECT Id FROM users ORDER BY Id LIMIT 1").fetchone() if 'users' in tables else None
    issued_by = admin[0] if admin else 1

    # Clean existing mock data
    mock_surnames = ['Mockson', 'Testa', 'Sample', 'Demo']
    for s in mock_surnames:
        if 'BeneficiaryStaging' in tables:
            cur.execute("DELETE FROM BeneficiaryStaging WHERE LastName=?", (s,))
        if 'beneficiary_digital_ids' in tables:
            cur.execute("""DELETE FROM beneficiary_digital_ids WHERE beneficiary_staging_id IN 
                           (SELECT StagingID FROM BeneficiaryStaging WHERE LastName=?)""", (s,))

    if 'households' in tables:
        cur.execute("DELETE FROM households WHERE household_code IN ('HH-0001', 'HH-0002', 'HH-0003', 'HH-0004', 'HH-0006')")

    # 1. Household
    hh_id = None
    if 'households' in tables:
        r = cur.execute("SELECT id FROM households WHERE household_code=?", (REGIDOR_HOUSEHOLD["code"],)).fetchone()
        if r:
            hh_id = r[0]
            cur.execute("""UPDATE households SET head_name=?, address_line=?, purok=?, contact_number=?, updated_at=? WHERE id=?""",
                        (REGIDOR_HOUSEHOLD["head_name"], REGIDOR_HOUSEHOLD["address"], REGIDOR_HOUSEHOLD["purok"],
                         REGIDOR_HOUSEHOLD["contact"], NOW, hh_id))
        else:
            cur.execute("""INSERT INTO households (SyncId, household_code, head_name, address_line, purok, contact_number, status, created_at, updated_at)
                           VALUES (?,?,?,?,?,?,?,?,?)""",
                        (str(uuid.uuid4()), REGIDOR_HOUSEHOLD["code"], REGIDOR_HOUSEHOLD["head_name"],
                         REGIDOR_HOUSEHOLD["address"], REGIDOR_HOUSEHOLD["purok"], REGIDOR_HOUSEHOLD["contact"],
                         "Active", NOW, NOW))
            hh_id = cur.lastrowid
    else:
        hh_id = 5

    # 2. Members & Staging & Digital IDs
    for mem in REGIDOR_HOUSEHOLD["members"]:
        member_id = None
        if 'household_members' in tables and hh_id:
            m = cur.execute("SELECT id FROM household_members WHERE household_id=? AND full_name=?", (hh_id, mem["full_name"])).fetchone()
            if m:
                member_id = m[0]
            else:
                cur.execute("""INSERT INTO household_members (SyncId, household_id, full_name, relationship_to_head, occupation, is_cash_for_work_eligible, created_at, updated_at)
                               VALUES (?,?,?,?,?,?,?,?)""",
                            (str(uuid.uuid4()), hh_id, mem["full_name"], mem["relationship"], "N/A", 1, NOW, NOW))
                member_id = cur.lastrowid

        if 'BeneficiaryStaging' in tables:
            s = cur.execute("SELECT StagingID FROM BeneficiaryStaging WHERE LOWER(LastName)=? AND LOWER(FirstName)=?",
                            (mem["surname"].lower(), mem["given"].lower())).fetchone()
            if s:
                staging_id = s[0]
                cur.execute("""UPDATE BeneficiaryStaging SET FullName=?, CivilRegistryId=?, ResidentsId=?, BeneficiaryId=?,
                               Sex=?, DateOfBirth=?, Age=?, MaritalStatus=?, Address=?, IsSenior=?, VerificationStatus=1,
                               LinkedHouseholdId=?, LinkedHouseholdMemberId=?, UpdatedAt=? WHERE StagingID=?""",
                            (mem["full_name"], mem["crn"], mem["res_id"], mem["ben_id"], mem["sex"], mem["dob"],
                             mem["age"], mem["marital"], REGIDOR_HOUSEHOLD["address"], mem["is_senior"],
                             hh_id, member_id, NOW, staging_id))
            else:
                cur.execute("""INSERT INTO BeneficiaryStaging (SyncId, UpdatedAt, ResidentsId, BeneficiaryId, CivilRegistryId,
                               LastName, FirstName, FullName, Sex, DateOfBirth, Age, MaritalStatus, Address, IsPwd, IsSenior,
                               VerificationStatus, LinkedHouseholdId, LinkedHouseholdMemberId, ImportedAt)
                               VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                            (str(uuid.uuid4()), NOW, mem["res_id"], mem["ben_id"], mem["crn"], mem["surname"],
                             mem["given"], mem["full_name"], mem["sex"], mem["dob"], mem["age"], mem["marital"],
                             REGIDOR_HOUSEHOLD["address"], 0, mem["is_senior"], 1, hh_id, member_id, NOW))
                staging_id = cur.lastrowid

            if 'beneficiary_digital_ids' in tables:
                d = cur.execute("SELECT id FROM beneficiary_digital_ids WHERE beneficiary_staging_id=?", (staging_id,)).fetchone()
                if not d:
                    qr = f"ASMBID{staging_id:06d}{secrets.token_hex(8).upper()}"
                    dcols = [c[1] for c in cur.execute("PRAGMA table_info(beneficiary_digital_ids)").fetchall()]
                    vals = {
                        'SyncId': str(uuid.uuid4()),
                        'UpdatedAt': NOW,
                        'beneficiary_staging_id': staging_id,
                        'household_id': hh_id,
                        'household_member_id': member_id,
                        'card_number': f"BID-{staging_id:06d}",
                        'qr_payload': qr,
                        'issued_by_user_id': issued_by,
                        'issued_at': NOW,
                        'is_active': 1
                    }
                    use = {k: v for k, v in vals.items() if k in dcols}
                    cur.execute(f"INSERT INTO beneficiary_digital_ids ({','.join(use)}) VALUES ({','.join('?'*len(use))})", list(use.values()))

    con.commit()
    con.close()
    print(f"SQLite {db_path} updated with Regidor household successfully.")

def seed_mysql(label, **conn_args):
    print(f"\n=== Seeding MySQL: {label} ===")
    try:
        conn = mysql.connector.connect(**conn_args)
        cur = conn.cursor()
    except Exception as e:
        print(f"Cannot connect to MySQL ({label}): {e}")
        return

    cur.execute("SHOW TABLES")
    all_tables = [r[0] for r in cur.fetchall()]
    table_map = {t.lower(): t for t in all_tables}

    user_tbl = table_map.get('users', 'users')
    hh_tbl = table_map.get('households', 'households')
    member_tbl = table_map.get('household_members', 'household_members')
    staging_tbl = table_map.get('beneficiarystaging', 'BeneficiaryStaging')
    dig_tbl = table_map.get('beneficiary_digital_ids', 'beneficiary_digital_ids')
    val_tbl = table_map.get('val_beneficiaries', 'val_beneficiaries')

    cur.execute(f"SELECT Id FROM `{user_tbl}` ORDER BY Id LIMIT 1")
    admin = cur.fetchone()
    issued_by = admin[0] if admin else 1

    # Remove mock households that are not Regidor
    mock_surnames = ['Mockson', 'Testa', 'Sample', 'Demo']
    for s in mock_surnames:
        if staging_tbl in all_tables:
            cur.execute(f"DELETE FROM `{staging_tbl}` WHERE LastName=%s", (s,))
        if val_tbl in all_tables:
            cur.execute(f"DELETE FROM `{val_tbl}` WHERE last_name=%s", (s,))

    if hh_tbl in all_tables:
        cur.execute(f"DELETE FROM `{hh_tbl}` WHERE household_code IN ('HH-0001', 'HH-0002', 'HH-0003', 'HH-0004', 'HH-0006')")

    # 1. Household
    cur.execute(f"SHOW COLUMNS FROM `{hh_tbl}`")
    hh_cols = [r[0] for r in cur.fetchall()]

    cur.execute(f"SELECT id FROM `{hh_tbl}` WHERE household_code=%s", (REGIDOR_HOUSEHOLD["code"],))
    r = cur.fetchone()
    if r:
        hh_id = r[0]
    else:
        vals = {
            'SyncId': str(uuid.uuid4()),
            'household_code': REGIDOR_HOUSEHOLD["code"],
            'head_name': REGIDOR_HOUSEHOLD["head_name"],
            'address_line': REGIDOR_HOUSEHOLD["address"],
            'purok': REGIDOR_HOUSEHOLD["purok"],
            'contact_number': REGIDOR_HOUSEHOLD["contact"],
            'status': 'Active',
            'created_at': NOW,
            'updated_at': NOW
        }
        use = {k: v for k, v in vals.items() if k in hh_cols}
        cols = ','.join(f'`{c}`' for c in use)
        ph = ','.join(['%s'] * len(use))
        cur.execute(f"INSERT INTO `{hh_tbl}` ({cols}) VALUES ({ph})", list(use.values()))
        hh_id = cur.lastrowid

    # 2. Members, Staging, ValBeneficiaries, Digital IDs
    cur.execute(f"SHOW COLUMNS FROM `{member_tbl}`")
    member_cols = [r[0] for r in cur.fetchall()]

    cur.execute(f"SHOW COLUMNS FROM `{staging_tbl}`")
    staging_cols = [r[0] for r in cur.fetchall()]

    for mem in REGIDOR_HOUSEHOLD["members"]:
        # Member
        cur.execute(f"SELECT id FROM `{member_tbl}` WHERE household_id=%s AND full_name=%s", (hh_id, mem["full_name"]))
        m = cur.fetchone()
        if m:
            member_id = m[0]
        else:
            vals = {
                'SyncId': str(uuid.uuid4()),
                'household_id': hh_id,
                'full_name': mem["full_name"],
                'relationship_to_head': mem["relationship"],
                'occupation': "N/A",
                'is_cash_for_work_eligible': 1,
                'created_at': NOW,
                'updated_at': NOW
            }
            use = {k: v for k, v in vals.items() if k in member_cols}
            cols = ','.join(f'`{c}`' for c in use)
            ph = ','.join(['%s'] * len(use))
            cur.execute(f"INSERT INTO `{member_tbl}` ({cols}) VALUES ({ph})", list(use.values()))
            member_id = cur.lastrowid

        # Staging
        cur.execute(f"SELECT StagingID FROM `{staging_tbl}` WHERE LastName=%s AND FirstName=%s", (mem["surname"], mem["given"]))
        s = cur.fetchone()
        if s:
            staging_id = s[0]
            cur.execute(f"""UPDATE `{staging_tbl}` SET FullName=%s, CivilRegistryId=%s, ResidentsId=%s, BeneficiaryId=%s,
                            Sex=%s, DateOfBirth=%s, Age=%s, MaritalStatus=%s, Address=%s, IsSenior=%s, VerificationStatus=1,
                            LinkedHouseholdId=%s, LinkedHouseholdMemberId=%s, UpdatedAt=%s WHERE StagingID=%s""",
                        (mem["full_name"], mem["crn"], mem["res_id"], mem["ben_id"], mem["sex"], mem["dob"],
                         mem["age"], mem["marital"], REGIDOR_HOUSEHOLD["address"], mem["is_senior"],
                         hh_id, member_id, NOW, staging_id))
        else:
            vals = {
                'SyncId': str(uuid.uuid4()),
                'UpdatedAt': NOW,
                'ResidentsId': mem["res_id"],
                'residents_id': mem["res_id"],
                'BeneficiaryId': mem["ben_id"],
                'beneficiary_id': mem["ben_id"],
                'CivilRegistryId': mem["crn"],
                'civilregistry_id': mem["crn"],
                'LastName': mem["surname"],
                'last_name': mem["surname"],
                'FirstName': mem["given"],
                'first_name': mem["given"],
                'FullName': mem["full_name"],
                'full_name': mem["full_name"],
                'Sex': mem["sex"],
                'sex': mem["sex"],
                'DateOfBirth': mem["dob"],
                'date_of_birth': mem["dob"],
                'Age': mem["age"],
                'age': mem["age"],
                'MaritalStatus': mem["marital"],
                'marital_status': mem["marital"],
                'Address': REGIDOR_HOUSEHOLD["address"],
                'address': REGIDOR_HOUSEHOLD["address"],
                'IsPwd': 0,
                'is_pwd': 0,
                'IsSenior': mem["is_senior"],
                'is_senior': mem["is_senior"],
                'VerificationStatus': 1,
                'verification_status': 1,
                'LinkedHouseholdId': hh_id,
                'linked_household_id': hh_id,
                'LinkedHouseholdMemberId': member_id,
                'linked_household_member_id': member_id,
                'ImportedAt': NOW,
                'imported_at': NOW
            }
            use = {k: v for k, v in vals.items() if k in staging_cols}
            cols = ','.join(f'`{c}`' for c in use)
            ph = ','.join(['%s'] * len(use))
            cur.execute(f"INSERT INTO `{staging_tbl}` ({cols}) VALUES ({ph})", list(use.values()))
            staging_id = cur.lastrowid

        # Digital ID
        if dig_tbl in all_tables:
            cur.execute(f"SELECT id FROM `{dig_tbl}` WHERE beneficiary_staging_id=%s", (staging_id,))
            d = cur.fetchone()
            if not d:
                cur.execute(f"SHOW COLUMNS FROM `{dig_tbl}`")
                dig_cols = [r[0] for r in cur.fetchall()]
                qr = f"ASMBID{staging_id:06d}{secrets.token_hex(8).upper()}"
                vals = {
                    'SyncId': str(uuid.uuid4()),
                    'UpdatedAt': NOW,
                    'beneficiary_staging_id': staging_id,
                    'household_id': hh_id,
                    'household_member_id': member_id,
                    'card_number': f"BID-{staging_id:06d}",
                    'qr_payload': qr,
                    'issued_by_user_id': issued_by,
                    'issued_at': NOW,
                    'is_active': 1
                }
                use = {k: v for k, v in vals.items() if k in dig_cols}
                cols = ','.join(f'`{c}`' for c in use)
                ph = ','.join(['%s'] * len(use))
                cur.execute(f"INSERT INTO `{dig_tbl}` ({cols}) VALUES ({ph})", list(use.values()))

        # Val Beneficiaries
        if val_tbl in all_tables:
            cur.execute(f"SELECT COUNT(*) FROM `{val_tbl}` WHERE residents_id=%s", (mem["res_id"],))
            if not cur.fetchone()[0]:
                cur.execute(f"""INSERT INTO `{val_tbl}` (residents_id, beneficiary_id, civilregistry_id,
                                last_name, first_name, full_name, sex, date_of_birth, age, marital_status,
                                address, is_pwd, is_senior, created_at, updated_at)
                                VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,0,%s,NOW(),NOW())""",
                             (mem["res_id"], mem["ben_id"], mem["crn"], mem["surname"], mem["given"],
                              mem["full_name"], mem["sex"], mem["dob"], mem["age"], mem["marital"],
                              REGIDOR_HOUSEHOLD["address"], mem["is_senior"]))

    conn.commit()
    conn.close()
    print(f"MySQL ({label}) updated with Regidor household successfully.")

if __name__ == '__main__':
    import os
    appdata_db = os.path.expandvars(r'%LOCALAPPDATA%\eKalingaPlus\ams.db')
    seed_sqlite(appdata_db)
    seed_sqlite('ams.db')
    seed_sqlite(r'bin\Debug\net9.0-windows\ams.db')
    seed_sqlite('ayudasys.db')
    seed_sqlite(r'bin\Debug\net9.0-windows\ayudasys.db')

    seed_mysql('Local MySQL', user='root', password='codenameHylux122818', host='127.0.0.1', database='attendance_shifting_db')

    try:
        seed_mysql('Hostinger Remote MySQL', user='u621755393_ams_user', password='Ams@2026', host='194.59.164.58', database='u621755393_ams', connection_timeout=15)
    except Exception as ex:
        print("Remote MySQL not reached:", ex)

    print("\n--- Seeding Completed ---")
