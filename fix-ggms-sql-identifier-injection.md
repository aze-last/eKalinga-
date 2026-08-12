# Fix: SQL Identifier Injection in GGMS Query Builders

## Context

Two services build raw SQL by interpolating table names from `appsettings.json` config
without escaping backtick characters. A backtick in a configured table name breaks out
of the MySQL identifier context (SCS0002 / CA2100).

Other services in this repo (`DatabaseTableReviewService`, `LocalBackupService`,
`RuntimeSchemaBootstrapper`) already have a local `EscapeIdentifier` helper that strips
backticks. Apply the same pattern to the two affected services.

---

## Fix 1 — `Services/GgmsBudgetSyncService.cs`

### Step 1: Add `EscapeIdentifier` alongside the existing `NormalizeTableName` method

```csharp
private static string EscapeIdentifier(string identifier) =>
    identifier.Replace("`", "");
```

### Step 2: Wrap every interpolated table name in the three query builders

**`BuildBudgetAllocationQuery`** (around line 107):
```csharp
// before
var allocation = NormalizeTableName(allocationTable, "budget_allocations");
var office     = NormalizeTableName(officeTable, "tbl_offices");

// after
var allocation = EscapeIdentifier(NormalizeTableName(allocationTable, "budget_allocations"));
var office     = EscapeIdentifier(NormalizeTableName(officeTable, "tbl_offices"));
```

**`BuildLegacyAllocationQuery`** (around line 129):
```csharp
// before
var allocation = NormalizeTableName(allocationTable, "officeallocations");
var office     = NormalizeTableName(officeTable, "tbl_offices");

// after
var allocation = EscapeIdentifier(NormalizeTableName(allocationTable, "officeallocations"));
var office     = EscapeIdentifier(NormalizeTableName(officeTable, "tbl_offices"));
```

**`BuildSpentAmountQuery`** (around line 151):
```csharp
// before
var tableName = NormalizeTableName(configuredTableName, "consolidated_transactions");

// after
var tableName = EscapeIdentifier(NormalizeTableName(configuredTableName, "consolidated_transactions"));
```

---

## Fix 2 — `Services/GgmsProjectSyncService.cs`

### Step 1: Add `EscapeIdentifier` alongside the existing `NormalizeTableName` method

```csharp
private static string EscapeIdentifier(string identifier) =>
    identifier.Replace("`", "");
```

### Step 2: Wrap the interpolated table name in `BuildProjectDetailsQuery`

**`BuildProjectDetailsQuery`** (around line 229):
```csharp
// before
var table = NormalizeTableName(projectDetailsTable, "project_details");

// after
var table = EscapeIdentifier(NormalizeTableName(projectDetailsTable, "project_details"));
```

---

## Constraints

- Do **not** change method signatures, SQL query text, parameter names, or any other logic.
- Do **not** modify tests — the existing `GgmsBudgetSyncServiceTests` and
  `GgmsProjectSyncServiceTests` assert on the query string content and must still pass.
- Keep diffs minimal; touch only the four call sites and add the two private helpers.

## Verification

After applying, run:

```
dotnet build AttendanceShiftingManagement.sln
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~GgmsBudgetSyncServiceTests"
dotnet test AttendanceShiftingManagement.Tests --filter "FullyQualifiedName~GgmsProjectSyncServiceTests"
```

All tests must pass and the build must be clean.
