import mysql.connector

def update_barcodes():
    config = {
        'host': '127.0.0.1',
        'port': 3306,
        'user': 'root',
        'password': 'codenameHylux122818',
        'database': 'attendance_shifting_db'
    }
    
    try:
        conn = mysql.connector.connect(**config)
        cursor = conn.cursor()
        
        # Get 3 beneficiaries
        cursor.execute("SELECT id FROM beneficiary_staging LIMIT 3")
        rows = cursor.fetchall()
        
        if len(rows) < 3:
            print("Not enough beneficiaries in the database!")
            return
            
        barcodes = ["4800016068010", "6936137800166", "4800049720121"]
        
        for i, row in enumerate(rows):
            beneficiary_id = row[0]
            barcode = barcodes[i]
            
            # Update the barcode
            cursor.execute(
                "UPDATE beneficiary_staging SET barcode_number = %s WHERE id = %s",
                (barcode, beneficiary_id)
            )
            print(f"Updated beneficiary ID {beneficiary_id} with barcode {barcode}")
            
        conn.commit()
        print("Successfully assigned the 3 barcodes to mock beneficiaries.")
        
    except Exception as e:
        print(f"Error: {e}")
    finally:
        if 'conn' in locals() and conn.is_connected():
            cursor.close()
            conn.close()

if __name__ == "__main__":
    update_barcodes()
