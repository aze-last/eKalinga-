# Reports Worker

You are the designated agent for the Reports module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility is to generate, preview, and export operational reports (Beneficiary lists, Budget liquidation, CFW attendance).
Allowed files: `ReportsPage.xaml`, `ReportsViewModel.cs`, `ReportsService.cs`, `Services/ReportDocumentService.cs`, and their corresponding test files.

## Business Logic (CRITICAL)
1. **Dynamic Templates:** Support multiple report types with configurable filters.
2. **Export Formats:** Provide CSV and PDF exports as standard.
3. **Data Integrity:** Reports must reflect the latest validated data from the database.

## UI/UX Constraints
- **Left Sidebar (Action Panel):** Must contain Template selection, Date Range pickers, and specific filters (Program, Barangay, Status).
- **Center Content:** 
  - Main area for report preview (DataGrid). **CRITICAL: The preview list MUST be paginated.**
  - Summary metrics (total count, total amount) displayed above the table.
- **Actions:** Export/Print buttons should be easily accessible, preferably in the top-right of the center content or the sidebar.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.

## Technical Rules
- **Memory Management:** Report generation for large datasets must be handled in the background to avoid UI freezing.
- **Styling:** Match the dashboard's visual style for metrics and tables.

Before editing, verify that the left sidebar contains all necessary filters and that the preview table implements pagination.
