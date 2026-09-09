# Reports Worker

You are the designated agent for the Reports module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility is to query, aggregate, preview, and export operational and financial reports across all ayuda modules (Budget Utilization, Distribution Claims, Cash-for-Work/Seminars, and Admin Activity Audits).

Allowed files:
- `Views/ReportsPage.xaml`
- `Views/ReportsPage.xaml.cs`
- `ViewModels/ReportsViewModel.cs`
- `Services/ReportsService.cs`
- `Services/ReportDocumentService.cs`
- `Services/ReportPdfExportService.cs`
- `AttendanceShiftingManagement.Tests/ReportsPageBindingTests.cs`
- `AttendanceShiftingManagement.Tests/ReportDocumentServiceTests.cs`

## Core Business Logic & Workflows (Source of Truth)

### 1. Supported Report Types (`ReportsReportType`)
1. **Budget Utilization:** Analyzes budget caps versus actual released amounts, remaining balances, and threshold alerts across programs.
2. **Distribution Claims:** Detailed claims log capturing beneficiary details, unit amounts/items, claim timestamps, and releasing users.
3. **Cash-for-Work & Seminars:** Participant rosters, daily attendance records (Present/Absent/Pending), wage rates, and total payouts. Includes standalone event attendance sheet snapshots (`BuildCashForWorkAttendanceSheetSnapshotAsync`).
4. **Admin Activity Audit:** Immutable audit trail tracking Create/Edit/Status changes by Admins and SuperAdmins.

### 2. Snapshot Architecture (`ReportsSnapshot`)
Each report query generates a structured snapshot containing:
- **Metadata:** Report Title, Subtitle, Date Range label, Program label, and Suggested Orientation (`Portrait` or `Landscape`).
- **Executive Summary Highlights (`Highlights`):** Key narrative bullet points summarizing performance and flags.
- **KPI Metrics (`Metrics`):** Summary cards (Total Disbursed, Participant count, Remaining budget, etc.).
- **Dynamic Table (`DataTable`):** Structured rows with typed columns for high-density preview.

### 3. Document Generation & Export Pipelines
- **CSV Export:** Direct CSV export of raw tabular rows.
- **FlowDocument Builder (`ReportDocumentService`):** Compiles print-ready documents with municipality branding/logo, metadata header, KPI summary table, executive summary bullets, detailed tabular grid, and standard signature lines ("Prepared by / Reviewed by / Approved by").
- **PDF Export (`ReportPdfExportService`):** Converts the report snapshot directly to a formatted PDF file.
- **Print Preview:** Integrates with system print dialogs.

## UI/UX & Layout Architecture

### 1. Left Sidebar (Filter & Navigation Panel)
- **Report Template Selector:** Navigate between the 4 standard report types with descriptive help cards.
- **Date Range Filters:** `DateFrom` and `DateTo` date pickers.
- **Program / Project Filter:** Conditional program dropdown (automatically enabled/disabled based on report relevance).
- **Refresh Action:** Triggers asynchronous snapshot compilation.

### 2. Center Content (Preview & Metrics)
- **Header & Executive Summary:** Displays report title, active date range, and executive summary bullet cards.
- **Metric Cards Row:** High-level KPI metric cards positioned above the detailed data grid.
- **Detailed DataGrid:** Dynamic preview grid rendering the snapshot's `DataTable` rows.
- **Action Toolbar:** Fast export buttons for **Save PDF**, **Export CSV**, and **Print Preview**.

## Technical & Memory Rules
- All report queries must execute asynchronously in background tasks to prevent UI thread freezing during large dataset aggregations.
- Data table generation must utilize streaming or efficient LINQ projections.
- Preserve text/binding properties expected by binding tests (`ReportsReportTypeOption`, `SelectedReportType`, `ProgramFilters`).
