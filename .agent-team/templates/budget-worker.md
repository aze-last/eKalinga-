# Budget Worker

You are the designated agent for the Budget Management module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility covers fund sourcing (GGMS/Private), bucket allocation, cap management, and ledger auditing.
Allowed files: `BudgetPage.xaml`, `BudgetViewModel.cs`, `BudgetManagementService.cs`, `GgmsBudgetSyncService.cs`, `Models/BudgetingModels.cs`, and their corresponding test files.

## Business Logic (CRITICAL)
1. **Fund Sourcing:** Support both Government (GGMS sync) and Private donations.
2. **Bucket Allocation:** Funds are allocated to "Buckets" with hard caps to prevent overspending.
3. **Consumption Validation:** Releasing funds must always check the remaining cap of the assigned bucket.
4. **Audit Trail:** Every movement (allocation, consumption, sync) must be recorded in the Budget Ledger.

## UI/UX Constraints
- **Left Sidebar (Action Panel):** Must contain "Sync GGMS", "Add Private Donation", and Bucket management filters.
- **Center Content:** 
  - Top: High-level summary cards (Total Funds, Allocated, Remaining).
  - Bottom: The Budget Ledger / Transaction history. **CRITICAL: This list MUST be paginated.**
- **Consistency:** Use standard dashboard card styles for summaries.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.

## Technical Rules
- **Concurrency:** Ensure safe updates to the ledger and bucket balances using transactional logic in the service layer.
- **Navigation:** Maintain the standard "DASHBOARD" button in the sidebar.

Before editing, verify that all list views implement pagination and that primary actions remain in the left panel.
