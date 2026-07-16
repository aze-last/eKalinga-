"""One-time full data reset (authorized by developer 2026-07-15).
Clears all app data but preserves: users, user_profiles, user_permissions,
system_registrations, and EF migrations history. Prints row counts as it goes.
"""
import sqlite3

KEEP_SQLITE = {'users', 'user_profiles', 'user_permissions', 'system_registrations',
               'sqlite_sequence'}
KEEP_MYSQL = {'users', 'user_profiles', 'user_permissions', 'system_registrations',
              '__efmigrationshistory'}

def clear_sqlite(path):
    print(f'\n=== SQLite: {path} ===')
    con = sqlite3.connect(path)
    cur = con.cursor()
    tables = [r[0] for r in cur.execute(
        "SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
    for t in tables:
        if t in KEEP_SQLITE:
            continue
        n = cur.execute(f'SELECT COUNT(*) FROM "{t}"').fetchone()[0]
        cur.execute(f'DELETE FROM "{t}"')
        cur.execute("DELETE FROM sqlite_sequence WHERE name=?", (t,))
        print(f'  cleared {t}: {n} rows')
    kept = [t for t in tables if t in KEEP_SQLITE and t != 'sqlite_sequence']
    for t in kept:
        n = cur.execute(f'SELECT COUNT(*) FROM "{t}"').fetchone()[0]
        print(f'  KEPT {t}: {n} rows')
    con.commit()
    cur.execute('VACUUM')
    con.close()

def clear_mysql(label, **conn_args):
    import mysql.connector
    print(f'\n=== MySQL: {label} ({conn_args["host"]}/{conn_args["database"]}) ===')
    conn = mysql.connector.connect(**conn_args)
    cur = conn.cursor()
    cur.execute('SHOW TABLES')
    tables = [r[0] for r in cur.fetchall()]
    cur.execute('SET FOREIGN_KEY_CHECKS=0')
    for t in tables:
        if t.lower() in KEEP_MYSQL:
            cur.execute(f'SELECT COUNT(*) FROM `{t}`')
            print(f'  KEPT {t}: {cur.fetchone()[0]} rows')
            continue
        cur.execute(f'SELECT COUNT(*) FROM `{t}`')
        n = cur.fetchone()[0]
        cur.execute(f'TRUNCATE TABLE `{t}`')
        print(f'  cleared {t}: {n} rows')
    cur.execute('SET FOREIGN_KEY_CHECKS=1')
    conn.commit()
    conn.close()

clear_sqlite(r'bin\Debug\net9.0-windows\ams.db')
clear_sqlite('ams.db')

clear_mysql('Local', user='root', password='codenameHylux122818',
            host='127.0.0.1', database='attendance_shifting_db')

try:
    clear_mysql('Hostinger Remote', user='u621755393_ams_user', password='Ams@2026',
                host='194.59.164.58', database='u621755393_ams',
                connection_timeout=30)
except Exception as e:
    print(f'\nRemote FAILED: {e}')

print('\nDone.')
