import sqlite3

con = sqlite3.connect('ams.db')
cur = con.cursor()

print('--- households table schema ---')
print([c[1] for c in cur.execute('PRAGMA table_info(households)').fetchall()])

print('--- mock households (SQLite ams.db) ---')
for r in cur.execute("SELECT id, household_code, head_name FROM households WHERE household_code LIKE 'MOCK-%'"):
    print(r)

print('--- members per mock household ---')
for r in cur.execute("""SELECT h.household_code, m.id, m.full_name, m.relationship_to_head
                        FROM households h JOIN household_members m ON m.household_id=h.id
                        WHERE h.household_code LIKE 'MOCK-%' ORDER BY h.id, m.id"""):
    print(r)

print('--- staging rows linked to mock households ---')
for r in cur.execute("""SELECT s.StagingID, s.FirstName, s.LastName, s.BeneficiaryId, s.VerificationStatus, s.LinkedHouseholdId, s.LinkedHouseholdMemberId
                        FROM BeneficiaryStaging s
                        WHERE s.LinkedHouseholdId IN (SELECT id FROM households WHERE household_code LIKE 'MOCK-%')
                        ORDER BY s.StagingID"""):
    print(r)

print('--- total staging rows ---')
print(cur.execute("SELECT COUNT(*) FROM BeneficiaryStaging").fetchone())

# Now MySQL side (what the Masterlist actually displays)
try:
    import mysql.connector
    conn = mysql.connector.connect(user='root', password='codenameHylux122818',
                                   host='127.0.0.1', database='attendance_shifting_db')
    mcur = conn.cursor()
    print('--- MySQL val_beneficiaries: mock surnames ---')
    mcur.execute("""SELECT residents_id, beneficiary_id, first_name, last_name
                    FROM val_beneficiaries
                    WHERE LOWER(last_name) IN ('mockson','testa','sample','demo','regidor')
                    ORDER BY last_name, first_name""")
    for r in mcur.fetchall():
        print(r)
    mcur.execute("SELECT COUNT(*) FROM val_beneficiaries")
    print('total val_beneficiaries:', mcur.fetchone())
    conn.close()
except Exception as e:
    print('MySQL check failed:', e)
