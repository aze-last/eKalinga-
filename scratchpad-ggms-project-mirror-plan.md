# Plan — Mirror GGMS project_details into the Budget module

## Scope correction
Earlier draft over-focused on consolidated_transactions/beneficiary plumbing (CRS-id centric).
This plan is centered on the actual ask: **mirror GGMS-created projects into AMS Budget module,
deduct releases from each project's own sub-budget, and pull newly created GGMS projects for our
office (OFF-2026-0006) as part of the existing Sync GGMS action.**

## Write scope (hard boundary)
- READ ONLY: GGMS `project_details`, GGMS office/allocation tables, CRS `val_beneficiaries`.
- WRITE: `ams.db` (SQLite) and AMS Hostinger only.
- Pre-existing exception (unchanged, keep): the consolidated_transactions push to GGMS that already exists.
  Not expanded by this plan. We do NOT read project_details_id back; we only push as today.

## Sync GGMS button — combined flow (KEY CHANGE)
The existing **Sync GGMS** button in the Budget module (`BudgetViewModel.SyncGovernmentBudgetAsync`,
currently calls `GgmsBudgetSyncService.SyncAyudaBudgetAsync`) becomes a **two-step sync** in one click:

  1. STEP 1 — Budget allocation (existing): read office-level allocation for our office_code from
     GGMS (budget_allocations -> officeallocations fallback) and record/refresh the GovernmentBudgetSnapshot.
  2. STEP 2 — Projects (new): read GGMS `project_details` WHERE office_code = our office code
     (OFF-2026-0006) and upsert them into the local `ggms_project_cache`. Detect newly created
     projects (project_details_id not yet in cache) and updated ones (by SourceUpdatedAt).

  After both steps: reload the Budget overview + center list. Status message summarizes both, e.g.
  "Government budget synced. 2 new project(s) found for OFF-2026-0006."

  Failure isolation: if STEP 1 fails, report it and stop. If STEP 1 succeeds but STEP 2 fails
  (e.g. project_details unreachable), still commit STEP 1 and surface a partial-success warning so
  the allocation sync is never lost.

  Office code for both steps = BudgetRuntimeOptions.AyudaOfficeCode (defaults OFF-2026-0006).
  Both steps filter by office_code, never office id.

## Data model (new)

### 1. New local mirror table — `ggms_project_cache` (ams.db only)
Model `GgmsProjectCache` in `Models/LocalOnlyModels.cs` (follows existing `GgmsAllocationCache` pattern):
- `GgmsProjectCacheId` (PK, int, AI)
- `ProjectDetailsId`  (string, GGMS project_details.project_details_id — e.g. OPP-2026-0006) UNIQUE
- `YearlyBudgetId`    (int)
- `OfficeCode`        (string — filter key; we use office_code NOT office id)
- `ProjectName`       (string — GGMS `project`)
- `Description`       (string?)
- `SystemName`        (string?)
- `TotalBudget`       (decimal 18,2 — the sub-allocation amount)
- `Status`            (string — GGMS status, e.g. active)
- `VoucherCode`       (string?)
- `SourceCreatedAt`   (DateTime? — GGMS create_at)
- `SourceUpdatedAt`   (DateTime? — GGMS updated_at, for change detection on refresh)
- `CachedAt`          (DateTime)
- `IsLinked`          (bool — has a local AyudaProgram been created from this project?)

Register `DbSet<GgmsProjectCache>` in `LocalDbContext`.
Add `CREATE TABLE IF NOT EXISTS ggms_project_cache (...)` to `SQLiteSchemaBootstrapper.EnsureSQLiteSchema`.

### 2. Link field on AyudaProgram — `source_project_details_id`
Add nullable `SourceProjectDetailsId` (string, MaxLength 45) to `AyudaProgram`.
This is the durable link: an AMS project (AyudaProgram) that draws from GGMS project OPP-2026-xxxx.
- AMS Hostinger: add column via EF migration (ayuda_programs).
- ams.db: add via EnsureColumnExists in SQLiteSchemaBootstrapper.

## Services (new/changed)

### 3. New `GgmsProjectSyncService`
- `ReadProjectsForOfficeAsync()` — connects to GGMS (reuse BudgetRuntimeOptions.GgmsConnection),
  SELECT from `project_details` WHERE office_code = @officeCode (OFF-2026-0006).
  Static query builder `BuildProjectDetailsQuery(table)` for test assertions (mirrors existing pattern).
  Columns pulled: project_details_id, yearly_budget_id, office_code, project, description,
  system_name, total_budget, status, voucher_code, create_at, updated_at.
