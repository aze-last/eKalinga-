
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

## UI/UX Theme Lock

Keep the dashboard UI stable.

Dashboard rules:
- Do not redesign the dashboard unless explicitly asked.
- Dashboard is only for module entry points and high-level summary cards.
- Keep the existing dashboard module set:
  - Validated Beneficiaries
  - Aid Request
  - Budget
  - Distribution
  - Cash-for-Work
  - Reports
- Do not add random widgets, recent activity feeds, unrelated sections, or experimental layouts to the dashboard.
- Do not change dashboard visual direction when modifying module pages.

Module page rules:
- All operational module actions must stay inside the left sidebar or left action panel.
- The center area must show the main list, table, records, or module information.
- Details, preview, selected item info, history, and summaries should appear in the center/right content area.
- Do not scatter primary actions across the whole screen.
- Do not move core operations into dashboard tiles.
- Do not redesign module structure unless I explicitly request a redesign.

Default module layout:
- Left side:
  - filters
  - search
  - create/add buttons
  - workflow actions
  - scanner/session controls
  - module navigation/actions
- Center:
  - main list/table
  - selected module data
  - records
  - information cards
- Right side only when needed:
  - selected item details
  - history
  - preview
  - audit/summary

Visual consistency rules:
- Reuse existing WPF styles, brushes, spacing, buttons, cards, and typography where possible.
- **Blurred Overlay Standard:** Every operational action (Create, Edit, Add, Payout, etc.) must open as a new panel in an overlay layer above the main content. The main content (DataGrid/List) must remain visible but be blurred (`BlurEffect`) while the overlay is active. Do not swap the center area or collapse the main list to show operational forms.
- Do not introduce a new visual style per feature.
- Do not change colors, spacing system, button style, card style, or typography unless I ask for a theme update.
- If a page needs a new component, match the existing dashboard/module style.
- Prefer clean desktop admin layout over mobile/web-style experiments.

Before changing UI:
1. Identify whether the change affects dashboard or module page.
2. Keep dashboard unchanged unless requested.
3. For modules, preserve the left-sidebar + center-content structure.
4. List exact XAML files to modify before editing.

When editing XAML:
- Preserve existing bindings and commands.
- Do not remove working bindings.
- Do not rename commands/properties unless required.
- Do not make formatting-only changes.
- Keep layout changes minimal and consistent.

UI/UX instructions when prompting use this Bien. To gemini nevermind this part.

UI constraint:
Keep dashboard UI unchanged.
Apply changes only inside the target module page.
All module operations/actions must stay in the left sidebar/action panel.
The center area must remain for lists, records, and module information.

Before editing:
- identify root cause/design issue
- list exact XAML files to modify
- preserve existing bindings and commands

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