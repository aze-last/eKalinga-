import sqlite3
import mysql.connector
import pprint

db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
conn_local = sqlite3.connect(db_path)
cur_local = conn_local.cursor()

cur_local.execute("SELECT id, username, is_active, is_deleted FROM users")
local_users = cur_local.fetchall()
print("Local users (ams.db):")
for u in local_users:
    print(u)

conn_remote = mysql.connector.connect(
    host="194.59.164.58",
    user="u621755393_ams_user",
    password="Ams@2026",
    database="u621755393_ams",
    connection_timeout=5
)
cur_remote = conn_remote.cursor()

print("\nRemote users matching local IDs:")
for u in local_users:
    uid = u[0]
    cur_remote.execute(f"SELECT id, username, is_active, is_deleted FROM users WHERE id = {uid}")
    remote_u = cur_remote.fetchone()
    print(remote_u)

conn_local.close()
conn_remote.close()
