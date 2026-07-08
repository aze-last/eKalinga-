import sqlite3
import uuid
import datetime

db_path = r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db'
conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Get admin hash
cur.execute("SELECT password_hash FROM users WHERE id = 1")
admin_hash = cur.fetchone()[0]

# Insert new dev user
temp_sync_id = str(uuid.uuid4())
now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

cur.execute("""
    INSERT INTO users (SyncId, username, email, password_hash, role, is_active, is_deleted, created_at, updated_at)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
""", (temp_sync_id, "temp_dev", "dev@local.com", admin_hash, 0, 1, 0, now, now))

print("Created temp_dev user with same password as admin.")

# Deactivate users 1 and 2
cur.execute("UPDATE users SET is_active = 0, is_deleted = 1, updated_at = ? WHERE id IN (1, 2)", (now,))

conn.commit()

# Print current users
cur.execute("SELECT id, username, is_active, is_deleted FROM users")
print("\nCurrent local users:")
for u in cur.fetchall():
    print(u)

conn.close()
