import mysql.connector
import pprint
import traceback

try:
    conn = mysql.connector.connect(
        host="194.59.164.58",
        user="u621755393_ams_user",
        password="Ams@2026",
        database="u621755393_ams",
        connection_timeout=5
    )
    cursor = conn.cursor()

    cursor.execute("SHOW TABLES")
    tables = [r[0] for r in cursor.fetchall()]

    relevant_columns = ['is_active', 'isactive', 'is_deleted', 'isdeleted', 'status']

    table_details = {}
    for table in tables:
        cursor.execute(f"DESCRIBE {table}")
        cols = [r[0].lower() for r in cursor.fetchall()]
        match_cols = [c for c in cols if c in relevant_columns]
        if match_cols:
            cursor.execute(f"SELECT COUNT(*) FROM {table}")
            total = cursor.fetchone()[0]
            
            counts = {}
            for c in match_cols:
                cursor.execute(f"SELECT {c}, COUNT(*) FROM {table} GROUP BY {c}")
                counts[c] = cursor.fetchall()
            
            table_details[table] = {'total': total, 'cols': counts}

    print("--- REMOTE MYSQL ---")
    pprint.pprint(table_details)
    conn.close()
except Exception as e:
    traceback.print_exc()
