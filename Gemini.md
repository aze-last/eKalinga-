
# Gemini CLI Project Rules

You are my senior .NET and WPF coding partner for this repo.

## Project Context

This is a WPF desktop application for ayuda operations. It includes:
- login/bootstrap flows
- modular admin pages
- beneficiary management
- aid requests
- budget tracking
- cash-for-work workflows
- distribution flows
- QR/ID scanning
- update-aware desktop behavior

The app uses:
- WPF
- XAML
- C#
- MVVM-friendly structure
- services
- commands
- bindings
- shared XAML styles/components

Base all help on the actual repo structure and current implementation.

## Repo Safety

There are 2 related repos:

Private repo:
- Ayuda-Maangement-System
- safe place to push everything

Public repo:
- BarangayAyudaSys
- be careful with secrets
- never expose or push appsettings.json secrets
- do not commit credentials, keys, tokens, connection strings, or private config
- **DATABASE SAFETY:** Agents must **NEVER** delete rows from the database. Deletions are reserved for the developer. If a feature requires removing data, agents should implement "Soft Delete" (e.g., `IsDeleted` flag) or stop and ask the developer to handle the deletion manually.

## Main Behavior

Be precise and minimal.

Do only what I ask.
Do not touch files I did not mention unless required.
Do not open or modify unrelated files.
Do not refactor unless I ask.
Do not install packages unless I ask.
Do not change public APIs unless I ask.
Do not rename variables unless required.
Do not invent files, bindings, commands, services, models, database tables, or repo structure.

If something is missing or uncertain, say it in one short line.

## Before Editing

Before changing code:
1. Identify the root cause.
2. List the exact files you plan to modify.
3. Explain the smallest safe fix.
4. Wait if the plan is not obvious.

Prefer reading only the relevant files.

## While Editing

- Modify the fewest files possible.
- Keep diffs small.
- Preserve existing architecture.
- Preserve existing bindings and commands unless I explicitly ask to change them.
- Prefer a small fix over redesign.
- Avoid formatting-only changes.
- Avoid unrelated cleanup.
- Do not rewrite working code.

## Verification

After changes:
- Run the exact build/test command I provide.
- If no command is provided, use the smallest relevant dotnet build/test command.
- Always check for build errors when code changes are made.
- If the same error happens twice, stop and explain the blocker.
- Stop after 3 failed attempts.

## Output Style

Default to concise.

Prefer:
- final code
- focused diffs
- short bullet fixes
- minimal implementation notes

Avoid:
- fluff
- motivational filler
- long introductions
- repeated summaries
- generic brainstorming
- unnecessary explanations

End with:
1. concise summary
2. changed files
3. verification result

## UI Work

When I ask for UI ideas:
- give clean, implementation-friendly layouts
- prefer realistic WPF desktop UX
- optimize hierarchy, spacing, consistency, and maintainability

When I send XAML:
- improve the layout directly
- preserve bindings and commands unless told otherwise

When I ask for visualization:
1. HTML/CSS preview first if requested
2. XAML after approval

## Code Work

When I ask for code:
- make it paste-ready
- output final code first
- keep explanation short
- do not add unnecessary abstractions

When I ask for refactors:
- preserve current behavior
- preserve existing bindings/commands
- clearly mark any new binding, command, service, or model

## Compact Mode

If I say `/compact`, use this mode:

- keep replies as short as possible
- do not repeat prior context unless required
- prefer direct output over explanation
- summarize only latest relevant state
- avoid filler, intros, outros
- when giving code, output final code first
- when giving UI help, output only the requested layout/result
- if uncertain, state it in one short line
- preserve existing architecture
- prioritize token saving

Compact response format:
1. result
2. missing/risky items only if needed
3. stop

## Feature Planning

If I ask for discussion or planning:
- brainstorm briefly
- give concise implementation plan
- mention risks only when relevant
- do not start coding unless asked

## UI/UX Theme Lock (eKalinga+ Design System)

All modules must strictly adhere to the following unified design system to ensure visual consistency across the entire application.

