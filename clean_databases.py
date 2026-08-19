import sqlite3
import os

def should_keep_table(table_name: str) -> bool:
    t = table_name.lower().strip()
    # 1. val_beneficiaries and beneficiary staging
    if t == 'val_beneficiaries' or t == 'beneficiarystaging':
        return True
    # 2. all tables containing 'crs' in their name
    if 'crs' in t:
        return True
    # 3. users and user authentication / profile tables
    if t in ('users', 'user_profiles', 'user_permissions', 'user_preferences', 'system_registrations'):
        return True
    # 4. system / EF migration metadata
    if t in ('sqlite_sequence', '__efmigrationshistory'):
        return True
    return False

def clean_sqlite(path: str):
    if not os.path.exists(path):
        return
    print(f"\n=======================================================")
    print(f" SQLite: {path}")
    print(f"=======================================================")
    con = sqlite3.connect(path)
    cur = con.cursor()
    
    tables = [r[0] for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
    cleared = []
    kept = []
    
    # Disable foreign keys during cleanup
    cur.execute("PRAGMA foreign_keys = OFF;")
    
    for t in tables:
        if should_keep_table(t):
            if t != 'sqlite_sequence':
                try:
                    cnt = cur.execute(f'SELECT COUNT(*) FROM "{t}"').fetchone()[0]
                    kept.append((t, cnt))
                except Exception as ex:
                    kept.append((t, f"err: {ex}"))
            continue
        
        try:
            cnt = cur.execute(f'SELECT COUNT(*) FROM "{t}"').fetchone()[0]
            cur.execute(f'DELETE FROM "{t}"')
            try:
                cur.execute("DELETE FROM sqlite_sequence WHERE name=?", (t,))
            except Exception:
                pass
            cleared.append((t, cnt))
        except Exception as ex:
            print(f"  [ERROR] Clearing {t}: {ex}")
            
    con.commit()
    cur.execute("PRAGMA foreign_keys = ON;")
    cur.execute("VACUUM;")
    con.close()
    
    print("\n--- KEPT TABLES ---")
    for t, cnt in kept:
        print(f"  [KEPT]    {t:35} : {cnt} rows")
        
    print("\n--- CLEARED TABLES ---")
    for t, cnt in cleared:
        print(f"  [CLEARED] {t:35} : {cnt} rows removed")

def clean_mysql(label: str, **conn_args):
    try:
        import mysql.connector
    except ImportError:
        print("mysql-connector not installed!")
        return

    print(f"\n=======================================================")
    print(f" MySQL: {label} ({conn_args.get('host')}/{conn_args.get('database')})")
    print(f"=======================================================")
    
    try:
        conn = mysql.connector.connect(**conn_args)
        cur = conn.cursor()
        cur.execute("SHOW TABLES")
        tables = [r[0] for r in cur.fetchall()]
        
        cur.execute("SET FOREIGN_KEY_CHECKS = 0")
        
        kept = []
        cleared = []
        
        for t in tables:
            if should_keep_table(t):
                cur.execute(f"SELECT COUNT(*) FROM `{t}`")
                cnt = cur.fetchone()[0]
                kept.append((t, cnt))
                continue
            
            cur.execute(f"SELECT COUNT(*) FROM `{t}`")
            cnt = cur.fetchone()[0]
            cur.execute(f"TRUNCATE TABLE `{t}`")
            cleared.append((t, cnt))
            
        cur.execute("SET FOREIGN_KEY_CHECKS = 1")
        conn.commit()
        conn.close()
        
        print("\n--- KEPT TABLES ---")
        for t, cnt in kept:
            print(f"  [KEPT]    {t:35} : {cnt} rows")
            
        print("\n--- CLEARED TABLES ---")
        for t, cnt in cleared:
            print(f"  [CLEARED] {t:35} : {cnt} rows removed")
            
    except Exception as e:
        print(f"MySQL ({label}) Error: {e}")

if __name__ == '__main__':
    # 1. SQLite AppData (Primary App DB)
    appdata_db = os.path.expandvars(r'%LOCALAPPDATA%\eKalingaPlus\ams.db')
    clean_sqlite(appdata_db)
    
    # 2. SQLite build output & root
    clean_sqlite(r'bin\Debug\net9.0-windows\ams.db')
    clean_sqlite('ams.db')
    
    # 3. Hostinger Remote MySQL
    clean_mysql(
        'Hostinger Remote AMS',
        user='u621755393_ams_user',
        password='Ams@2026',
        host='194.59.164.58',
        database='u621755393_ams',
        connection_timeout=30
    )
    
    # 4. Local MySQL (if running)
    try:
        clean_mysql(
            'Local MySQL',
            user='root',
            password='codenameHylux122818',
            host='127.0.0.1',
            database='attendance_shifting_db',
            connection_timeout=5
        )
    except Exception:
        pass

    print("\nDatabase cleanup process completed successfully.")
