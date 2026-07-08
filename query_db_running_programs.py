import sqlite3

db_path = 'bin/Debug/net9.0-windows/ams.db'

def query_db():
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    cursor.execute("SELECT id, program_name, distribution_status, is_active FROM ayuda_programs")
    rows = cursor.fetchall()
    print("Programs:")
    for r in rows:
        print(f"ID: {r[0]}, Name: {r[1]}, Status: {r[2]}, Active: {r[3]}")

    conn.close()

if __name__ == '__main__':
    query_db()
