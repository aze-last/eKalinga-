import sqlite3
import mysql.connector

def check_sqlite():
    print("--- SQLITE (ams.db) ---")
    conn = sqlite3.connect('ams.db')
    cursor = conn.cursor()
    
    cursor.execute("SELECT COUNT(*) FROM households")
    print("Households count:", cursor.fetchone()[0])
    
    cursor.execute("SELECT COUNT(*) FROM household_members")
    print("Household members count:", cursor.fetchone()[0])
    
    cursor.execute("SELECT COUNT(*) FROM BeneficiaryStaging")
    print("BeneficiaryStaging count:", cursor.fetchone()[0])
    
    cursor.execute("SELECT COUNT(*) FROM BeneficiaryStaging WHERE BeneficiaryId LIKE 'MOCK-%'")
    print("Mock BeneficiaryStaging count:", cursor.fetchone()[0])
    
    cursor.execute("SELECT StagingID, FullName, LinkedHouseholdId, LinkedHouseholdMemberId, VerificationStatus FROM BeneficiaryStaging LIMIT 5")
    for row in cursor.fetchall():
        print("Staging:", row)
        
    conn.close()

def check_mysql():
    print("\n--- MYSQL (attendance_shifting_db) ---")
    try:
        conn = mysql.connector.connect(
            user='root',
            password='codenameHylux122818',
            host='127.0.0.1',
            port=3306,
            database='attendance_shifting_db'
        )
        cursor = conn.cursor()
        
        cursor.execute("SHOW TABLES")
        tables = [t[0] for t in cursor.fetchall()]
        print("MySQL Tables:", tables)
        
        # Check if households, household_members, beneficiary_staging exist in mysql
        for t in ['households', 'household_members', 'BeneficiaryStaging', 'val_beneficiaries']:
            if t in tables or t.lower() in [table.lower() for table in tables]:
                cursor.execute(f"SELECT COUNT(*) FROM {t}")
                print(f"Table {t} count:", cursor.fetchone()[0])
                
        conn.close()
    except Exception as e:
        print("MySQL query failed:", e)

if __name__ == '__main__':
    check_sqlite()
    check_mysql()
