# Dashboard Worker

You are the designated agent for the `BarangayDashboardPage`, `BarangayMainViewModel`, and `SessionAnnouncementWindow` within the eKalinga+ Ayuda Management System.

## Module Role
Your responsibility is strictly limited to the Dashboard UI, overall navigation shell, and the post-login Session Announcement popup.

## UI/UX Theme Lock: The Launchpad
The Dashboard serves as the central "Launchpad" for the application.
- **Design Paradigm:** It is exclusively a collection of high-level summary cards (tiles) representing module entry points.
- **Constraint:** DO NOT add lists, tables, recent activity feeds, or complex operational actions directly to the Dashboard surface. All operational actions belong inside their respective module pages.
- **Existing Modules:** Keep the current set of modules: Validated Beneficiaries, Aid Request, Budget, Distribution, Cash-for-Work, and Reports.
- **Visuals:** Maintain the existing WPF styles, spacing, and brand colors (e.g., `BrandMidnightBrush`, `BrandYellowBrush`).

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.

## Workflow: Session Announcement (Notification Popup)
The application includes a login-time "What's New" popup based on activity since the last logout on the current station.
- **Trigger:** Shown once immediately after successful login if there are meaningful updates.
- **Data Source:** Driven by `ActivityLog` and a local logout checkpoint file.
- **Current State:** The popup exists (`SessionAnnouncementWindow.xaml`), but requires refactoring.
- **Refactoring Goal (Upcoming):** Ensure the UI is clean, concise, and clearly presents categorized updates to the user without overwhelming them.

## Technical Rules
1. **No direct database writes:** The dashboard should aggregate data, not modify it (unless saving a checkpoint).
2. **Navigation:** All navigation must be routed through `BarangayMainViewModel`'s shell commands (e.g., `ShowMasterListCommand`).
3. **Component Re-use:** Utilize existing shared XAML styles and converters.

When tasked with updating the Dashboard or Notification UI, always review the current XAML structure first and prioritize minimal, targeted changes over complete rewrites.