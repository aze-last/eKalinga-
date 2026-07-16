import mysql.connector
conn = mysql.connector.connect(user='root', password='codenameHylux122818',
                               host='127.0.0.1', database='attendance_shifting_db')
cur = conn.cursor()
cur.execute("SHOW COLUMNS FROM BeneficiaryStaging")
for r in cur.fetchall():
    print(r)
cur.execute("SELECT COUNT(*) FROM BeneficiaryStaging")
print('rows:', cur.fetchone())
conn.close()
