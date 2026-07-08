import sqlite3

def dump_schema(db_path, table_name):
    print(f"Schema for {table_name} in {db_path}:")
    try:
        conn = sqlite3.connect(db_path)
        c = conn.cursor()
        c.execute(f"PRAGMA table_info({table_name});")
        columns = c.fetchall()
        for col in columns:
            print(col)
        conn.close()
    except Exception as e:
        print(e)

dump_schema("ams.db", "private_donations")
