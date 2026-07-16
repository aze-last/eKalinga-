"""Mirror SQLite BeneficiaryStaging rows into MySQL BeneficiaryStaging so the
Masterlist status join agrees with the local approval state."""
import sqlite3
import mysql.connector

con = sqlite3.connect(r'bin\Debug\net9.0-windows\ams.db')
con.row_factory = sqlite3.Row
rows = con.execute("SELECT * FROM BeneficiaryStaging ORDER BY StagingID").fetchall()
con.close()

conn = mysql.connector.connect(user='root', password='codenameHylux122818',
                               host='127.0.0.1', database='attendance_shifting_db')
mcur = conn.cursor()
mcur.execute("SHOW COLUMNS FROM BeneficiaryStaging")
mysql_cols = [r[0] for r in mcur.fetchall()]

inserted = 0
for row in rows:
    d = dict(row)
    mcur.execute("SELECT COUNT(*) FROM BeneficiaryStaging WHERE ResidentsId=%s", (d['ResidentsId'],))
    if mcur.fetchone()[0]:
        continue
    use = {k: v for k, v in d.items() if k in mysql_cols}
    cols = ','.join(f'`{c}`' for c in use)
    ph = ','.join(['%s'] * len(use))
    mcur.execute(f"INSERT INTO BeneficiaryStaging ({cols}) VALUES ({ph})", list(use.values()))
    inserted += 1

conn.commit()
mcur.execute("SELECT COUNT(*), SUM(VerificationStatus=1) FROM BeneficiaryStaging")
total, approved = mcur.fetchone()
print(f'inserted {inserted}; MySQL staging now {total} rows, {approved} approved')
conn.close()
