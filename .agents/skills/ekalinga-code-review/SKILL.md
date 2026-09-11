---
name: ekalinga-code-review
description: "Perform in-depth, production-grade code reviews for the eKalinga+ (BarangayAyudaSys) WPF desktop application. Audits budget waterfall coupling, database soft-delete rules, XAML/MVVM binding test integrity, multi-unit connection presets, and heraldic design system compliance."
category: review
risk: safe
tags: "[dotnet, wpf, code-review, ekalinga, ayuda, budget-waterfall, ef-core]"
date_added: "2026-09-11"
---

# eKalinga+ In-Depth Code Review

This skill provides a comprehensive, rigorous code review protocol specifically calibrated for the **eKalinga+** (a.k.a. `BarangayAyudaSys` / `AttendanceShiftingManagement`) desktop application (.NET 9 WPF, MVVM, EF Core).

---

## 1. Review Priority Matrix

| Priority | Focus Area | Critical Check |
|---|---|---|
| **P0: Blockers** | Database & Financial Safety | Zero hard deletes, zero secret leaks, Budget Waterfall coupling. |
| **P0: Tests** | Binding & Source Tests | No renaming of XAML bindings, commands, or elements that break text-assertion tests. |
| **P1: Business Logic** | State Machines & 1:1 Funding | Strict aid request lifecycle, project distribution 1:1 source coupling, CFW attendance validation. |
| **P1: Multi-Unit** | Concurrency & Presets | Safe case numbers, dual DbContext (`LocalDbContext` vs `AppDbContext`), `SyncId` UUIDs. |
| **P2: UI/UX** | Design System Lock | Heraldic green/gold palette, fixed 320px sidebar, 15px blur overlay, locked dashboard. |

---

## 2. Checklist: Critical Blockers (P0)

### 2.1 Database Deletion Safety
- [ ] **Zero Hard Deletes:** Search diff for `_context.Remove(`, `_context.*.RemoveRange(`, `DELETE FROM`, or `ExecuteDeleteAsync(`.
- [ ] If found, **REJECT IMMEDIATELY**. Deletions are strictly reserved for the human developer.
- [ ] Data removal MUST use soft deletion (e.g., `IsDeleted = true`, `IsActive = false`) or status transition (`Cancelled`/`Rejected`).

### 2.2 Secrets & Repository Sanitization
- [ ] Verify `appsettings.json` and `appsettings.template.json` are NOT modified unless explicitly instructed.
- [ ] Ensure no connection strings, passwords, SMTP credentials, or private keys are hardcoded in `.cs` or `.xaml` files.

### 2.3 Budget Waterfall & Ledger Integrity
- [ ] Every financial payout (Aid Request release, Cash-for-Work payout, Project Distribution claim) MUST create a `BudgetLedgerEntry`.
- [ ] Verify that consumption follows the waterfall:
  1. Earmarked sub-budget bucket (e.g. `AssistanceCaseBudgets`, `CashForWorkBudgets`, or `ProjectBudgetSources`).
  2. Cascade down to general Private Donations or GGMS government funds if earmarked capacity is insufficient.
- [ ] Check that no disconnected, unledgered budget balance fields are introduced.

### 2.4 Binding & Source Test Protection
- [ ] `AttendanceShiftingManagement.Tests` contains reflection-less tests that parse `.cs` and `.xaml` files as raw text (e.g., `*BindingTests.cs`, `*SourceTests.cs`).
- [ ] Inspect diff for:
  - Renamed `ICommand` properties or ViewModel getters/setters.
  - Renamed XAML `x:Name`, `Path=...`, `Command=...`, or Converter resource keys.
- [ ] Any mismatch will break test assertions even if `dotnet build` compiles cleanly.

---

## 3. Checklist: Business Logic & Workflows (P1)

### 3.1 Aid Request (Assistance Cases) State Machine
- [ ] Strict progression: `Pending` $\rightarrow$ `UnderReview` $\rightarrow$ `Approved` $\rightarrow$ `Released`.
- [ ] Fast-track release must validate that `ApprovedAmount > 0` and total budget pool is sufficient before releasing.
- [ ] Terminal lock: Once in `Released` state, the case must be completely read-only in the UI.
- [ ] Rejection and cancellation must mandate non-empty `ResolutionNotes` / `RejectionReason`.

