import mysql.connector

def check_db(name, host, user, pwd, db):
    try:
        print(f"\nConnecting to {name}...")
        cnx = mysql.connector.connect(
            user=user,
            password=pwd,
            host=host,
            port=3306,
            database=db
        )
        cursor = cnx.cursor(dictionary=True)
        cursor.execute("SELECT id, program_name, is_active FROM ayuda_programs")
        rows = cursor.fetchall()
        print(f"{name} all programs:")
        for r in rows:
            print(r)
        cnx.close()
    except Exception as e:
        print(f"{name} connection failed:", e)

check_db('Lan', '192.168.1.2', 'root', 'client123', 'attendance_shifting_db')
check_db('Remote', '194.59.164.58', 'u621755393_ams_user', 'Ams@2026', 'u621755393_ams')
