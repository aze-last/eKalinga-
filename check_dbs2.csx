using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.Extensions.DependencyInjection;

var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AttendanceShiftingManagement", "connectionsettings.json");
var json = File.ReadAllText(settingsPath);
Console.WriteLine("Connection Settings Loaded:");
Console.WriteLine(json);

var settings = ConnectionSettingsService.Load();

foreach (var preset in new[] { "Lan", "Remote" }) 
{
    Console.WriteLine($"\n--- Checking {preset} Database ---");
    var dbPreset = settings.GetPreset(preset);
    if (string.IsNullOrWhiteSpace(dbPreset.Server)) 
    {
        Console.WriteLine($"{preset} preset is empty.");
        continue;
    }
    
    var connString = ConnectionSettingsService.BuildConnectionString(dbPreset);
    Console.WriteLine($"Server: {dbPreset.Server}, Database: {dbPreset.Database}");
    
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optionsBuilder.UseMySql(connString, ServerVersion.AutoDetect(connString));
    
    try 
    {
        using var context = new AppDbContext(optionsBuilder.Options);
        var programs = context.AyudaPrograms.AsNoTracking().Where(p => p.IsActive).Select(p => new { p.Id, p.ProgramName, p.IsActive }).ToList();
        Console.WriteLine($"Active Programs in {preset}:");
        foreach(var p in programs) 
        {
            Console.WriteLine($"- Id: {p.Id}, Name: {p.ProgramName}, IsActive: {p.IsActive}");
        }
    } 
    catch (Exception ex) 
    {
        Console.WriteLine($"Failed to connect to {preset}: {ex.Message}");
    }
}
