# AGENTS.md

WPF desktop app (.NET 9, `net9.0-windows`, SDK pinned to 9.0.310 in `global.json`). C# project name `AttendanceShiftingManagement` is historical; product is eKalinga+ (a.k.a. BarangayAyudaSys).

## Commands (PowerShell, repo root)

```powershell
dotnet build AttendanceShiftingManagement.sln
dotnet test AttendanceShiftingManagement.Tests
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~BudgetManagementServiceTests"  # one class
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~ClassName.MethodName"          # one test
.\scripts\build-installer.ps1 -BootstrapInnoSetup   # publish win-x64 + Inno Setup installer -> artifacts\installer\output
```

- Run build after every code change; run full tests when production code changed.
- xUnit suite includes `*SourceTests` / `*BindingTests` that read `.cs`/`.xaml` as text — renaming a binding, command, or XAML element breaks tests even when the build passes.
- EF design-time factory (`Data/AppDbContextFactory.cs`): `ASM_DB_PRESET` selects a connection preset, `ASM_DB_CONNECTION_STRING` overrides fully.

## Architecture

- MVVM, no framework: `Helpers/ObservableObject.cs` + `Helpers/RelayCommand.cs`; `Views/` bind `ViewModels/` which call `Services/` (all business logic lives in one service per module); `Models/` are EF entities shared by all contexts.
- Startup: `App.xaml.cs` → `SplashWindow` (theme, maintenance, DB init) → `LoginWindow` → `MainWindow`. Startup failures log to `%LocalAppData%\AttendanceShiftingManagement\startup_log.txt`.
- Three contexts in `Data/`: `AppDbContext` (primary MySQL, presets Local/LAN/Remote via `ConnectionSettingsService`; migrations in `Data/Migrations/` applied at startup by `StartupMigrationCoordinator`), `LocalDbContext` (SQLite `ams.db`, offline caches + `SyncMetadata`, schema via `SQLiteSchemaBootstrapper`), `CrsDbContext` (read-only central CRS MySQL for validated-beneficiary sync).

## Core invariants

- Budget waterfall: Government (GGMS sync) + Private Donations → earmarked buckets (`AssistanceCaseBudget`, `CashForWorkBudget`, project sources). Every release (aid release, CFW payout, distribution claim) MUST write a `BudgetLedgerEntry` consuming the bucket first, then general pools. Never add budget paths that bypass the ledger.
- Aid lifecycle: `Pending` → `UnderReview` → `Approved` → `Released` (or `Rejected`/`Cancelled`). Release requires `ApprovedAmount` + program.
- Projects/Events (Distribution, Cash-for-Work, Seminar) are spawned 1:1 from a funding source in the Budget module — never standalone. Enrollment is manual/bulk from the masterlist only.

## Hard rules

- Never delete DB rows — use `IsDeleted` soft delete or stop and ask.
- `appsettings.json` holds live secrets, is gitignored, and the code pushes to a public repo. Do not touch it or `appsettings.template.json` unless explicitly asked; never commit secrets.
- Do not redesign `BarangayDashboardPage`/module set; Equipment Borrowing stays hidden (no nav, cards, permissions).
- Keep diffs small; preserve existing bindings/commands; paginate every module list (low-end laptops).
- UI theme lock: brand `#15803D`, action gold `#F59E0B`, sidebar `#F8FAFC`, page `#F1F5F9`, cards white/`#E2E8F0`; 320px left sidebar, 30px padding; operational actions open as blurred overlay (`BlurRadius` 15, `#CC0F172A` backdrop), list stays visible behind.
- Users: only `SuperAdmin` sees System User Management; nobody can delete/deactivate their own account; Remote Snapshot / App Database / GGMS overlays unlock for `Admin` + `SuperAdmin` via password re-entry.

## Notes

- Root `check_*.py` / `query_*.py` / `fix_*.py` are throwaway DB-inspection scratch, not app code.
- `.agent-team/` swarm harness: only use when the user explicitly asks (see `README.md` "Agentic Swarm"). Full conventions in `CLAUDE.md`.
