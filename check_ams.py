import sqlite3

db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
try:
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    cursor.execute("SELECT id, program_name, is_active FROM ayuda_programs")
    rows = cursor.fetchall()
    print("ams.db ALL programs:")
    for r in rows:
        print(dict(r))
    conn.close()
except Exception as e:
    print("Error:", e)
