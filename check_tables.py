import sqlite3
con = sqlite3.connect(r'bin\Debug\net9.0-windows\ams.db')
for r in con.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"):
    print(r[0])
