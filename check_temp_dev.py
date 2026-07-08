import sqlite3
c = sqlite3.connect(r'c:\Users\ASUS\source\repos\eKalinga-\bin\Debug\net9.0-windows\ams.db')
print(c.execute("SELECT id, username, is_active, role FROM users WHERE username='temp_dev'").fetchall())
