using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AttendanceShiftingManagement.Data;

var options = new DbContextOptionsBuilder<LocalDbContext>()
    .UseSqlite("Data Source=ayudasys.db")
    .Options;

using var db = new LocalDbContext(options);
var staging = db.BeneficiaryStaging.Where(b => b.FullName.Contains("Bianca")).ToList();
Console.WriteLine($"Found {staging.Count} staging records for Bianca");
foreach (var s in staging) {
    Console.WriteLine($"StagingID: {s.StagingID}, FullName: {s.FullName}, BenId: {s.BeneficiaryId}");
    
    var membership = db.AyudaProjectBeneficiaries.FirstOrDefault(m => m.BeneficiaryStagingId == s.StagingID);
    if (membership != null) {
        Console.WriteLine($"  -> Enrolled in project {membership.AyudaProgramId}");
    } else {
        Console.WriteLine($"  -> NOT enrolled via StagingID");
        var alt = db.AyudaProjectBeneficiaries.FirstOrDefault(m => m.BeneficiaryId == s.BeneficiaryId);
        if (alt != null) {
             Console.WriteLine($"  -> Enrolled via BeneficiaryId! Project {alt.AyudaProgramId}, StagingId in proj: {alt.BeneficiaryStagingId}");
        }
    }
    
    var digId = db.BeneficiaryDigitalIds.FirstOrDefault(d => d.BeneficiaryStagingId == s.StagingID);
    if (digId != null) {
        Console.WriteLine($"  -> Digital ID: {digId.CardNumber}, Payload: {digId.QrPayload}");
    } else {
        Console.WriteLine($"  -> NO Digital ID");
    }
}

var allDigIds = db.BeneficiaryDigitalIds.ToList();
Console.WriteLine($"Total Digital IDs: {allDigIds.Count}");

var pending = db.AyudaProjectBeneficiaries.Where(b => b.Status == AttendanceShiftingManagement.Models.DistributionBeneficiaryStatus.Pending).ToList();
Console.WriteLine($"Total Pending Beneficiaries: {pending.Count}");
foreach(var p in pending) {
    Console.WriteLine($"Proj {p.AyudaProgramId} Ben {p.BeneficiaryStagingId} Name {p.FullName} BenId {p.BeneficiaryId}");
}
