import sqlite3

db_path = 'bin/Debug/net9.0-windows/ams.db'

def query_db():
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    cursor.execute("PRAGMA table_info(scanner_sessions)")
    print("Columns:", [c[1] for c in cursor.fetchall()])

    cursor.execute("SELECT * FROM scanner_sessions")
    rows = cursor.fetchall()
    print("Scanner Sessions:")
    for r in rows:
        print(r)

    conn.close()

if __name__ == '__main__':
    query_db()
