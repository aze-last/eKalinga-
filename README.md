# Barangay Ayuda System

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
3. Sign in with the seeded admin account.

## Seeded Admin Account
- `admin@barangay.local` / `admin123`

## Database Reset
Set `Database.ResetOnStartup` to `true` in `appsettings.json`, run the app once, then set it back to `false`.
