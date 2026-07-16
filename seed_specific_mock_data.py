import sqlite3, uuid, secrets
from datetime import datetime
import mysql.connector

NOW = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

# Exact data from mock-beneficiaries-added.txt
BENEFICIARIES = [
    # Household 1
    ("HH-0001", "Mockson", "Aaron Luis", "Father"),
    ("HH-0001", "Mockson", "Ethan Cole", "Son"),
    ("HH-0001", "Mockson", "Quincy Ray", "Son"),
    ("HH-0001", "HH-0001", "Mockson, Marco Dean", "Uncle"), # name parsing fallback
    # Household 2
    ("HH-0002", "Testa", "Bianca Mae", "Mother"),
    ("HH-0002", "Testa", "Faith Anne", "Daughter"),
    ("HH-0002", "Testa", "Jasmine Lee", "Daughter"),
    ("HH-0002", "Testa", "Nina Grace", "Grandmother"),
    ("HH-0002", "Testa", "Rhea Sol", "Aunt"),
    # Household 3
    ("HH-0003", "Sample", "Carlo Rey", "Father"),
    ("HH-0003", "Sample", "Gabriel Nilo", "Son"),
    ("HH-0003", "Sample", "Kevin Noel", "Son"),
    ("HH-0003", "Sample", "Oscar Jude", "Grandfather"),
    ("HH-0003", "Sample", "Simon Troy", "Cousin"),
    # Household 4
    ("HH-0004", "Demo", "Diana Joy", "Mother"),
    ("HH-0004", "Demo", "Hazel Rose", "Daughter"),
    ("HH-0004", "Demo", "Lara Kim", "Daughter"),
    ("HH-0004", "Demo", "Paula Ivy", "Aunt"),
    ("HH-0004", "Demo", "Tessa May", "Cousin"),
    ("HH-0004", "Demo", "Xyra Jane", "Niece"),
    # Household 5
    ("HH-0005", "Regidor", "Bienvinido M.", "Father"),
    ("HH-0005", "Regidor", "Maria Jocelyn G.", "Mother"),
    ("HH-0005", "Regidor", "Bien Josef G.", "Son"),
    # Household 6
    ("HH-0006", "Testa", "Vina Claire", "Mother"),
    ("HH-0006", "Testa", "Warren Kyle", "Son"),
    ("HH-0006", "Testa", "Ulrich Jace", "Father"),
]

# Standardize demographic data
def get_demographics(surname, given, relationship):
    # Determine gender
    if relationship in ["Mother", "Daughter", "Grandmother", "Aunt", "Niece"] or "Maria" in given:
        sex = "Female"
    else:
        sex = "Male"

    # Age and Senior status
    age = "30"
    is_senior = 0
    dob = "1996-01-01"

    if relationship in ["Grandmother", "Grandfather"] or "Bienvinido" in given or "Maria" in given:
        age = "65"
        is_senior = 1
        dob = "1961-01-01"
    elif relationship in ["Father", "Mother", "Aunt", "Uncle"]:
        age = "45"
        dob = "1981-01-01"

    return sex, age, is_senior, dob

