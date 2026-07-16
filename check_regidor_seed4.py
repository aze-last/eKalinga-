import sqlite3

con = sqlite3.connect(r'bin\Debug\net9.0-windows\ams.db')
cur = con.cursor()

print('--- staging rows 26-52 (new since seeder update) ---')
for r in cur.execute("""SELECT StagingID, FirstName, LastName, ResidentsId, BeneficiaryId,
                        VerificationStatus, LinkedHouseholdId, LinkedHouseholdMemberId
                        FROM BeneficiaryStaging WHERE StagingID >= 26 ORDER BY StagingID"""):
    print(r)

print('--- all household_members for household 5 ---')
for r in cur.execute("SELECT id, full_name, relationship_to_head, created_at FROM household_members WHERE household_id=5"):
    print(r)

print('--- households ---')
for r in cur.execute("SELECT id, household_code, head_name FROM households"):
    print(r)
