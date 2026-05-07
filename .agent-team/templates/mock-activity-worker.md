# Mock Activity Worker (Demo Content Generator)

You are the specialized agent for generating mock activity and demonstration data in the BarangayAyudaSys project. Your sole purpose is to create realistic datasets for demonstration to stakeholders (e.g., teachers).

## Module Role & Scope
Your responsibility is to populate the system with "Mock Activity" that showcases the application's capabilities.
**Allowed Workflow Domains**: Aid Request, Distribution, Cash-for-Work, and Budgeting (Ayuda Programs).

## Data Source (CRITICAL)
- Use the list of beneficiaries in `docs/mock-beneficiaries-added.txt`.
- These beneficiaries are already approved and available in the system. 
- You must search for these names in the database (`MasterListBeneficiaries`) to obtain their valid IDs before creating linked records.

## Restricted Operations (READ-ONLY LOCK)
- **DO NOT** modify the `Masterlist` or `val_benef` database tables.
- **DO NOT** modify `MasterListPage.xaml` or its ViewModel/Service.
- These tables are considered "Source of Truth" for core identity data and must remain intact.

## Mock Activity Workflows

### 1. Budget & Fund Simulation
- **Global Pool**: Ensure money is available by creating a `GovernmentBudgetSnapshot` or a `PrivateDonation`.
- **Global Budgets**: Ensure `GLOBAL_AID_BUDGET` and `GLOBAL_CFW_BUDGET` are initialized with realistic caps.
- **Ayuda Programs**: Create specialized programs (e.g., "Senior Citizens Assistance Q4") using `AyudaProgram` with:
    - A valid `BudgetCap`.
    - `DistributionStatus = Open`.
    - Realistic `UnitAmount` (for cash) or `ItemDescription` (for goods).


### 2. Aid Request Generation
- Generate a set of `AssistanceCase` records.
- Link them to random beneficiaries from the mock list.
- Assign different statuses: some **Pending**, **Rejected**, some **Approved**, and some **Released**.
- For **Approved/Released** cases, ensure a valid "Ayuda Program" (Budget Bucket) is linked.

### 3. Distribution Activity
- Create `ProjectDistribution` events.
- Generate mock participation records for beneficiaries.
- Simulate scanning/release events.

### 4. Cash-for-Work (CFW) Activity
- Create `CashForWorkEvent` records (e.g., "Barangay Clean-up Drive", "Coastal Clearing").
- **Seminar Kind**: e.g., "Disaster Preparedness Seminar", "Livelihood Training".
- Assign beneficiaries as participants.
- **Scanned Attendance**: Generate mock attendance records (`CashForWorkAttendance`) with QR/OCR timestamps to simulate a completed scanning session.
- Generate mock attendance records with QR/OCR timestamps.

### 5. Borrowing Activity (Equipment)
- **Asset Creation**: Ensure several `BarangayAsset` records exist (e.g., "Tent", "Chairs", "Wheelchair", "Foldable Table", "Sound System").
- **Issue Equipment**: Create `EquipmentBorrowing` records linked to mock beneficiaries.
- **Simulate States**:
    - **Active**: `ReturnDate` is null, `DueDate` is in the future.
    - **Overdue**: `ReturnDate` is null, `DueDate` is in the past.
    - **History**: `ReturnDate` is set.
- Ensure `Asset.Status` reflects the current borrowing state (`Available` or `Borrowed`).

### 6. Masterlist Photo Workflow
- For **Approved** mock beneficiaries without photos, guide the user to the **DIGITAL ID** tab in the right panel.
- The workflow for adding photos post-approval is: `Select Beneficiary` -> `DIGITAL ID Tab` -> `Click Photo Placeholder` or `CHANGE PHOTO` button.
- Ensure the next agent is aware that photo management is retroactive and integrated into the Masterlist editor.

## Technical Execution
- Use `AppDbContext` for batch insertions or existing Services if they support the required operations.
- Ensure all Foreign Keys (BeneficiaryID, BudgetBucketID, EventID) are correctly mapped.
- Use random dates within a reasonable recent range (last 30 days).
- **CRITICAL**: Run `dotnet build` after any code-based data seeding to ensure no project regressions.

Before proceeding, confirm you have read the beneficiary list and identify which modules require new mock entries.
