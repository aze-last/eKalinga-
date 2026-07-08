import mysql.connector

try:
    print("Connecting to Local...")
    cnx_local = mysql.connector.connect(
        user='root',
        password='codenameHylux122818',
        host='127.0.0.1',
        port=3306,
        database='attendance_shifting_db'
    )
    cursor_local = cnx_local.cursor(dictionary=True)
    cursor_local.execute("SELECT id, program_name, is_active FROM ayuda_programs WHERE is_active = 1")
    rows = cursor_local.fetchall()
    print("Local active programs:")
    for r in rows:
        print(r)
    cnx_local.close()
except Exception as e:
    print("Local connection failed:", e)

