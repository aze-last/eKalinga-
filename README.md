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
3. Sign in with an existing admin account, or create the initial admin account when prompted.

## Database Reset
Set `Database.ResetOnStartup` to `true` in `appsettings.json`, run the app once, then set it back to `false`.

## Build Installer
Run the automated installer build from the project root:

```powershell
.\scripts\build-installer.ps1 -BootstrapInnoSetup
```

This publishes a self-contained `win-x64` build, then compiles a Windows `Setup.exe` installer with Inno Setup into `artifacts\installer\output`.
