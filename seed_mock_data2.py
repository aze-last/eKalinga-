"""Recreate val_beneficiaries (schema mirrors CrsDbContext/CrsValBeneficiary) then
insert the 27 mock rows from the SQLite staging table."""
import sqlite3
import mysql.connector

conn = mysql.connector.connect(user='root', password='codenameHylux122818',
                               host='127.0.0.1', database='attendance_shifting_db')
mcur = conn.cursor()

mcur.execute("""
CREATE TABLE IF NOT EXISTS val_beneficiaries (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    residents_id BIGINT NOT NULL,
    beneficiary_id VARCHAR(64) NOT NULL DEFAULT '',
    user_id INT NULL,
    civilregistry_id VARCHAR(64) NULL,
    last_name VARCHAR(128) NULL,
    first_name VARCHAR(128) NULL,
    middle_name VARCHAR(128) NULL,
    full_name VARCHAR(255) NULL,
    sex VARCHAR(16) NULL,
    date_of_birth VARCHAR(32) NULL,
    age VARCHAR(8) NULL,
    marital_status VARCHAR(32) NULL,
    address VARCHAR(255) NULL,
    is_pwd TINYINT(1) NOT NULL DEFAULT 0,
    pwd_id_no VARCHAR(64) NULL,
    is_senior TINYINT(1) NOT NULL DEFAULT 0,
    senior_id_no VARCHAR(64) NULL,
    disability_type VARCHAR(128) NULL,
    cause_of_disability VARCHAR(128) NULL,
    created_at DATETIME NULL,
    updated_at DATETIME NULL,
    KEY idx_residents (residents_id),
    KEY idx_lastname (last_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
""")
print('val_beneficiaries table ensured.')

con = sqlite3.connect(r'bin\Debug\net9.0-windows\ams.db')
rows = con.execute("""SELECT ResidentsId, BeneficiaryId, CivilRegistryId, LastName, FirstName,
                      FullName, Sex, DateOfBirth, Age, MaritalStatus, Address, IsSenior
                      FROM BeneficiaryStaging ORDER BY StagingID""").fetchall()
con.close()

inserted = 0
for r in rows:
    mcur.execute("SELECT COUNT(*) FROM val_beneficiaries WHERE residents_id=%s", (r[0],))
    if mcur.fetchone()[0]:
        continue
    mcur.execute("""INSERT INTO val_beneficiaries (residents_id, beneficiary_id, civilregistry_id,
                    last_name, first_name, full_name, sex, date_of_birth, age, marital_status,
                    address, is_pwd, is_senior, created_at, updated_at)
                    VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,0,%s,NOW(),NOW())""",
                 (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11]))
    inserted += 1
conn.commit()
mcur.execute("SELECT COUNT(*) FROM val_beneficiaries")
print(f'inserted {inserted}, total val_beneficiaries: {mcur.fetchone()[0]}')
mcur.execute("SELECT residents_id, first_name, last_name, age, is_senior FROM val_beneficiaries WHERE last_name='Regidor'")
for r in mcur.fetchall():
    print(r)
conn.close()
