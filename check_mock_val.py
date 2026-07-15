import mysql.connector

conn = mysql.connector.connect(
    user='root',
    password='codenameHylux122818',
    host='127.0.0.1',
    database='attendance_shifting_db'
)
cur = conn.cursor()
cur.execute("SELECT residents_id, last_name, first_name FROM val_beneficiaries WHERE LOWER(last_name) IN ('mockson', 'testa', 'sample', 'demo', 'regidor')")
rows = cur.fetchall()
print("Matching records in MySQL val_beneficiaries:")
for r in rows:
    print(r)
conn.close()
