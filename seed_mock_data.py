"""Seed mock households + beneficiaries directly (replicates DevSeedService).
Writes: SQLite (households, household_members, BeneficiaryStaging, beneficiary_digital_ids)
and local MySQL val_beneficiaries so the Masterlist shows them.
Additive + idempotent: skips rows that already exist.
"""
import sqlite3, uuid, secrets
from datetime import datetime

NOW = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

FAMILIES = [
    ("Mockson", "MOCK-HH-01", ["Aaron Luis", "Ethan Cole", "Ian Paolo", "Marco Dean", "Quincy Ray", "Ulrich Jace"]),
    ("Testa",   "MOCK-HH-02", ["Bianca Mae", "Faith Anne", "Jasmine Lee", "Nina Grace", "Rhea Sol", "Vina Claire"]),
    ("Sample",  "MOCK-HH-03", ["Carlo Rey", "Gabriel Nilo", "Kevin Noel", "Oscar Jude", "Simon Troy", "Warren Kyle"]),
    ("Demo",    "MOCK-HH-04", ["Diana Joy", "Hazel Rose", "Lara Kim", "Paula Ivy", "Tessa May", "Xyra Jane"]),
    ("Regidor", "MOCK-HH-05", ["Bienvinido M.", "Maria Jocelyn G.", "Bien Josef G."]),
]
RELATIONSHIPS = ["Head", "Spouse", "Son", "Daughter", "Son", "Daughter"]

REGIDOR = {  # given-name prefix -> (sex, age, is_senior, dob)
    "Bienvinido":  ("Male", "66", 1, "1960-01-01"),
    "Maria":       ("Female", "62", 1, "1964-01-01"),
    "Bien Josef":  ("Male", "28", 0, "1998-01-01"),
}

def demographics(surname, given, idx):
    if surname == "Regidor":
        for prefix, d in REGIDOR.items():
            if given.startswith(prefix):
                return d
    return ("Male" if idx % 2 == 0 else "Female", "30", 0, "1996-01-01")

DB = r'bin\Debug\net9.0-windows\ams.db'
con = sqlite3.connect(DB)
cur = con.cursor()

admin = cur.execute("SELECT Id FROM users ORDER BY Id LIMIT 1").fetchone()
issued_by = admin[0] if admin else 1
print(f'issued_by_user_id = {issued_by}')

rows = []          # (global_index, surname, given, hh_code, relationship, sex, age, senior, dob, marital)
gi = 0
for surname, code, members in FAMILIES:
    for i, given in enumerate(members):
        gi += 1
        sex, age, senior, dob = demographics(surname, given, i)
        rows.append((gi, surname, given, code, RELATIONSHIPS[i % 6],
                     sex, age, senior, dob, "Single" if i == 2 else "Married"))

# --- SQLite: households ---
hh_ids = {}
for surname, code, members in FAMILIES:
    r = cur.execute("SELECT id FROM households WHERE household_code=?", (code,)).fetchone()
    if r:
        hh_ids[code] = r[0]
        continue
    cur.execute("""INSERT INTO households (SyncId, household_code, head_name, address_line,
                   purok, contact_number, status, created_at, updated_at)
                   VALUES (?,?,?,?,?,?,?,?,?)""",
                (str(uuid.uuid4()), code, f"{members[0]} {surname}",
                 f"{surname} Residence, Mock Purok", "Mock Purok", "0900-000-0000",
                 "Active", NOW, NOW))
    hh_ids[code] = cur.lastrowid
print('households:', hh_ids)

