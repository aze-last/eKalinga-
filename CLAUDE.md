# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

eKalinga+ (a.k.a. BarangayAyudaSys) — a WPF desktop app (.NET 9, `net9.0-windows`, SDK pinned to 9.0.310 in `global.json`) for barangay ayuda (aid) operations: beneficiary masterlist, aid requests, budget tracking, cash-for-work, project distributions, digital IDs, and QR/OCR scanning. The C# project name is `AttendanceShiftingManagement` (historical; the product is eKalinga+).

## Commands

```powershell
dotnet build AttendanceShiftingManagement.sln          # build
dotnet test AttendanceShiftingManagement.Tests          # run all tests
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~BudgetManagementServiceTests"   # one test class
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~ClassName.MethodName"           # one test
.\scripts\build-installer.ps1 -BootstrapInnoSetup       # publish win-x64 + build Inno Setup installer into artifacts\installer\output
```

Always run `dotnet build AttendanceShiftingManagement.sln` after code changes, and `dotnet test AttendanceShiftingManagement.Tests` when production code changed. Tests are xUnit; many are "SourceTests"/"BindingTests" that read `.cs`/`.xaml` files as text to assert bindings and structure exist — renaming bindings, commands, or XAML elements can break them even if the build passes.

For EF Core design-time commands, `AppDbContextFactory` honors env vars `ASM_DB_PRESET` (connection preset name) and `ASM_DB_CONNECTION_STRING` (full override).

## Architecture

MVVM without a framework: `Helpers/ObservableObject.cs` and `Helpers/RelayCommand.cs` are the base primitives; value converters also live in `Helpers/`. `Views/` (XAML pages/windows) bind to `ViewModels/`, which call `Services/` (one service per module — all business logic lives there). `Models/` are EF entities shared by all contexts.

**Startup flow:** `App.xaml.cs` → `SplashWindow` (handles theme, maintenance, and DB init) → `LoginWindow` → `MainWindow` hosting module pages. Startup problems are logged to `%LocalAppData%\AttendanceShiftingManagement\startup_log.txt`.

**Three database contexts** (`Data/`):
- `AppDbContext` — primary MySQL database. Connection chosen from presets (Local/LAN/Remote) in `appsettings.json` via `ConnectionSettingsService`. Migrations in `Data/Migrations/` are applied at startup by `StartupMigrationCoordinator` (which can also bootstrap/repair schema on an empty DB).
- `LocalDbContext` — SQLite `ams.db`; mirrors the app tables plus local-only offline cache tables (GGMS allocation/transaction caches, `SyncMetadata`). Schema handled by `SQLiteSchemaBootstrapper`.
- `CrsDbContext` — read-side connection to the central CRS (municipality) MySQL database for validated-beneficiary masterlist sync.

**Budget waterfall (core invariant):** funds enter from two streams — Government (GGMS sync) and Private Donations — and can be earmarked into buckets (AssistanceCaseBudget, CashForWorkBudget, project sources). Every fund release (aid request release, CFW payout, distribution claim) MUST record a `BudgetLedgerEntry` that consumes from the earmarked bucket first, then cascades to the general pools. Never add budget fields that bypass this ledger.

**Aid request lifecycle:** `Pending` → `UnderReview` → `Approved` → `Released` (or `Rejected`/`Cancelled`). Only valid transitions; release requires an assigned `ApprovedAmount` and program.

**Project distributions** are spawned from a specific funding source (Private Donation or GGMS budget, 1:1) in the Budget module — never created independently. Beneficiary enrollment is manual/bulk selection from the masterlist only (no demographic auto-enrollment).

## Hard rules

- **Never delete database rows.** Implement soft delete (`IsDeleted` flag) or stop and ask; deletions are reserved for the developer.
- **Never commit or expose secrets** from `appsettings.json` (connection strings, SMTP keys). This code is pushed to a public repo (`BarangayAyudaSys`); `appsettings.template.json` is the sanitized reference. Do not touch `appsettings.json`/`appsettings.template.json` unless explicitly asked.
- **Do not redesign the dashboard** (`BarangayDashboardPage`) or change its module set unless explicitly asked. The Equipment Borrowing module is permanently hidden from the UI — do not re-expose its navigation, cards, or permission settings.
- Preserve existing bindings and commands unless the task explicitly changes them; keep diffs small and avoid unrelated refactors.
- All module lists must be paginated (performance on low-end laptops).

## UI design system (eKalinga+ theme lock)

- Colors: brand `#1E4E89` (headers/titles), action accent `#F59E0B` (gold, primary operational buttons), sidebar background `#F8FAFC` (keep sidebars light), page background `#F1F5F9`, cards white with `1px #E2E8F0` borders. Success `#15803D`, error `#BE123C`, warning `#854D0E`.
- Layout: fixed 320px left sidebar (navigation, filters, gold action buttons), main data center, optional detail panel right; 30px content padding; 12–16px card radius, 6–8px button radius.
- Every operational action (Create/Edit/Payout/etc.) opens as a blurred overlay above the main content: `BlurRadius` 15.0 on the main grid, `#CC0F172A` backdrop; the list stays visible behind it — never swap or collapse the center area.
- Typography: module headers 24px bold brand; sidebar section headers 13px bold all-caps; DataGrid text 12–13px; card labels 12px bold muted above 24px heavy values.

## User management rules

- Only `SuperAdmin` can access the System User Management tab; SuperAdmins are exempt from permission restrictions.
- No user (including SuperAdmin) can delete or deactivate their own logged-in account; SuperAdmins may soft-delete other accounts.
- Security overlays for Remote Snapshot, App Database, and GGMS Budget Source must allow both `Admin` and `SuperAdmin` to unlock via password re-entry.

## Agent team harness

`.agent-team/` contains a repo-local swarm harness (task board, mailbox, worker templates). Only use it when the user asks for the swarm workflow; see `.agent-team/README.md` and the "Agentic Swarm" section of `README.md`.

## Notes

- The repo root contains many throwaway `check_*.py` / `query_*.py` / `fix_*.py` scripts used for ad-hoc SQLite/MySQL inspection — they are developer scratch tools, not part of the app.
- Session/day-end change summaries are documented in the Obsidian wiki at `C:\Users\ASUS\OneDrive\Desktop\Projects-wiki\WhatsNew.md` (prepend `## [YYYY-MM-DD hh:mm AM/PM] Title` entries) when the user asks for a summary.
