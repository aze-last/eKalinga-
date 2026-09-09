# GGMS Transaction Worker

You are the designated agent for the GGMS Consolidated Transactions module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility covers cross-system transaction logging, synchronization between eKalinga+ (AMS) and the central municipal Government Grant Management System (GGMS) MySQL database (`consolidated_transactions`), offline queue persistence, and the transaction audit portal.

Allowed files:
- `Views/GgmsConsolidatedTransactionPage.xaml`
- `Views/GgmsConsolidatedTransactionPage.xaml.cs`
- `ViewModels/GgmsConsolidatedTransactionViewModel.cs`
- `Services/GgmsConsolidatedTransactionService.cs`
- `Models/GgmsConsolidatedTransaction.cs`
- `AttendanceShiftingManagement.Tests/GgmsConsolidatedTransaction*.cs`

## Core Business Logic & Workflows (Source of Truth)

### 1. Cross-Module Sync Hub
The GGMS transaction service acts as the central destination whenever disbursements occur across operational modules:
- **Aid Requests:** Records individual assistance releases via `TryWriteAssistanceCaseReleaseAsync` (`ProjectName = "Aid Request"`).
- **Project Distributions:** Records single and bulk distribution claims via `TryWriteProjectDistributionClaimAsync` / `TryWriteBulkProjectDistributionClaimsAsync` (`ProjectName = "Project Distribution"`).
- **Cash-for-Work & Seminars:** Records batch wage payouts via `TryWriteCashForWorkReleaseAsync` (`ProjectName = "Cash For Work"` or `"Seminar"`).

### 2. Two-Tier Identity Mapping
- **`project_code`:** Stable, internal AMS-prefixed project code (e.g., `AMS-000001`, `AMS-PD-000001`, `AMS-CFW-000001`, `AMS-SEM-000001`), preserved across all disbursements.
- **`project_details_id`:** External GGMS grant mapping (e.g., `OPP-2026-0006`) populated when the project was spawned from a mirrored GGMS sub-allocation.
- **Beneficiary Attributes:** Extracts and normalizes `beneficiary_id`, `civil_registry_id`, split name parts (`first_name`, `middle_name`, `last_name`), `barangay` (parsed from address), and `household_no`.

### 3. Offline Resilience & Queue Flushing
- **Connectivity Detection:** Listens to `ConnectivityService.Instance.ConnectivityChanged`.
- **Local Fallback:** When GGMS MySQL is offline/unreachable, transaction payloads are serialized into JSON and stored in SQLite `GgmsPendingTransactionCache`.
- **Automatic Queue Drain:** When connectivity is restored, `FlushPendingTransactionsAsync()` replays all cached entries and clears the queue upon confirmed database insertion.

### 4. Dynamic Column Probing
- Before issuing `INSERT` commands, the service probes `information_schema.COLUMNS` on the GGMS database (`GetOptionalColumnsAsync`) to gracefully handle optional fields (`project_details_id`, `project_name`, `barangay`, `household_no`) without failing on schema variations.

## UI/UX & Layout Architecture

### 1. Header & Metric Overview
- **Header Bar:** Displays live GGMS connection status pill and manual "Sync Now" / reload actions.
- **KPI Summary Cards:** Real-time metrics for Total Synchronized Transactions, Distribution count, Cash-for-Work & Seminar count, and Aid Request count.

### 2. Transaction Grid & Filtering
- **Category Filter Tabs:** "All Transactions", "Project Distribution", "Cash For Work", "Seminar", "Aid Request".
- **Search:** Case-insensitive search across beneficiary names, IDs, project codes, and transaction types.
- **Mandatory Pagination:** Fixed 25 items per page (`_pageSize = 25`) with previous/next controls.
- **Detail Overlay / Inspector:** Slide-out panel displaying full transaction audit metadata, office details, timestamps, and raw amounts.
- **Loading Feedback:** Animated skeleton block states during data fetch.

## Technical & Concurrency Rules
- Wrap multi-row inserts in database transactions (`BeginTransactionAsync`).
- GGMS transactions are strictly append-only audit records; never delete or alter historical rows.
