using AttendanceShiftingManagement.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace AttendanceShiftingManagement
{
    public class BarcodeLinker
    {
        public static async Task Main(string[] args)
        {
            const string TargetName = "Bien Josef G Regidor";
            const string Barcode = "4800016068010";

            Console.WriteLine($"Linking barcode {Barcode} to profile {TargetName}...");

            try
            {
                using var context = new AppDbContext();
                
                // 1. Find the beneficiary staging record
                var staging = await context.Set<Models.MasterListBeneficiary>()
                    .FromSqlRaw("SELECT * FROM BeneficiaryStaging WHERE FullName LIKE {0}", $"%{TargetName}%")
                    .FirstOrDefaultAsync();

                if (staging == null)
                {
                    Console.WriteLine("Error: Beneficiary record not found.");
                    return;
                }

                Console.WriteLine($"Found record for {staging.FullName} (StagingID: {staging.ResidentsId})");

                // 2. Update or Insert the Digital ID record
                var rowsAffected = await context.Database.ExecuteSqlRawAsync(
                    "UPDATE beneficiary_digital_ids SET qr_payload = {0} WHERE beneficiary_staging_id = (SELECT StagingID FROM BeneficiaryStaging WHERE FullName LIKE {1} LIMIT 1)",
                    Barcode, $"%{TargetName}%");

                if (rowsAffected > 0)
                {
                    Console.WriteLine("Success: Barcode linked to existing Digital ID.");
                }
                else
                {
                    Console.WriteLine("Notice: No existing Digital ID found. You need to 'Print Digital ID' once in the app first to generate the record, or I can insert a mock one.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
