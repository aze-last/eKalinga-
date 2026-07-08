import sqlite3

def query_schema():
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()
    cursor.execute("PRAGMA table_info(ayuda_programs)")
    print("ayuda_programs columns:", [c[1] for c in cursor.fetchall()])
    
    cursor.execute("SELECT * FROM ayuda_programs")
    rows = cursor.fetchall()
    print("ayuda_programs rows:")
    for r in rows:
        print(r)
    conn.close()

if __name__ == '__main__':
    query_schema()
