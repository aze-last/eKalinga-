import os
import sqlite3

def check_db(db_path):
    print(f"\nChecking: {db_path}")
    try:
        conn = sqlite3.connect(db_path)
        c = conn.cursor()
        
        # Check if private_donations table exists
        c.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='private_donations'")
        table_exists = c.fetchone()
        if not table_exists:
            print("  Table 'private_donations' does not exist.")
            conn.close()
            return
            
        # Get column info
        c.execute("PRAGMA table_info(private_donations)")
        columns = [row[1] for row in c.fetchall()]
        print(f"  Columns in private_donations: {columns}")
        if 'donation_type' in columns:
            print("  [OK] 'donation_type' is present.")
        else:
            print("  [ERROR] 'donation_type' is MISSING!")
            
        conn.close()
    except Exception as e:
        print(f"  Error checking database: {e}")

def main():
    root_dir = r"c:\Users\ASUS\source\repos\eKalinga-"
    for root, dirs, files in os.walk(root_dir):
        # Skip .git, .venv, obj, etc.
        if any(skip in root for skip in ['.git', '.venv', 'obj', '.artifacts', '.gemini']):
            continue
        for file in files:
            if file.endswith('.db'):
                db_path = os.path.join(root, file)
                check_db(db_path)

if __name__ == '__main__':
    main()
