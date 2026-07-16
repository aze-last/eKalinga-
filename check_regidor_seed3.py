import sqlite3, os

for path in [r'bin\Debug\net9.0-windows\ams.db', 'ams.db']:
    print('===', path, 'exists:', os.path.exists(path))
    if not os.path.exists(path):
        continue
    print('mtime:', os.path.getmtime(path), 'size:', os.path.getsize(path))
    con = sqlite3.connect(path)
    cur = con.cursor()
    try:
        print('HH-05 members:', cur.execute("SELECT m.id, m.full_name, m.relationship_to_head FROM households h JOIN household_members m ON m.household_id=h.id WHERE h.household_code='MOCK-HH-05'").fetchall())
        print('Regidor staging:', cur.execute("SELECT StagingID, FirstName, LastName FROM BeneficiaryStaging WHERE LOWER(LastName)='regidor'").fetchall())
        print('total staging:', cur.execute("SELECT COUNT(*) FROM BeneficiaryStaging").fetchone())
    except Exception as e:
        print('error:', e)
    con.close()
