import sqlite3
c = sqlite3.connect(r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db')
c.execute("UPDATE users SET created_at = '2026-07-08 16:29:11.0000000', updated_at = '2026-07-08 16:29:11.0000000' WHERE id=3")
c.execute("UPDATE users SET updated_at = '2026-07-08 16:29:11.0000000' WHERE id IN (1,2)")
c.commit()
print("Dates fixed.")