# --- SQLite: members, staging, digital ids / MySQL rows collected ---
mysql_rows = []
for (idx, surname, given, code, rel, sex, age, senior, dob, marital) in rows:
    hh = hh_ids[code]
    full = f"{given} {surname}"
    m = cur.execute("SELECT id FROM household_members WHERE household_id=? AND full_name=?", (hh, full)).fetchone()
    if m:
        member_id = m[0]
    else:
        cur.execute("""INSERT INTO household_members (SyncId, household_id, full_name,
                       relationship_to_head, occupation, is_cash_for_work_eligible, created_at, updated_at)
                       VALUES (?,?,?,?,?,?,?,?)""",
                    (str(uuid.uuid4()), hh, full, rel, "N/A", 1, NOW, NOW))
        member_id = cur.lastrowid

    res_id = 900000 + idx
    ben_id = f"MOCK-{idx:04d}"
    crn = f"MOCK-CRN-{idx:04d}"
    address = f"{surname} Residence, Mock Purok"

    s = cur.execute("SELECT StagingID FROM BeneficiaryStaging WHERE LOWER(LastName)=? AND LOWER(FirstName)=?",
                    (surname.lower(), given.lower())).fetchone()
    if s:
        staging_id = s[0]
    else:
        cur.execute("""INSERT INTO BeneficiaryStaging (SyncId, UpdatedAt, ResidentsId, BeneficiaryId,
                       CivilRegistryId, LastName, FirstName, FullName, Sex, DateOfBirth, Age,
                       MaritalStatus, Address, IsPwd, IsSenior, VerificationStatus,
                       LinkedHouseholdId, LinkedHouseholdMemberId, ImportedAt)
                       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                    (str(uuid.uuid4()), NOW, res_id, ben_id, crn, surname, given, full,
                     sex, dob, age, marital, address, 0, senior, 1, hh, member_id, NOW))
        staging_id = cur.lastrowid

    d = cur.execute("SELECT id FROM beneficiary_digital_ids WHERE beneficiary_staging_id=?", (staging_id,)).fetchone()
    if not d:
        qr = f"ASMBID{staging_id:06d}{secrets.token_hex(8).upper()}"
        dcols = [c[1] for c in cur.execute("PRAGMA table_info(beneficiary_digital_ids)").fetchall()]
        vals = {'SyncId': str(uuid.uuid4()), 'UpdatedAt': NOW, 'beneficiary_staging_id': staging_id,
                'household_id': hh, 'household_member_id': member_id,
                'card_number': f"BID-{staging_id:06d}", 'qr_payload': qr,
                'issued_by_user_id': issued_by, 'issued_at': NOW, 'is_active': 1}
        use = {k: v for k, v in vals.items() if k in dcols}
        cur.execute(f"INSERT INTO beneficiary_digital_ids ({','.join(use)}) VALUES ({','.join('?'*len(use))})",
                    list(use.values()))

    mysql_rows.append((res_id, ben_id, crn, surname, given, full, sex, dob, age, marital, address, senior))

con.commit()
print('SQLite seeded: households', cur.execute("SELECT COUNT(*) FROM households").fetchone()[0],
      '| members', cur.execute("SELECT COUNT(*) FROM household_members").fetchone()[0],
      '| staging', cur.execute("SELECT COUNT(*) FROM BeneficiaryStaging").fetchone()[0],
      '| digital_ids', cur.execute("SELECT COUNT(*) FROM beneficiary_digital_ids").fetchone()[0])
con.close()

# --- MySQL: val_beneficiaries ---
import mysql.connector
conn = mysql.connector.connect(user='root', password='codenameHylux122818',
                               host='127.0.0.1', database='attendance_shifting_db')
mcur = conn.cursor()
inserted = 0
for (res_id, ben_id, crn, surname, given, full, sex, dob, age, marital, address, senior) in mysql_rows:
    mcur.execute("SELECT COUNT(*) FROM val_beneficiaries WHERE residents_id=%s", (res_id,))
    if mcur.fetchone()[0]:
        continue
    mcur.execute("""INSERT INTO val_beneficiaries (residents_id, beneficiary_id, civilregistry_id,
                    last_name, first_name, full_name, sex, date_of_birth, age, marital_status,
                    address, is_pwd, is_senior, created_at, updated_at)
                    VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,0,%s,NOW(),NOW())""",
                 (res_id, ben_id, crn, surname, given, full, sex, dob, age, marital, address, senior))
    inserted += 1
conn.commit()
mcur.execute("SELECT COUNT(*) FROM val_beneficiaries")
print(f'MySQL val_beneficiaries: inserted {inserted}, total now {mcur.fetchone()[0]}')
conn.close()
print('Done.')