### 3.2 Project Distributions & Events
- [ ] Distribution projects, Cash-for-Work events, and Seminars must spawn from a 1:1 funding source in the Budget module.
- [ ] Beneficiary enrollment must be manual or bulk selection from the approved masterlist only. Demographics-based auto-enrollment (e.g., "all seniors") is prohibited.
- [ ] Planned budget (`Unit Amount * Beneficiary Count`) must never exceed the allocated source fund cap.

### 3.3 Cash-for-Work & Seminars
- [ ] Cash-for-Work and Seminars share identical logic, UI, and workflows (distinguished by category/prefix).
- [ ] Wage payouts must cross-verify attendance records (`CashForWorkAttendance`).

---

## 4. Checklist: Data Access & Multi-Unit Concurrency (P1)

### 4.1 DbContext Context Awareness
- [ ] Understand the context role:
  - `AppDbContext`: Always connects via MySQL using the preset specified in `ConnectionSettingsService`.
  - `LocalDbContext`: Dynamically connects via MySQL if preset is `Lan` or `Remote`; falls back to SQLite `ams.db` only when preset is `Local`.
  - `CrsDbContext`: Read-only connection to municipal CRS database for beneficiary verification.
- [ ] Ensure queries across contexts do not mix untracked entity instances.

### 4.2 Multi-Unit Concurrency Guardrails
- [ ] **Case Number Collisions:** Check case number generators (e.g. `GenerateCaseNumberAsync`). Ensure concurrent intake stations on the same LAN/Remote MySQL DB cannot generate duplicate sequence numbers (use retry loops or unique constraints).
- [ ] **Distributed ID:** Verify newly created entities initialize `SyncId = Guid.NewGuid()`.
- [ ] **AsNoTracking:** Use `.AsNoTracking()` on read-heavy or paginated queries to conserve memory on low-end municipal laptops.

---

## 5. Checklist: UI/UX & Design System Lock (P2)

### 5.1 Barangay Heraldic Color Palette
- [ ] **Brand Primary:** `#15803D` (Forest Green) for headers, icons, and title emphasis.
- [ ] **Action Accent:** `#F59E0B` (Amber/Gold) exclusively for primary action/CTA buttons.
- [ ] **Sidebar Background:** `#F8FAFC` (Light Slate/Off-white). Never use dark sidebars.
- [ ] **Surfaces:** Pure White `#FFFFFF` for cards; `#F1F5F9` for main background.
- [ ] **Error / Destructive:** `#BE123C` (Crimson). Never use red for chrome or decoration.
- [ ] **Banned Colors:** No teal (`#0F766E`), no blue chrome, no raw unstyled primary colors.

### 5.2 Layout & Overlays
- [ ] **Sidebar Width:** Fixed at `320px`.
- [ ] **Dashboard Lock:** Do NOT redesign `BarangayDashboardPage`. Keep existing module set. `Equipment Borrowing` must remain hidden.
- [ ] **Blurred Overlays:** Every operational action panel (Create, Edit, Disburse) must open over the center list with:
  - `BlurRadius="15"` on the main content container.
  - Backdrop brush `#CC0F172A` (Midnight Slate at 80% opacity).
  - Main list/DataGrid must remain visible behind the blur (never swap or collapse the center area).
- [ ] **Pagination:** All tables/grids must include pagination controls (`CurrentPage`, `PageSize`, `TotalPages`).

---

## 6. Review Execution & Verification Steps

When performing a review on any git diff, PR, or staged changes:

1. **Diff Inspection:**
   Run `git diff` or review the changed files against each checklist above.
2. **Compile Solution:**
   ```powershell
   dotnet build AttendanceShiftingManagement.sln
   ```
   Ensure zero errors and zero new compiler warnings.
3. **Run Test Suite:**
   ```powershell
   dotnet test AttendanceShiftingManagement.Tests
   ```
   Verify that all unit tests, integration tests, and binding tests pass.
4. **Structured Review Output Format:**
   Summarize findings using this structure:
   - **Verdict:** `APPROVED`, `REQUEST_CHANGES`, or `BLOCKED`.
   - **Critical Violations (P0):** Hard deletes, secret leaks, budget ledger breaks, binding test breaks.
   - **Workflow & Business Logic (P1):** State transitions, overdraw risks, concurrency traps.
   - **UI/UX Compliance (P2):** Theme locks, colors, blur overlays, pagination.
   - **Verification Results:** Build & test run output.
