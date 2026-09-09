# Project Distribution Worker

You are the designated agent for the Project Distribution (Ayuda Release) module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your role is to execute and manage the field distribution of aid projects (e.g., Cash, Goods, Rice, Kits) to qualified beneficiaries. This includes beneficiary enrollment management, qualification evaluation, barcode/QR scanner processing, claim recording, and real-time distribution queue monitoring.

Allowed files:
- `Views/ProjectDistribution*.xaml`
- `Views/ProjectDistribution*.xaml.cs`
- `ViewModels/ProjectDistributionViewModel.cs`
- `Services/ProjectDistributionService.cs`
- `AttendanceShiftingManagement.Tests/ProjectDistribution*.cs`

## Core Business Logic & Workflows (Source of Truth)

### 1. Project Sourcing (Budget Module Coupling)
- **Spawned in Budget:** Projects (`AyudaProgram`) are created in the Budget module and strictly tied 1:1 to parent funding sources (`SourceDonationId`, `SourceGGMSBudgetId`, or `SourceProjectDetailsId`).
- **Operational Loading:** The Distribution module loads active, non-closed distribution projects for execution. It does not spawn new projects directly.

### 2. Beneficiary Enrollment & Bucketing
- **Masterlist Sourcing:** Beneficiaries are selected individually or in bulk from approved `BeneficiaryStaging` records.
- **3-Bucket State Machine:** Enrolled beneficiaries exist in one of three states (`DistributionBeneficiaryStatus`):
  - **PENDING (Col 2):** Eligible and queued for release.
  - **RELEASED / CLAIMED (Col 0):** Successfully claimed and disbursed.
  - **UNRELEASED / REJECTED (Col 4):** Excluded or unreleased candidates (can be moved back to Pending or Released).
- **Duplicate Prevention:** Prevents enrolling the same beneficiary twice within a single project.

### 3. Household Duplicate Protection
- **Cross-Member Check:** Evaluates `HouseholdVerificationContext` before release.
- **Warning Prompts:** Warns operators if another member of the same household has already claimed or received the same assistance type across projects.

### 4. Claim Recording & Financial Integration
- **Claim Entity:** On release, creates an immutable `AyudaProjectClaim` capturing the snapshot of unit amount, item details, QR payload, and timestamps.
- **Budget Waterfall Ledger:** Automatically calls `BudgetManagementService.RecordReleaseAsync` to log a `BudgetLedgerEntry` that consumes from the project's linked funding envelope.
- **GGMS Consolidated Transactions:** Automatically calls `IGgmsConsolidatedTransactionService.RecordReleaseAsync` to record cross-system transaction sync records.
- **Beneficiary Assistance Ledger:** Updates the global beneficiary aid history via `BeneficiaryAssistanceLedgerService`.

### 5. Live Queue Monitor
- **Secondary Display:** Manages `ProjectDistributionLivePreviewWindow` to broadcast real-time queue states, active recipient names, and live project progress to external client/public monitors.

## POS Scanner Workflow Constraints (CRITICAL)
1. **Scanner-First Mode:** The module operates in a scanner-first mode; the barcode/QR scanner buffer remains armed whenever a project is selected.
2. **Global Focus Redirection:** Unfocused keyboard inputs route to the scanner buffer textbox.
3. **Queue Protection & Dialog Locks:** Ignore/dump new scan inputs while an active result modal or release success popup is open.
4. **Lookup Cooldowns:** Cooldown timers apply ONLY after a successful lookup, allowing immediate retries on bad/failed reads.
5. **Audio Feedback (`Console.Beep`):**
   - Success: High-pitched, short beep (`2000Hz, 150ms`).
   - Already Claimed / Error: Low-pitched, long beep (`800Hz, 300ms`).
6. **Auto-Closing Success Overlay:** Successful releases show a large confirmation overlay that automatically dismisses (1.5 seconds) and instantly re-arms the scanner.

## UI/UX & Layout Architecture
- **Left Sidebar:** Project selection, summary metrics (distributed amount, remaining budget, recipient count), mobile scanner session PINs, and bulk actions.
- **Center Content (3-Column Bucket Layout):**
  - Left (Col 0): **RELEASED / CLAIMED**
  - Center (Col 2): **PENDING (QUEUED)**
  - Right (Col 4): **UNRELEASED / EXCLUDED**
- **Independent Pagination & Filtering:** All three bucket columns and beneficiary pickers MUST implement independent search and pagination.
- **Overlays:** Beneficiary Add/Search panels and Release Confirmation dialogs open as overlays above the main bucket grid.

## Technical & Concurrency Rules
- Support dual local/remote execution via `RemoteWriteExecutionService`.
- Maintain soft deletes (`IsDeleted`) where applicable; never delete database rows.
- Ensure all queries are paginated and memory-capped (`BeneficiaryPickerDisplayLimit`).
