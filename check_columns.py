import sqlite3
import mysql.connector

def check_sqlite():
    db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    
    tables = ['users', 'ayuda_programs']
    print("--- SQLITE AMS.DB ---")
    for t in tables:
        cur.execute(f"PRAGMA table_info({t})")
        cols = [r[1].lower() for r in cur.fetchall()]
        has_sync = 'sync_id' in cols or 'syncid' in cols
        has_updated = 'updated_at' in cols or 'updatedat' in cols
        print(f"{t}: SyncId={has_sync}, UpdatedAt={has_updated}")
    conn.close()

def check_remote():
    try:
        conn = mysql.connector.connect(
            host="194.59.164.58",
            user="u621755393_ams_user",
            password="Ams@2026",
            database="u621755393_ams",
            connection_timeout=5
        )
        cur = conn.cursor()
        tables = ['users', 'ayuda_programs']
        print("\n--- REMOTE MYSQL ---")
        for t in tables:
            cur.execute(f"DESCRIBE {t}")
            cols = [r[0].lower() for r in cur.fetchall()]
            has_sync = 'sync_id' in cols or 'syncid' in cols
            has_updated = 'updated_at' in cols or 'updatedat' in cols
            print(f"{t}: SyncId={has_sync}, UpdatedAt={has_updated}")
        conn.close()
    except Exception as e:
        print("Remote error:", e)

check_sqlite()
check_remote()