def seed_sqlite(db_path):
    print(f"\n=== Seeding SQLite: {db_path} ===")
    con = sqlite3.connect(db_path)
    cur = con.cursor()

    admin = cur.execute("SELECT Id FROM users ORDER BY Id LIMIT 1").fetchone()
    issued_by = admin[0] if admin else 1

    # Insert Households first
    hh_map = {}
    households_to_create = set(b[0] for b in BENEFICIARIES)
    for code in sorted(households_to_create):
        # find head of household
        members = [b for b in BENEFICIARIES if b[0] == code]
        head_member = next((m for m in members if m[3] in ["Father", "Mother", "Head"]), members[0])
        head_name = head_member[2] if "Mockson," not in head_member[2] else head_member[2].split(",")[1].strip()
        head_surname = head_member[1] if head_member[1] != code else head_member[2].split(",")[0].strip()
        head_fullname = f"{head_name} {head_surname}"

        r = cur.execute("SELECT id FROM households WHERE household_code=?", (code,)).fetchone()
        if r:
            hh_map[code] = r[0]
        else:
            cur.execute("""INSERT INTO households (SyncId, household_code, head_name, address_line,
                           purok, contact_number, status, created_at, updated_at)
                           VALUES (?,?,?,?,?,?,?,?,?)""",
                        (str(uuid.uuid4()), code, head_fullname, f"{head_surname} Residence, Purok {code[-1]}",
                         f"Purok {code[-1]}", "0900-000-0000", "Active", NOW, NOW))
            hh_map[code] = cur.lastrowid

    # Insert Members, Staging, Digital IDs
    for idx, (code, surname, given, relationship) in enumerate(BENEFICIARIES, 1):
        hh_id = hh_map[code]
        
        # Clean up names
        if "Mockson," in given:
            parts = given.split(",")
            s_name = parts[0].strip()
            g_name = parts[1].strip()
        else:
            s_name = surname
            g_name = given

        full_name = f"{g_name} {s_name}"
        sex, age, is_senior, dob = get_demographics(s_name, g_name, relationship)

        # 1. Household Member
        m = cur.execute("SELECT id FROM household_members WHERE household_id=? AND full_name=?", (hh_id, full_name)).fetchone()
        if m:
            member_id = m[0]
        else:
            cur.execute("""INSERT INTO household_members (SyncId, household_id, full_name,
                           relationship_to_head, occupation, is_cash_for_work_eligible, created_at, updated_at)
                           VALUES (?,?,?,?,?,?,?,?)""",
                        (str(uuid.uuid4()), hh_id, full_name, relationship, "N/A", 1, NOW, NOW))
            member_id = cur.lastrowid

        # 2. Beneficiary Staging (Approved status = 1)
        res_id = 950000 + idx
        ben_id = f"BEN-{idx:04d}"
        crn = f"CRN-{idx:04d}"
        address = f"{s_name} Residence, Purok {code[-1]}"

        s = cur.execute("SELECT StagingID FROM BeneficiaryStaging WHERE LOWER(LastName)=? AND LOWER(FirstName)=?",
                        (s_name.lower(), g_name.lower())).fetchone()
        if s:
            staging_id = s[0]
        else:
            cur.execute("""INSERT INTO BeneficiaryStaging (SyncId, UpdatedAt, ResidentsId, BeneficiaryId,
                           CivilRegistryId, LastName, FirstName, FullName, Sex, DateOfBirth, Age,
                           MaritalStatus, Address, IsPwd, IsSenior, VerificationStatus,
                           LinkedHouseholdId, LinkedHouseholdMemberId, ImportedAt)
                           VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                        (str(uuid.uuid4()), NOW, res_id, ben_id, crn, s_name, g_name, full_name,
                         sex, dob, age, "Married" if relationship in ["Father", "Mother"] else "Single",
                         address, 0, is_senior, 1, hh_id, member_id, NOW))
            staging_id = cur.lastrowid

        # 3. Digital ID
        d = cur.execute("SELECT id FROM beneficiary_digital_ids WHERE beneficiary_staging_id=?", (staging_id,)).fetchone()
        if not d:
            qr = f"ASMBID{staging_id:06d}{secrets.token_hex(8).upper()}"
            dcols = [c[1] for c in cur.execute("PRAGMA table_info(beneficiary_digital_ids)").fetchall()]
            vals = {'SyncId': str(uuid.uuid4()), 'UpdatedAt': NOW, 'beneficiary_staging_id': staging_id,
                    'household_id': hh_id, 'household_member_id': member_id,
                    'card_number': f"BID-{staging_id:06d}", 'qr_payload': qr,
                    'issued_by_user_id': issued_by, 'issued_at': NOW, 'is_active': 1}
            use = {k: v for k, v in vals.items() if k in dcols}
            cur.execute(f"INSERT INTO beneficiary_digital_ids ({','.join(use)}) VALUES ({','.join('?'*len(use))})",
                        list(use.values()))

    con.commit()
    print('SQLite seeded successfully.')
    con.close()

def seed_mysql(label, **conn_args):
    print(f"\n=== Seeding MySQL: {label} ===")
    conn = mysql.connector.connect(**conn_args)
    cur = conn.cursor()

    # Get exact casing of tables
    cur.execute("SHOW TABLES")
    all_tables = [r[0] for r in cur.fetchall()]
    table_map = {t.lower(): t for t in all_tables}

    # Fetch admin user
    user_tbl = table_map.get('users', 'users')
    cur.execute(f"SELECT Id FROM `{user_tbl}` ORDER BY Id LIMIT 1")
    admin = cur.fetchone()
    issued_by = admin[0] if admin else 1

    hh_tbl = table_map.get('households', 'households')
    member_tbl = table_map.get('household_members', 'household_members')
    staging_tbl = table_map.get('beneficiarystaging', 'BeneficiaryStaging')
    dig_tbl = table_map.get('beneficiary_digital_ids', 'beneficiary_digital_ids')
    val_tbl = table_map.get('val_beneficiaries', 'val_beneficiaries')

    # Get column lists
    cur.execute(f"SHOW COLUMNS FROM `{hh_tbl}`")
    hh_cols = [r[0] for r in cur.fetchall()]

    cur.execute(f"SHOW COLUMNS FROM `{member_tbl}`")
    member_cols = [r[0] for r in cur.fetchall()]

    cur.execute(f"SHOW COLUMNS FROM `{staging_tbl}`")
    staging_cols = [r[0] for r in cur.fetchall()]

    # Insert Households
    hh_map = {}
    households_to_create = set(b[0] for b in BENEFICIARIES)
    for code in sorted(households_to_create):
        members = [b for b in BENEFICIARIES if b[0] == code]
        head_member = next((m for m in members if m[3] in ["Father", "Mother", "Head"]), members[0])
        head_name = head_member[2] if "Mockson," not in head_member[2] else head_member[2].split(",")[1].strip()
        head_surname = head_member[1] if head_member[1] != code else head_member[2].split(",")[0].strip()
        head_fullname = f"{head_name} {head_surname}"

        cur.execute(f"SELECT id FROM `{hh_tbl}` WHERE household_code=%s", (code,))
        r = cur.fetchone()
        if r:
            hh_map[code] = r[0]
        else:
            vals = {
                'SyncId': str(uuid.uuid4()),
                'household_code': code,
                'head_name': head_fullname,
                'address_line': f"{head_surname} Residence, Purok {code[-1]}",
                'purok': f"Purok {code[-1]}",
                'contact_number': "0900-000-0000",
                'status': "Active",
                'created_at': NOW,
                'updated_at': NOW
            }
            use = {k: v for k, v in vals.items() if k in hh_cols}
            cols = ','.join(f'`{c}`' for c in use)
            ph = ','.join(['%s'] * len(use))
            cur.execute(f"INSERT INTO `{hh_tbl}` ({cols}) VALUES ({ph})", list(use.values()))
            hh_map[code] = cur.lastrowid

    # Insert Members, Staging, Digital IDs
    for idx, (code, surname, given, relationship) in enumerate(BENEFICIARIES, 1):
        hh_id = hh_map[code]
        
        # Clean up names
        if "Mockson," in given:
            parts = given.split(",")
            s_name = parts[0].strip()
            g_name = parts[1].strip()
        else:
            s_name = surname
            g_name = given

        full_name = f"{g_name} {s_name}"
        sex, age, is_senior, dob = get_demographics(s_name, g_name, relationship)
        marital = "Married" if relationship in ["Father", "Mother"] else "Single"

        # 1. Household Member
        cur.execute(f"SELECT id FROM `{member_tbl}` WHERE household_id=%s AND full_name=%s", (hh_id, full_name))
        m = cur.fetchone()
        if m:
            member_id = m[0]
        else:
            vals = {
                'SyncId': str(uuid.uuid4()),
                'household_id': hh_id,
                'full_name': full_name,
                'relationship_to_head': relationship,
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

        # 2. Beneficiary Staging (Approved status = 1)
        res_id = 950000 + idx
        ben_id = f"BEN-{idx:04d}"
        crn = f"CRN-{idx:04d}"
        address = f"{s_name} Residence, Purok {code[-1]}"

        # SQLite columns are slightly different, so map to staging columns dynamically
        cur.execute(f"SELECT StagingID FROM `{staging_tbl}` WHERE LastName=%s AND FirstName=%s", (s_name, g_name))
        s = cur.fetchone()
        if s:
            staging_id = s[0]
        else:
            # We map database staging columns based on table schema
            vals = {
                'SyncId': str(uuid.uuid4()),
                'UpdatedAt': NOW,
                'ResidentsId': res_id,
                'residents_id': res_id, # casing fallback
                'BeneficiaryId': ben_id,
                'beneficiary_id': ben_id,
                'CivilRegistryId': crn,
                'civilregistry_id': crn,
                'LastName': s_name,
                'last_name': s_name,
                'FirstName': g_name,
                'first_name': g_name,
                'FullName': full_name,
                'full_name': full_name,
                'Sex': sex,
                'sex': sex,
                'DateOfBirth': dob,
                'date_of_birth': dob,
                'Age': age,
                'age': age,
                'MaritalStatus': marital,
                'marital_status': marital,
                'Address': address,
                'address': address,
                'IsPwd': 0,
                'is_pwd': 0,
                'IsSenior': is_senior,
                'is_senior': is_senior,
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

        # 3. Digital ID if the table exists
        if dig_tbl in all_tables:
            cur.execute(f"SELECT id FROM `{dig_tbl}` WHERE beneficiary_staging_id=%s", (staging_id,))
            d = cur.fetchone()
            if not d:
                cur.execute(f"SHOW COLUMNS FROM `{dig_tbl}`")
                dig_cols = [r[0] for r in cur.fetchall()]
                qr = f"ASMBID{staging_id:06d}{secrets.token_hex(8).upper()}"
                vals = {'SyncId': str(uuid.uuid4()), 'UpdatedAt': NOW, 'beneficiary_staging_id': staging_id,
                        'household_id': hh_id, 'household_member_id': member_id,
                        'card_number': f"BID-{staging_id:06d}", 'qr_payload': qr,
                        'issued_by_user_id': issued_by, 'issued_at': NOW, 'is_active': 1}
                use = {k: v for k, v in vals.items() if k in dig_cols}
                cols = ','.join(f'`{c}`' for c in use)
                ph = ','.join(['%s'] * len(use))
                cur.execute(f"INSERT INTO `{dig_tbl}` ({cols}) VALUES ({ph})", list(use.values()))

        # 4. val_beneficiaries (if present)
        if val_tbl in all_tables:
            cur.execute(f"SELECT COUNT(*) FROM `{val_tbl}` WHERE residents_id=%s", (res_id,))
            if not cur.fetchone()[0]:
                cur.execute(f"""INSERT INTO `{val_tbl}` (residents_id, beneficiary_id, civilregistry_id,
                                last_name, first_name, full_name, sex, date_of_birth, age, marital_status,
                                address, is_pwd, is_senior, created_at, updated_at)
                                VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,0,%s,NOW(),NOW())""",
                             (res_id, ben_id, crn, s_name, g_name, full_name, sex, dob, age, marital, address, is_senior))

    conn.commit()
    conn.close()
    print("MySQL seeded successfully.")

# Seed all 4 databases
seed_sqlite('ams.db')
seed_sqlite(r'bin\Debug\net9.0-windows\ams.db')

seed_mysql('Local MySQL', user='root', password='codenameHylux122818',
           host='127.0.0.1', database='attendance_shifting_db')

try:
    seed_mysql('Hostinger Remote MySQL', user='u621755393_ams_user', password='Ams@2026',
               host='194.59.164.58', database='u621755393_ams', connection_timeout=30)
except Exception as e:
    print("Hostinger Remote MySQL seeding failed:", e)

print("\nDone seeding all databases.")
