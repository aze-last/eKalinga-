using System;
using System.Threading.Tasks;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Services;

class Program
{
    static async Task Main()
    {
        try
        {
            using var db = new LocalDbContext();
            var svc = new BeneficiaryHistoryService(db);
            var res = await svc.SearchBeneficiariesAsync("", 1, 20);
            Console.WriteLine($"Success! Count: {res.TotalCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
        }
    }
}
