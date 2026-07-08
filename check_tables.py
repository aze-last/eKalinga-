import sqlite3

db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
conn = sqlite3.connect(db_path)
cursor = conn.cursor()

cursor.execute("SELECT name FROM sqlite_master WHERE type='table';")
tables = [r[0] for r in cursor.fetchall()]

relevant_columns = ['is_active', 'isactive', 'is_deleted', 'isdeleted', 'status']

table_details = {}
for table in tables:
    cursor.execute(f"PRAGMA table_info({table})")
    cols = [r[1].lower() for r in cursor.fetchall()]
    match_cols = [c for c in cols if c in relevant_columns]
    if match_cols:
        cursor.execute(f"SELECT COUNT(*) FROM {table}")
        total = cursor.fetchone()[0]
        
        counts = {}
        for c in match_cols:
            cursor.execute(f"SELECT {c}, COUNT(*) FROM {table} GROUP BY {c}")
            counts[c] = cursor.fetchall()
        
        table_details[table] = {'total': total, 'cols': counts}

import pprint
print("--- SQLITE AMS.DB ---")
pprint.pprint(table_details)
