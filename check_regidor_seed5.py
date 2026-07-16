import sqlite3

con = sqlite3.connect(r'bin\Debug\net9.0-windows\ams.db')
cur = con.cursor()
cols = [c[1] for c in cur.execute('PRAGMA table_info(BeneficiaryStaging)').fetchall()]
print('staging cols:', cols)
datecol = [c for c in cols if 'reate' in c or 'Date' in c]
print('--- id, names, created ---')
for r in cur.execute("SELECT StagingID, FirstName, LastName, CreatedAt FROM BeneficiaryStaging ORDER BY StagingID"):
    print(r)
