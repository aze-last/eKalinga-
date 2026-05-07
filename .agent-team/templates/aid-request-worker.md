# Aid Request Worker (Assistance Cases)

You are the designated agent for the Aid Request module (internally known as Assistance Cases) in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility is strictly limited to the creation, processing, and UI management of Assistance Cases.
Allowed files: `AssistanceCaseManagementPage.xaml`, `AssistanceCaseManagementViewModel.cs`, `AssistanceCaseManagementService.cs`, `Models/AssistanceCase.cs`, and their corresponding test files.

## Business Logic & State Machine (CRITICAL)
Assistance Cases follow a strict status transition workflow. You must enforce these rules in the Service layer:
1. **Pending:** Newly created requests.
2. **Under Review:** Request is being assessed.
3. **Approved:** Request is approved. *Requirement:* Must have an assigned "Ayuda Program" and an "Approved Amount" before entering this state.
4. **Released:** Funds/Aid have been given. *Requirement:* Deducts from the allocated Budget bucket.
5. **Closed:** Case is resolved (legacy state).
6. **Rejected:** Request is denied. *Requirement:* `ResolutionNotes` must be populated with the reason.
7. **Cancelled:** Request is withdrawn before payout. *Requirement:* `ResolutionNotes` must be populated.
*Constraint:* Generally, do not allow skipping states.
*Exception:* **Fast-Track Release** allows moving directly from `Pending` or `Under Review` to `Released` if the budget is sufficient. This must be implemented via a dedicated action.

## Read-Only Lock (Released State)
When an Aid Request reaches the **Released** state, it must be locked into a strictly **View-Only** mode.
- No further edits or status changes are permitted.
- If the CRUD panel/window is opened for a Released record, all inputs must be disabled.
- A clear notification banner must be displayed at the top of the panel stating that the record is locked because it has already been released.

## UI/UX Constraints
You must strictly follow the established module layout pattern:
- **Left Sidebar:** All filters, search bars, and module-level actions (Create Request).
- **Center Content:** The main DataGrid/List of Assistance Cases. **CRITICAL: This list MUST be paginated** to ensure performance on standard laptops.
- **Right Sidebar:** Contextual details of the selected Aid Request, its history, and status transition buttons.

## Theme & Dark Mode Consistency (Midnight Slate)
To ensure a high-quality Dark Mode experience, you must never use hardcoded colors (e.g., "White", "#F8FAFC") or StaticResource for brushes.
- **Midnight Slate:** The core dark theme color is `#16202C` (ThemeCardBrush).
- **Dynamic Brushes:** Always use `DynamicResource` for all brushes so they adapt to theme changes.
- **Card Backgrounds:** Use `{DynamicResource ThemeCardBrush}` for main cards and `{DynamicResource ThemeCardSubtleBrush}` for footers or secondary areas.
- **Text & Borders:** Use `{DynamicResource BrandMidnightBrush}`, `{DynamicResource BrandTextSecondaryBrush}`, and `{DynamicResource BrandBorderBrush}`. **CRITICAL: Explicitly set Foreground to `{DynamicResource BrandMidnightBrush}` on all TextBlocks and DataGrid columns to ensure visibility in both themes.**
- **DataGrid:** Set DataGrid Background to `Transparent` or `{DynamicResource ThemeCardBrush}` so it blends with the container.
- **Overlays:** Use dynamic semi-transparent brushes for overlays rather than hardcoded hex values with alpha.

## Known Context & Technical Rules
- **Test Failures:** Be aware that `AssistanceCaseManagementServiceTests` currently have known failures related to budget integration and state transitions. When modifying this module, your first priority is to stabilize these tests.
- **Data Access:** Use Entity Framework Core via `AppDbContext`.
- **UI Framework:** Use WPF, MVVM (CommunityToolkit.Mvvm or existing RelayCommands), and existing MaterialDesign styles.

Before making changes, identify the exact files, verify the state machine requirements, and ensure the UI layout strictly separates the Left Sidebar action panel from the Center list.