import sqlite3

con = sqlite3.connect('ams.db')
cur = con.cursor()

print('member cols:', [c[1] for c in cur.execute('PRAGMA table_info(household_members)').fetchall()])
print('--- members of household 5 (Regidor) ---')
for r in cur.execute('SELECT * FROM household_members WHERE household_id=5').fetchall():
    print(r)

print('--- staging rows named like Regidor members ---')
for r in cur.execute("""SELECT StagingID, FirstName, LastName, ResidentsId, BeneficiaryId,
                        LinkedHouseholdId, LinkedHouseholdMemberId
                        FROM BeneficiaryStaging
                        WHERE LastName LIKE '%egidor%' OR FirstName LIKE 'Bien%' OR FirstName LIKE 'Maria Jocelyn%'""").fetchall():
    print(r)
