import sqlite3

def query_programs():
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()
    cursor.execute("SELECT id, title, distribution_status FROM ayuda_programs")
    rows = cursor.fetchall()
    print("Programs:")
    for r in rows:
        print(f"ID: {r[0]}, Title: {r[1]}, Status: {r[2]}")
    conn.close()

if __name__ == '__main__':
    query_programs()
