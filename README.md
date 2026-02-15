# Attendance Shifting Management System (ASMS)

WPF + EF Core + MySQL attendance and shift management application.

## Quick Start
1. Ensure MySQL is running and `appsettings.json` connection string is correct.
2. Run the app from Visual Studio.
3. On startup, the app migrates schema and seeds demo data.

## Seeded Login Accounts
- Admin: `admin@mcdonald.com` / `admin123`
- HR Staff: `hr@mcdonald.com` / `hr123`
- Manager: `manager1@mcdonald.com` / `manager123`
- Crew (demo): `crew@gmail.com` / `crew123`
- Crew (batch): `crew1@mcdonald.com` to `crew19@mcdonald.com` / `crew123`

## Reset and Reseed Database
Use either option:
1. Set `Database.ResetOnStartup` to `true` in `appsettings.json`, run once, then set it back to `false`.
2. Start app with argument: `--reset-db`.

## Teacher Submission Docs
- `docs/ASMS-Teacher-Submission.md`
- `docs/asms_schema.sql`

## Phase 2 Enhancements (Implemented)
- Leave workflow now uses centralized validation service (balance checks, overlap checks, approval/rejection rules).
- Attendance status logic now handles shared shifts correctly and includes `On Leave` / `Early Leave`.
- Payroll now computes gross pay, late/early-leave deductions, and net pay.
- CSV exports available for payroll and manager daily attendance reports.
- Audit logging added for login, attendance, leave actions, and payroll save actions.

## Phase 3 Finalization (Implemented)
- RBAC hardening:
  - User Management is Admin-only.
  - HR restrictions enforced in dashboard navigation/commands.
- Flow correction:
  - `Add New Employee` now opens employee dialog (not user dialog).
  - Manager `Attendance Logs` now routes to attendance logs page.
- Final docs:
  - `docs/PHASE3-QA-DEPLOYMENT.md`
  - `docs/PHASE3-DEFENSE-CHECKLIST.md`

## Demo Role Switch (New)
- Purpose: avoid repeated logout/login during adviser demo.
- Enabled via `AppSettings:EnableDemoRoleSwitch` in `appsettings.json`.
- Admin can use `Switch Role` from `MainWindow` sidebar.
- When impersonating, `Return Admin` appears:
  - in `MainWindow` for HR impersonation,
  - in `ManagerMainWindow` and `CrewMainWindow` for manager/crew impersonation.
- Audit events recorded:
  - `ImpersonationStart`
  - `ImpersonationEnd`
