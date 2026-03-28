# Project Distribution Workflow Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add project-based ayuda distribution with approved-beneficiary membership, one-claim-per-project enforcement, and QR scanner claim support.

**Architecture:** Reuse `AyudaProgram` as the project/program aggregate, extend it with distribution fields, and add two new persistence concepts: `project membership` and `project claim`. Integrate the new flow into the existing scanner session gateway by adding a distribution scanner mode tied to a selected project.

**Tech Stack:** C#/.NET 9, WPF, EF Core, MySQL, xUnit

---

### Task 1: Extend Persistence Model

**Files:**
- Modify: `Models/BudgetingModels.cs`
- Modify: `Data/AppDbContext.cs`
- Modify: `Data/RuntimeSchemaBootstrapper.cs`
- Modify: `Data/Migrations/AppDbContextModelSnapshot.cs`
- Create: `Data/Migrations/<timestamp>_AddProjectDistributionWorkflow.cs`
- Create: `Data/Migrations/<timestamp>_AddProjectDistributionWorkflow.Designer.cs`
- Test: `AttendanceShiftingManagement.Tests/StartupMigrationTests.cs`

**Step 1: Write the failing test**

Add a migration/model snapshot test that asserts:
- `AyudaProgram` exposes distribution fields
- `AyudaProjectBeneficiary` exists
- `AyudaProjectClaim` exists
- `ScannerSession` includes project linkage for distribution mode

**Step 2: Run test to verify it fails**

Run: `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter StartupMigrationTests`

Expected: FAIL because the new entities/fields do not exist yet.

**Step 3: Write minimal implementation**

Add:
- `AssistanceType`, `UnitAmount`, `ItemDescription`, `StartDate`, `EndDate`, `BudgetCap`, `DistributionStatus` to `AyudaProgram`
- `AyudaProjectBeneficiary` entity with project ID + staging/identity fields
- `AyudaProjectClaim` entity with project ID + staging/identity fields + claimed metadata
- `ScannerSession.AyudaProgramId`
- matching `DbSet<>` registrations and runtime schema bootstrap logic
- migration files

**Step 4: Run test to verify it passes**

Run the same filtered test command.

**Step 5: Commit**

Commit after the persistence layer and migration test are green.

### Task 2: Add Distribution Domain Service

**Files:**
- Create: `Services/ProjectDistributionService.cs`
- Test: `AttendanceShiftingManagement.Tests/ProjectDistributionServiceTests.cs`

**Step 1: Write the failing test**

Add tests for:
- attaching approved beneficiaries to a project
- blocking duplicate membership
- returning qualification success for an included beneficiary
- returning failure for a non-member
- recording one claim per project
- blocking duplicate claims for the same beneficiary/project

**Step 2: Run test to verify it fails**

Run: `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter ProjectDistributionServiceTests`

Expected: FAIL because the service and model behavior do not exist yet.

**Step 3: Write minimal implementation**

Implement service methods for:
- create/update distribution-ready project metadata
- add/remove beneficiaries from a project
- lookup project membership by beneficiary identity
- record project claim with duplicate protection
- return project claim history summary

**Step 4: Run test to verify it passes**

Run the same filtered test command.

**Step 5: Commit**

Commit after domain rules are green.

### Task 3: Add Distribution Scanner Support

**Files:**
- Modify: `Models/ScannerSession.cs`
- Modify: `Services/ScannerSessionService.cs`
- Modify: `Services/LocalScannerGatewayService.cs`
- Test: `AttendanceShiftingManagement.Tests/ScannerSessionServiceTests.cs`
- Create: `AttendanceShiftingManagement.Tests/LocalScannerGatewayDistributionSourceTests.cs`

**Step 1: Write the failing test**

Add tests for:
- creating a distribution scanner session with project ID
- preserving `AyudaProgramId` in session result/state
- source-level checks that the gateway includes a distribution claim endpoint and project-aware lookup/claim handling

**Step 2: Run test to verify it fails**

Run: `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter "ScannerSessionServiceTests|LocalScannerGatewayDistributionSourceTests"`

Expected: FAIL because distribution mode and endpoints do not exist.

**Step 3: Write minimal implementation**

Add:
- `ScannerSessionMode.Distribution`
- distribution session creation in `ScannerSessionService`
- project-aware lookup response in `LocalScannerGatewayService`
- claim endpoint that records one project claim via `ProjectDistributionService`
- scanner page actions for `Mark as Received`

**Step 4: Run test to verify it passes**

Run the same filtered test command.

**Step 5: Commit**

Commit after scanner integration is green.

### Task 4: Add Desktop Distribution UI

**Files:**
- Create: `ViewModels/ProjectDistributionViewModel.cs`
- Create: `Views/ProjectDistributionPage.xaml`
- Create: `Views/ProjectDistributionPage.xaml.cs`
- Modify: `ViewModels/BarangayMainViewModel.cs`
- Modify: `Views/MainWindow.xaml`
- Modify: `ViewModels/BudgetViewModel.cs`
- Modify: `Views/BudgetPage.xaml`
- Test: `AttendanceShiftingManagement.Tests/ProjectDistributionPageBindingTests.cs`
- Test: `AttendanceShiftingManagement.Tests/BarangayMainViewModelSourceTests.cs`

**Step 1: Write the failing test**

Add tests that assert:
- a `Distribution` sidebar entry exists
- the new page binds project selection, recipient membership controls, scanner session controls, and claim list
- budget page shows the extended project/program fields needed for distribution setup

**Step 2: Run test to verify it fails**

Run: `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter "ProjectDistributionPageBindingTests|BarangayMainViewModelSourceTests|BudgetPageBindingTests"`

Expected: FAIL because the page/bindings do not exist yet.

**Step 3: Write minimal implementation**

Implement:
- project list and detail editor
- approved-beneficiary search/add to project
- membership list and remove action
- project claim history list
- distribution scanner session generation + QR display
- sidebar navigation entry
- budget page fields for distribution metadata

**Step 4: Run test to verify it passes**

Run the same filtered test command.

**Step 5: Commit**

Commit after UI is green.

### Task 5: End-to-End Verification

**Files:**
- Modify as needed: any touched production/test files

**Step 1: Run focused tests**

Run:
- `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter ProjectDistributionServiceTests`
- `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter "ScannerSessionServiceTests|LocalScannerGatewayDistributionSourceTests"`
- `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore --filter "ProjectDistributionPageBindingTests|BudgetPageBindingTests|BarangayMainViewModelSourceTests|StartupMigrationTests"`

Expected: PASS

**Step 2: Run full verification**

Run:
- `dotnet build .\AttendanceShiftingManagement.sln -c Release --no-restore`
- `dotnet test .\AttendanceShiftingManagement.Tests\AttendanceShiftingManagement.Tests.csproj -c Release --no-restore`

Expected: build succeeds, tests pass

**Step 3: Apply migration locally if build/test are green**

Run:
- `dotnet ef database update --project .\AttendanceShiftingManagement.csproj --startup-project .\AttendanceShiftingManagement.csproj --configuration Release`

Expected: migration applies successfully

**Step 4: Final review**

Verify:
- one-claim-per-project rule works
- scanner lookup returns project qualification state
- desktop page can create project membership and scanner session
- no unrelated files were reverted

**Step 5: Commit**

Commit after full verification is green.
