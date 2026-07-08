import sqlite3
conn = sqlite3.connect('ayudasys.db')
c = conn.cursor()
print(c.execute('SELECT name FROM sqlite_master WHERE type=\'table\'').fetchall())
