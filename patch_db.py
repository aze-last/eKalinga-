import sqlite3

def patch_db(db_path):
    print(f"Patching {db_path}...")
    try:
        conn = sqlite3.connect(db_path)
        c = conn.cursor()
        
        # private_donations
        try:
            c.execute("ALTER TABLE private_donations ADD COLUMN donation_type TEXT NOT NULL DEFAULT 'Cash'")
            print("Added donation_type to private_donations")
        except Exception as e:
            print(f"Skipping donation_type on private_donations: {e}")

        try:
            c.execute("ALTER TABLE private_donations ADD COLUMN item_name TEXT")
            print("Added item_name to private_donations")
        except Exception as e:
            print(f"Skipping item_name on private_donations: {e}")

        try:
            c.execute("ALTER TABLE private_donations ADD COLUMN quantity decimal(18,2)")
            print("Added quantity to private_donations")
        except Exception as e:
            print(f"Skipping quantity on private_donations: {e}")
            
        try:
            c.execute("ALTER TABLE private_donations ADD COLUMN unit_of_measure TEXT")
            print("Added unit_of_measure to private_donations")
        except Exception as e:
            print(f"Skipping unit_of_measure on private_donations: {e}")

        conn.commit()
        conn.close()
        print(f"Finished patching {db_path}")
    except Exception as e:
        print(f"Error accessing {db_path}: {e}")

patch_db("ams.db")
patch_db("ayudasys.db")
patch_db("bin/Debug/net9.0-windows/ams.db")
