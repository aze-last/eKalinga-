import sqlite3
import os
import mysql.connector

def check_sqlite(path):
    print(f"\n--- Checking SQLite: {path} ---")
    if not os.path.exists(path):
        print("Does not exist.")
        return
    con = sqlite3.connect(path)
    cur = con.cursor()
    cur.execute("SELECT StagingID, FullName, BeneficiaryId, VerificationStatus, LinkedHouseholdId, LinkedHouseholdMemberId FROM BeneficiaryStaging WHERE LastName='Regidor'")
    print("BeneficiaryStaging (Regidor):", cur.fetchall())
    cur.execute("SELECT id, household_code, head_name FROM households WHERE household_code='HH-0005'")
    hh = cur.fetchall()
    print("Households (HH-0005):", hh)
    if hh:
        cur.execute("SELECT id, household_id, full_name, relationship_to_head FROM household_members WHERE household_id=?", (hh[0][0],))
        print("Household Members:", cur.fetchall())
    con.close()

def check_mysql():
    print("\n--- Checking Local MySQL ---")
    conn = mysql.connector.connect(user='root', password='codenameHylux122818', host='127.0.0.1', database='attendance_shifting_db')
    cur = conn.cursor()
    cur.execute("SELECT StagingID, FullName, BeneficiaryId, VerificationStatus, LinkedHouseholdId, LinkedHouseholdMemberId FROM BeneficiaryStaging WHERE LastName='Regidor'")
    print("BeneficiaryStaging (Regidor):", cur.fetchall())
    cur.execute("SELECT id, household_code, head_name FROM households WHERE household_code='HH-0005'")
    print("Households (HH-0005):", cur.fetchall())
    cur.execute("SELECT residents_id, last_name, first_name FROM val_beneficiaries WHERE last_name='Regidor'")
    print("ValBeneficiaries (Regidor):", cur.fetchall())
    conn.close()

if __name__ == '__main__':
    check_sqlite(os.path.expandvars(r'%LOCALAPPDATA%\eKalingaPlus\ams.db'))
    check_sqlite('ams.db')
    check_sqlite(r'bin\Debug\net9.0-windows\ams.db')
    check_mysql()
