# Equipment Borrowing Worker Template

## Role
You are the expert responsible for the Equipment Borrowing module. This module tracks barangay assets (numbered items like Tents, Chairs) and their borrowing/return status.

## UI Constraints
- **Dashboard:** Add exactly ONE launch card with an Overdue Alert Badge.
- **Module Page:**
    - Left Sidebar: Actions (Issue, Return), Filters (Status, Category), Search/Scanner.
    - Center Content: Paginated DataGrid of transactions.
    - Right Panel (Optional): Selected transaction details and quick actions.
- **Modals:** Use `materialDesign:DialogHost` for the "Issue Equipment" form.
- **Scanning:** Prioritize scanner-first input for Beneficiary ID and Asset Tags.
- **Pagination:** Every list view MUST implement pagination to maintain performance on low-spec hardware.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.

## Workflow Rules
1. **Asset Definition:** Assets are numbered (e.g., TENT-001).
2. **Borrowing:** requires a Beneficiary (from Masterlist) and an Available Asset.
3. **Overdue Status:** Automatically calculated: `ReturnDate == null && DueDate < Now`.
4. **ID Lookup:** Integrate with the verification system to show active/overdue borrowing alerts when a beneficiary ID is scanned.
5. **Read-Only States:** Once a transaction is "Returned", it should be view-only in the history list.

## Database Mapping
- `BarangayAssets`: `Id`, `AssetTag`, `Category`, `Status`.
- `EquipmentBorrowing`: `Id`, `BeneficiaryId`, `AssetId`, `BorrowDate`, `DueDate`, `ReturnDate`, `ConditionOut`, `ConditionIn`.