- `RefreshProjectCacheAsync(LocalDbContext)` — upsert into ggms_project_cache by ProjectDetailsId:
  insert new, update changed (by SourceUpdatedAt), preserve IsLinked flag. Never delete rows;
  if a GGMS project disappears, mark Status = 'archived' (soft) — respects "never delete" rule.
  Returns count of new projects found (surfaced in the Sync GGMS status message).
- New config option `GgmsProjectDetailsTable` (default "project_details") in BudgetRuntimeOptions,
  persisted in ggmssettings.json (same Normalize/Save/Load pattern as GgmsBudgetAllocationTable).

### 4. Wire into existing Sync GGMS action
`BudgetViewModel.SyncGovernmentBudgetAsync` calls STEP 1 (existing) then STEP 2
(GgmsProjectSyncService.RefreshProjectCacheAsync) inside the same try, sharing one LocalDbContext,
then LoadOverviewAsync + reload center list. No new button; existing gold Sync GGMS button drives both.

### 5. Budget waterfall — subtract project earmarks
In `BudgetManagementService.GetOverviewAsync` (the government branch):
- After computing governmentAllocated (office allocation) and releases, also subtract the
  SUM of ggms_project_cache.TotalBudget for active projects — these are earmarked sub-allocations
  already carved out on the GGMS side.
- Net unearmarked government pool = OfficeAllocation - SUM(project earmarks) - releases-since-sync.
- Per-project available = project.TotalBudget - SUM(ledger releases attributed to that project).
  Ledger attribution uses AyudaProgram.SourceProjectDetailsId -> BudgetLedgerEntry.ProgramId.

Reconciliation example (Ayuda OFF-2026-0006):
  Office 70,000 - project earmarks 31,641 = 38,359 unearmarked; each project spends its own envelope.

## UI (Budget module)

### 6. Project mirror list (center list view)
- Center list view: add mirrored GGMS projects as rows (Category = "GGMS Project"),
  alongside the existing Government Fund / Private Donation rows. Show ProjectName,
  TotalBudget, remaining (TotalBudget - released), Status, and IsLinked badge.
  Reuse `BudgetRecordListItem` with `OriginalItem = GgmsProjectCache`.
  Populated after the Sync GGMS button completes STEP 2.

### 7. Project linking flow
- Selecting a "GGMS Project" row + CREATE PROJECT opens the existing blurred overlay
  (OpenProjectCreationPanel), pre-filled from the mirrored project:
  - NewProjectSourceDescription = "Source: GGMS Project - {ProjectName} (PHP {TotalBudget})"
  - Store ProjectDetailsId so ConfirmCreateProjectAsync writes AyudaProgram.SourceProjectDetailsId.
  - Cap the project's releasable budget at TotalBudget.
  - Mark GgmsProjectCache.IsLinked = true on confirm.
- Batch/create inputs: name, description, amount — already present in the panel;
  amount validated against remaining project budget.

## Release behavior
- When AMS releases ayuda under a linked project, deduct from that project's TotalBudget envelope
  (enforced in release validation), record BudgetLedgerEntry as today (government_portion).
- consolidated_transactions push stays exactly as it is now (no schema change on GGMS side).
- Scan-time ID validation uses CRS val_beneficiaries (already wired via CrsValBeneficiary /
  BeneficiaryVerificationService) — reused, not rebuilt.

## Tests
- GgmsProjectSyncServiceTests: BuildProjectDetailsQuery shape (office_code filter, no office-id join,
  expected columns), configurable table name.
- BudgetRuntimeOptionsTests: GgmsProjectDetailsTable roundtrip.
- BudgetManagementService overview: project earmark subtraction math.
- SQLiteSchemaBootstrapperTests: ggms_project_cache table + source_project_details_id column created.
- SourceTests/BindingTests: Sync GGMS action drives project sync; GGMS Project list binding exists.

## Build/verify
- dotnet build AttendanceShiftingManagement.sln
- dotnet test AttendanceShiftingManagement.Tests
- EF migration for ayuda_programs.source_project_details_id (AMS Hostinger).
- Log to WhatsNew.md.

## Open decisions to confirm before coding
A. When a linked GGMS project's TotalBudget changes on GGMS after linking (admin edits 6k -> 8k):
   on Sync GGMS, update the cache + raise the AMS project cap to match? (plan: yes, update cap to GGMS
   value, never below already-released.)
B. Office code source: use BudgetRuntimeOptions.AyudaOfficeCode (defaults OFF-2026-0006) for both
   allocation + project steps. (plan: yes, single configured office code.)
