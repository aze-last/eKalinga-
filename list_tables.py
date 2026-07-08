import sqlite3

def list_tables(db_path):
    print(f"Tables in {db_path}:")
    try:
        conn = sqlite3.connect(db_path)
        c = conn.cursor()
        c.execute("SELECT name FROM sqlite_master WHERE type='table';")
        tables = c.fetchall()
        for t in tables:
            print(t[0])
        conn.close()
    except Exception as e:
        print(e)

list_tables("ams.db")
list_tables("ayudasys.db")
