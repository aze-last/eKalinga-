import sqlite3
c = sqlite3.connect(r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db')
c.execute("UPDATE users SET SyncId = upper(SyncId) WHERE id = 3")
c.commit()
print("SyncId upper-cased.")
