# BarangayAyudaSys

WPF + EF Core + MySQL application for barangay ayuda operations.

## Scope
- Barangay dashboard
- Masterlist snapshot review
- Beneficiary staging and approval
- Household registry
- Cash-for-work event and attendance handling
- Remote table snapshot tools
- Local database backup and restore

## Quick Start
1. Ensure MySQL is running for the local preset in `appsettings.json`.
2. Run the app from Visual Studio.
3. Sign in with an existing admin account, or create the initial admin account when prompted.

## Database Reset
Set `Database.ResetOnStartup` to `true` in `appsettings.json`, run the app once, then set it back to `false`.

## Build Installer
Run the automated installer build from the project root:

```powershell
.\scripts\build-installer.ps1 -BootstrapInnoSetup
```

This publishes a self-contained `win-x64` build, then compiles a Windows `Setup.exe` installer with Inno Setup into `artifacts\installer\output`.

## Agentic Swarm
This repo already includes a local swarm harness in `.agent-team/`.

Use this prompt when you want a session to work through the repo-local swarm:

```text
Use the repo-local agentic swarm in `.agent-team/`.

Workflow:
- read `.agent-team/README.md`, `.agent-team/team.json`, and `.agent-team/decisions.md` first
- treat `.agent-team/tasks/*.json` as the shared task board
- treat `.agent-team/mailbox/` as the worker message transport
- keep every task bounded to one worker and its `allowedPaths`
- use `repo-mapper@attendance-shifting-management` before editing when the real file seam is unclear
- use `manual-writer@attendance-shifting-management` only for `README.md`, `docs/User-Manual-Ayuda.docx`, and `.agent-team` documentation updates, and only after explicit user instruction
- use `verifier@attendance-shifting-management` for final verification

Rules:
- do not touch `appsettings.json` or `appsettings.template.json` unless explicitly assigned
- do not let `manual-writer@attendance-shifting-management` start rewrites, screenshot capture, exports, or file replacement automatically
- preserve existing architecture and bindings unless the task says otherwise
- keep task state current in `.agent-team/tasks`
- run `dotnet build AttendanceShiftingManagement.sln` before closing
- run `dotnet test AttendanceShiftingManagement.Tests` when production code changed
```

# Project Workflow Context

## Core Module Workflows

1. Cash-for-Work & Seminars
   - Create Event/Seminar
   - Assign Eligible Beneficiaries
   - QR/OCR Attendance Scanning
   - Review Payout Summary
   - Release Budget
   - Generate Printable Attendance Sheet
   - Key Files:
     - CashForWorkOcrViewModel.cs
     - CashForWorkService.cs

2. Aid Request / Assistance Cases
   - Create Request
   - Link Validated Beneficiary
   - Assign Ayuda Program
   - Status Transitions:
     - Pending
     - Under Review
     - Approved
     - Released
     - Closed
   - Rules:
     - Valid transitions only
     - Release requires approved amount and program
   - Key Files:
     - AssistanceCaseManagementViewModel.cs
     - AssistanceCaseManagementService.cs

3. Masterlist
   - Sync Validated Snapshot from central CRS
   - Apply Quick Filters
   - Pagination
   - Detailed Profile Review
   - Key Files:
     - MasterListViewModel.cs
     - MasterListService.cs

4. Budget Management
   - **Fund Sourcing:** Sync Government Budget (GGMS) or record Private Donations (Cash/Check/Proof-of-transfer).
   - **Bucket Allocation:** Create "Budget Buckets" for specific modules (Assistance Case Budgets or Cash-for-Work Budgets).
   - **Budget Cap:** Set a hard limit (Cap) on each bucket to reserve funds and prevent overspending.
   - **Module Assignment:** Link these buckets to specific Aid Requests or CFW events.
   - **Fund Consumption:** Releasing funds checks the remaining "Cap" of the bucket before deducting from the global Government/Private pool.
   - **Audit & Export:** Track every movement in the unified Budget Ledger and export liquidation-ready CSVs.
   - Key Files:
     - BudgetViewModel.cs
     - BudgetManagementService.cs
     - GgmsBudgetSyncService.cs

5. Reports
   - Select Template
   - Set Date Range / Program Filter
   - Build Snapshot
   - View Metrics / Highlights
   - Export CSV/PDF or Print Preview
   - Key Files:
     - ReportsViewModel.cs
     - ReportsService.cs

6. Session Announcement
   - Auto-detect activity since last logout
   - Categorize updates
   - Present summary on login
   - Key Files:
     - SessionAnnouncementViewModel.cs
     - SessionAnnouncementService.cs

7. Digital ID
   - Finalize Beneficiary Approval
   - Auto-generate ID Card Number and QR Payload
   - Upload Photo
   - Print Preview / Printing
   - Key Files:
     - BeneficiaryVerificationViewModel.cs
     - DigitalIdPrintPreviewWindow.xaml

     EVERY LISTS TO EVERY MODULE DAPAT NAKA PAGINATION PARA DILI HEAVY SA LAPTOP ANG PAG LOAD SA LIST

     ASK THE AI TO ADD A ON/OFF OF OTP FEATURE SA SETTINGS PARA SMNOOTH IMONG PAG DEBUG LATER ON YOU WILL REMOVE THAT FEATURE.
