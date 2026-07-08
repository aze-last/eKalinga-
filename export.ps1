$sourceDir = "c:\Users\ASUS\source\repos\eKalinga-"
$stagingDir = "$sourceDir\eKalinga_Modules_Export"
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
New-Item -ItemType Directory -Force -Path $stagingDir

$modules = @{
    "Masterlist" = @("*MasterList*", "*Beneficiary*")
    "Budget" = @("*Budget*", "*Ggms*")
    "Distribution" = @("*Distribution*", "*Borrowing*")
    "CashForWork" = @("*CashForWork*", "*Attendance*")
    "AidRequests" = @("*AssistanceCase*")
    "Reports" = @("*Report*")
    "UserManagement" = @("*UserManagement*")
    "Core" = @("*MainWindow*", "*App.*", "*Login*", "*Splash*", "*Scanner*", "*Settings*", "*Connection*", "*Dashboard*")
}

foreach ($mod in $modules.Keys) {
    $modDir = "$stagingDir\$mod"
    New-Item -ItemType Directory -Force -Path $modDir | Out-Null
    
    foreach ($pattern in $modules[$mod]) {
        Get-ChildItem -Path $sourceDir -Recurse -Filter $pattern -File | 
        Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\.git\\" -and $_.FullName -notmatch "\\.vs\\" -and $_.FullName -notmatch "eKalinga_Modules_Export" } |
        Copy-Item -Destination $modDir -Force -ErrorAction SilentlyContinue
    }
}

$mdContent = @"
# eKalinga+ Project Modules

This document provides an overview of the modules in the eKalinga+ Ayuda Management System.

## 1. Masterlist (Beneficiary Management)
Handles the core registry of beneficiaries, demographics, profiling, and ID management.

## 2. Budget
Manages funding streams (Government/GGMS and Private Donations), earmarking, and the budget waterfall system.

## 3. Distribution
Handles bulk project disbursements, beneficiary enrollment for events, and claiming of distributions tied to budget sources.

## 4. CashForWork
Manages cash-for-work events, beneficiary attendance tracking, and wage payouts.

## 5. AidRequests (Assistance Cases)
Handles individual walk-in assistance requests, approval workflows, and fund releases.

## 6. Reports
Generates system reports, statistics, and print previews.

## 7. UserManagement
Handles system users, roles (Admin/SuperAdmin), and access control permissions.

## 8. Core
Contains the application entry point, login flows, dashboard, database context, and shared services (like scanning and settings).

"@

Set-Content -Path "$stagingDir\Explanation.md" -Value $mdContent

Copy-Item -Path "$sourceDir\ams.db" -Destination "$stagingDir\ams.db" -Force

$zipPath = "$sourceDir\eKalinga_Advisor_Export.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath

Write-Host "Export completed: $zipPath"
