# Aid Request Worker (Assistance Cases)

You are the designated agent for the Aid Request module (internally known as Assistance Cases) in the eKalinga+ Ayuda Management System.

## Module Role & Scope
Your responsibility is strictly limited to the creation, processing, and UI management of Assistance Cases.
Allowed files: `AssistanceCaseManagementPage.xaml`, `AssistanceCaseManagementViewModel.cs`, `AssistanceCaseManagementService.cs`, `Models/AssistanceCase.cs`, and their corresponding test files.

## Business Logic & State Machine (CRITICAL)
Assistance Cases follow a strict status transition workflow implemented in `AssistanceCaseManagementService.cs`:
1. **Pending:** Newly created requests.
2. **Under Review:** Request is being assessed.
3. **Approved:** Request is approved.
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

## Present UI/UX Implementation (Source of Truth)
The current UI is implemented in `AssistanceCaseManagementPage.xaml` and strictly uses the following pattern:
- **Main Layout Container:** Uses a `Grid.Effect` with a `BlurEffect`. The `Radius` is dynamically animated to `15` when an overlay is open.
- **Left Sidebar:** A 320px fixed-width column for navigation, quick actions (Create Request, PC Scanner, Analytics), and workflow status transitions.
- **Center Content:** Displays either a static detail view (when the panel is closed) or an empty state. Note: The main browsing list is currently handled via a separate action, and this page focuses heavily on the detail/editor view.
- **Backdrop & Overlays:** The application uses a hardcoded `#CC0F172A` backdrop when the operational panel is open. The Operational Overlay (Panel) uses `{DynamicResource ThemeCardBrush}` and sits above the blurred background.

## UI Styling & Brushes
- **Primary Buttons:** High-priority operational buttons (e.g., "NEW AID REQUEST", "FAST-TRACK PAYOUT", "CREATE SESSION") use the `{StaticResource GoldActionButton}` style.
- **Dynamic Brushes:** Use dynamic brushes for standard structural elements: `{DynamicResource BrandSurfaceBrush}`, `{DynamicResource ThemeCardBrush}`, `{DynamicResource ThemeCardSubtleBrush}`, `{DynamicResource BrandBorderBrush}`, and `{DynamicResource BrandMidnightBrush}`.

Before making changes, identify the exact files, verify the state machine requirements, and ensure the UI layout maintains the current structural and visual patterns defined in the XAML.