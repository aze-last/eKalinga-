# Budget Worker

You are the designated agent for the Budget Management module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility covers fund sourcing (GGMS / Private Donations), central project and event creation (Distribution, Cash-for-Work, Seminars), 1:1 funding allocation, budget cap management, ledger auditing, and earmark reallocation.

Allowed files:
- `Views/BudgetPage.xaml`
- `Views/BudgetPage.xaml.cs`
- `ViewModels/BudgetViewModel.cs`
- `Services/BudgetManagementService.cs`
- `Services/GgmsBudgetSyncService.cs`
- `Models/BudgetingModels.cs`
- `AttendanceShiftingManagement.Tests/BudgetManagementServiceTests.cs`
- `AttendanceShiftingManagement.Tests/BudgetPageBindingTests.cs`

## Core Business Logic & Workflows (Source of Truth)

### 1. Funding Ingestion
- **Government Funds (GGMS):** Synchronized from external GGMS snapshots and mirrored in `GgmsProjectCache`. Tracks yearly allocations, office details, project details codes (e.g., `OPP-2026-0006`), and sub-allocation envelopes.
- **Private Donations:** Supports both **Cash** and **Goods** donations from Persons or Organizations. Captures proof attachments, reference numbers, and auto-records a `Donation` or `GoodsDonation` ledger entry.

### 2. Central Project & Event Creation (Funding-First Architecture)
The Budget module is the **exclusive creator** for all operational projects and events:
- **Distribution Projects:** Creates `AyudaProgram` records (Cash or Goods) strictly linked 1:1 to a funding source (`SourceDonationId`, `SourceGGMSBudgetId`, or `SourceProjectDetailsId`).
- **Cash-for-Work & Seminar Projects:** Creates `CashForWorkBudget` and auto-spawns the corresponding `CashForWorkEvent` (matching `EventKind`: `CashForWork` or `Seminar`, `BenefitType`: `Cash` or `Goods`, `DailyRate`, and start/end dates).
- **Enforce Fund Envelopes:** The project budget cap is derived from and cannot exceed the parent funding source balance.
- **Beneficiary Pre-Enrollment:** Supports manual/bulk selection of validated beneficiaries from the masterlist, checking household benefit history, and validating total required budget against fund caps before project creation.

### 3. Budget Waterfall & Release Accounting
- **1:1 Fund Derivation:** Projects explicitly linked to a donation or GGMS envelope consume from that specific source first.
- **General Waterfall Fallback:** For unlinked assistance or general claims, releases draw from earmarked bucket caps first, then cascade down to available Government and Private general pools.
- **Strict Cap Enforcement:** Every release validates that cumulative spend plus release amount does not exceed the defined budget cap.

### 4. Earmark Reallocation
- **Dormant Fund Reclaim:** `ReallocateEarmarkAsync` allows reclaiming remaining or unused funds from completed/dormant projects or cases back to the unrestricted general pool via a `Reallocation` ledger entry.

### 5. Ledger & Audit Trail
- **Immutable Ledger:** Every monetary and in-kind movement (Donation, Release, Reallocation, GoodsDonation) is logged in `budget_ledger_entries`.
- **Activity Logging:** All mutations trigger `AuditService.LogActivityAsync` for system tracking.

## UI/UX & Layout Architecture

### 1. Workspace Panels (`BudgetWorkspacePanel`)
- **Dashboard Panel:** Summary KPI cards (Government Allocated/Spent/Available, Private Available, Total Released, Weekly/Monthly spend), funding source records list, and active project list.
- **Government Sync Panel:** GGMS connection status, sync logs, and mirrored project sub-allocations.
- **Ledger Panel:** Full transaction history with search, category filtering, export, and **mandatory pagination**.
- **Project Creation Panel / Wizard:** Modal overlay for creating Distribution, Cash-for-Work, or Seminar projects with funding selection, item configuration, and masterlist beneficiary selection.

### 2. Form & Interaction Behaviors
- **Private Donation Form Rules:**
  - When **Cash** is selected: Show *Donation Amount (PHP)*; hide all Goods fields (*Item Name*, *Quantity*, *Unit of Measure*).
  - When **Goods** is selected: Hide *Donation Amount*; show all Goods fields.
  - Toggling between Cash and Goods must automatically clear opposite fields.
  - Opening the donation form must call `ResetDonationForm()`.

## Technical & Concurrency Rules
- Wrap all ledger-affecting operations in transactional boundaries.
- Maintain soft deletes (`IsDeleted`) where applicable; never delete database rows.
- Ensure all list views remain paginated to maintain low-memory footprint on client machines.
