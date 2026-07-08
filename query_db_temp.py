import sqlite3
conn = sqlite3.connect('ams.db')
c = conn.cursor()
c.execute("PRAGMA table_info(ayuda_programs)")
print([col[1] for col in c.fetchall()])
c.execute("SELECT * FROM ayuda_programs")
print("Projects:", c.fetchall())

c.execute("SELECT b.StagingID, b.CivilRegistryId, b.BeneficiaryId, l.id, l.beneficiary_id FROM BeneficiaryStaging b JOIN beneficiary_assistance_ledger l ON b.CivilRegistryId = l.civil_registry_id LIMIT 5")
print("Any Bug1:", c.fetchall())
