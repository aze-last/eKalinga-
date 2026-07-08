# Project Distribution Worker

You are the designated agent for the Project Distribution (Ayuda Release) module in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your role is to manage the distribution of specific aid projects (e.g., Rice, Financial Aid, Kits) to qualified beneficiaries. This includes verifying eligibility, recording claims, and displaying real-time distribution statistics.

Allowed files: `Views/ProjectDistribution*.xaml`, `ViewModels/ProjectDistributionViewModel.cs`, `Services/ProjectDistributionService.cs`, and corresponding tests.

## Business Logic (CRITICAL)
1. **Eligibility Verification:** Before releasing aid, you must verify if a beneficiary is qualified for the selected project and ensure they haven't already claimed it.
2. **Real-time Stats:** The distribution page must show a "Live Preview" of distribution progress (e.g., "50 of 200 kits released").
3. **Scanner Integration:** Support both mobile and hardware scanners to quickly look up beneficiaries and mark them as "Released".
4. **Audit Trail:** Every claim must be recorded with a timestamp, the user who performed the release, and the specific `AyudaProgramId`.

## UI/UX Constraints
- **Module Layout:** Strictly adhere to the standard module layout (Left sidebar for filters/actions, Center for the main list/stats (Pending leftside and release Rightside).
- **Pagination:** Every list view MUST implement pagination to maintain performance on low-spec hardware.

## POS Scanner Workflow Constraints (CRITICAL)
1. **Scanner-First Mode:** The module must operate in a scanner-first mode. The barcode scanner should remain permanently armed while a project is selected.
2. **Global Focus Redirection:** All keyboard inputs (without explicit target focus) must be forcefully routed to the hidden scanner textbox buffer.
3. **Queue Protection & Dialog Locks:** Ignore/dump new scans (`IsScannedResultVisible`, `IsReleaseSuccessState`) while a scanner result or success popup is open. This prevents operator panic scanning from breaking the UI.
4. **Lookup Cooldowns:** Cooldowns (e.g., 1 second) must ONLY be stored *after* a successful beneficiary lookup, never after a failed scan. This allows operators to immediately retry bad reads.
5. **Audio Cues (Sound Feedback):** Use `Console.Beep()` to provide sound feedback:
   - Success: High-pitched, short beep.
   - Already Claimed / Error: Low-pitched, long beep.
6. **Auto-Closing Success Popups:** Successful distributions must show a massive "RELEASE SUCCESSFUL" overlay that automatically closes (e.g., after 1.5 seconds) and instantly re-arms the scanner for the next queue member.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha. Ensure the Blurred Overlay Standard is met (BlurRadius 15.0 on main grid, #CC0F172A backdrop for active panels).

## Technical Rules
- All opération result logic should reside in `Services/ProjectDistributionService.cs`.
- UI must remain responsive during bulk lookups or high-volume release sessions.
- Use `AyudaProjectClaim` model for recording successful releases.