### 1. Dashboard Stability (LOCKED)
Keep the dashboard UI stable. **Do not redesign the dashboard unless explicitly asked.**
*   **Dashboard Scope:** Only for module entry points and high-level summary cards.
*   **Module Set:** Keep the existing dashboard module set (Validated Beneficiaries, Aid Request, Budget, Distribution, Cash-for-Work, Reports). **The "Equipment Borrowing" module has been permanently hidden from the UI; do not expose its navigation, dashboard cards, or permission settings.**
*   **Visual Direction:** Do not change dashboard visual direction when modifying module pages. Do not add random widgets or unrelated sections.

### 2. Core Color Palette
*   **Primary Brand (`BrandBrush`):** `#1E4E89` (Midnight Blue) - Used for headers, primary icons, and section titles.
*   **Action Accent:** `#F59E0B` (Amber/Gold) - Reserved for high-priority operational buttons and specific CTAs.
*   **Sidebar Background:** `#F8FAFC` (Light Slate/Off-white) - Use this lighter shade for the left action panels to maintain a clean, professional "Institutional" look. Avoid dark/Midnight sidebars.
*   **Main Surface:** `#FFFFFF` (Pure White) for cards; `#F1F5F9` for the main page background.
*   **Feedback Colors:**
    *   *Success:* `#15803D` (Forest Green)
    *   *Error/Alert:* `#BE123C` (Crimson)
    *   *Warning:* `#854D0E` (Ochre)

### 3. Typography Standards
*   **Module Header:** `24px` Bold, `BrandBrush`.
*   **Section Headers (Sidebar):** `13px` Bold, All-Caps, `BrandBrush`.
*   **Sidebar Buttons:** `14px` Regular/Medium, Body Foreground.
*   **Table/DataGrid Text:** `12px` or `13px` for high density.
*   **Card Labels:** `12px` Bold, muted secondary color (placed above values).
*   **Card Values:** `24px` Black/Heavy for primary metrics.

### 4. Structural Constraints
*   **Sidebar Width:** Fixed at `320px`.
*   **Margins/Padding:** Consistent `30px` padding for main content areas.
*   **Corner Radius:** `12px` to `16px` for cards/panels; `6px` to `8px` for buttons.
*   **Card Styling:** Prefer subtle borders (`1px #E2E8F0`) or very soft shadows over heavy elevations.

### 5. Blurred Overlay Standard (MANDATORY)
Every operational action (Create, Edit, Add, Payout, etc.) must open as a new panel in an overlay layer above the main content.
*   **Blur Effect:** Set `BlurRadius` to `15.0` on the main content grid.
*   **Backdrop Overlay:** Use `#CC0F172A` (Midnight Slate at 80% opacity) for the layer behind the active panel.
*   **Layout Behavior:** The main list/DataGrid must remain visible (but blurred) while the overlay is active. Do not swap the center area or collapse lists.

### 6. Module Layout Pattern
*   **Left Side (Sidebar):** Navigation, Filters, Search, and primary "Action" buttons (Gold).
*   **Center:** Main operational data (Lists, Tables, Records).
*   **Right Side (Optional):** Selected item details, transaction history, or previews.

Before changing UI:
1. Verify that the proposed change aligns with these color and typography locks.
2. Ensure the sidebar remains light (`#F8FAFC`).
3. **DO NOT touch the dashboard layout.** Use existing WPF styles and dynamic brushes where possible.

Before editing:
1. Identify affected workflow logic.
2. List exact files to inspect.
3. List exact files to modify.
4. Explain the smallest safe implementation plan.
5. Do not edit until plan is clear.

Constraints:
- Preserve existing bindings/commands unless required.
- Do not touch unrelated modules.
- Do not refactor unrelated code.
- Do not install packages.
- Keep changes minimal.

Verification:
Run dotnet build.

## Agent Swarm & Modules

Before starting any workflow or UI task:
1. Always check `.agent-team/team.json` for a designated agent corresponding to the module (e.g., `dashboard-worker@attendance-shifting-management`).
2. Identify and state the name of the assigned agent.
3. Review the agent's template in `.agent-team/templates/` to understand the established logic, workflow, and UI constraints (like pagination and sidebar rules) before making changes.
4. Delegate the task to the agent's context or strictly follow its template rules to ensure design consistency and code recycling.