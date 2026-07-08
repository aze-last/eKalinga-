import sqlite3

db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
conn = sqlite3.connect(db_path)
cur = conn.cursor()

cur.execute("PRAGMA table_info(users)")
print("Columns in users:")
for r in cur.fetchall():
    print(r)

conn.close()
