
# Gemini CLI Project Rules

You are my senior Laravel coding partner for this repo.

## Project Context

This project uses Laravel 12 for the application backend and Tailwind CSS v4 for the user interface.

Base all help on the actual repository structure and current implementation. Follow Laravel 12 conventions for routes, controllers, models, migrations, policies, requests, services, Blade views, and tests. Use Tailwind CSS v4 utilities and CSS-first configuration for frontend styling.

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
Do not invent files, routes, controllers, services, models, database tables, or repository structure.

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
- Preserve existing routes, actions, and component interfaces unless I explicitly ask to change them.
- Prefer a small fix over redesign.
- Avoid formatting-only changes.
- Avoid unrelated cleanup.
- Do not rewrite working code.

## Verification

After changes:
- Run the exact build/test command I provide.
- If no command is provided, use the smallest relevant Laravel or frontend verification command.
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
- prefer realistic Laravel web UX
- optimize hierarchy, spacing, consistency, and maintainability

When I send Blade, HTML, or Tailwind CSS:
- improve the layout directly
- preserve existing routes, forms, actions, and data flow unless told otherwise

When I ask for visualization:
1. HTML/Tailwind preview first if requested
2. Blade/Tailwind implementation after approval

## Code Work

When I ask for code:
- make it paste-ready
- output final code first
- keep explanation short
- do not add unnecessary abstractions

When I ask for refactors:
- preserve current behavior
- preserve existing routes, actions, validation, and public interfaces
- clearly mark any new route, controller, service, model, migration, or component

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

## eKalinga+ UI/UX Theme Lock (Laravel 12 + Tailwind CSS v4)

All modules must strictly adhere to this design system to ensure visual consistency across the app. This is a hard lock, not a suggestion — deviating requires the developer's explicit sign-off.

**Scope note:** modules referenced below are limited to the 5 pages actually being built: **Dashboard, Budget, Project Distribution, Reports, GGMS Transactions.** Masterlist, Aid Requests, and Cash-for-Work are out of scope and must not appear in permission lists, navigation, or module references.

### 1. User Management & Permissions

- **SuperAdmin exemption:** only `SuperAdmin` can access "System User Management." SuperAdmins are exempt from all permission restrictions.
- **Self-protection:** a user (including SuperAdmin) cannot delete or deactivate their own currently logged-in account.
- **Admin deletion:** SuperAdmins may soft-delete other accounts, including other SuperAdmin accounts — except themselves.
- **Permission categorization** (checkboxes in the permissions UI, grouped):
    - *MODULES:* Dashboard, Budget, Distribution, Reports, GGMS Transactions.
    - *SYSTEM SETTINGS:* App Database, GGMS Budget Source.
- **Protected settings:** unlocking "App Database" or "GGMS Budget Source" settings requires password re-entry, allowed for both `Admin` and `SuperAdmin`.
- Roles are strictly `SuperAdmin` and `Admin` only — no other role exists.

### 2. Core Color Palette — "Barangay Heraldic" (Green/Amber/Crimson)

Each color has ONE job. Never distribute the three evenly — avoid the "fast-food kiosk" look.

Define these as Tailwind v4 theme tokens in `resources/css/app.css` using the `@theme` directive:

```css
@theme {
    --color-brand: #15803D;
    --color-accent: #F59E0B;
    --color-sidebar: #F8FAFC;
    --color-surface: #FFFFFF;
    --color-page-bg: #F1F5F9;
    --color-neutral-strong: #0F172A;
    --color-success: #15803D;
    --color-error: #BE123C;
    --color-warning: #854D0E;
}
```

- Red (`error`) is reserved exclusively for errors, destructive actions, and remove affordances. Never use it as chrome, a header color, or decoration.
- Amber/gold is fills-only with dark text on top — never amber/yellow text on a white background.
- **Off-palette colors are banned:** no teal and no blue chrome. If a component needs an unlisted color, revisit the design instead of adding a one-off exception.

### 3. Typography Standards

Use Tailwind utility classes directly — do not invent a parallel type scale:

| Element | Classes |
| --- | --- |
| Module header | `text-2xl font-bold text-brand` |
| Sidebar section header | `text-[13px] font-bold uppercase tracking-wide text-brand` |
| Sidebar buttons | `text-sm font-medium text-foreground` |
| Table text | `text-xs` or `text-[13px]` for high density |
| Card label | `text-xs font-bold text-muted-foreground` |
| Card value | `text-2xl font-black` |

### 4. Structural Constraints

- **Sidebar width:** fixed `w-80` (320px).
- **Main content padding:** consistent `p-[30px]`, or one standardized nearest Tailwind scale step across all pages.
- **Corner radius:** `rounded-xl` to `rounded-2xl` for cards/panels; `rounded-md` to `rounded-lg` for buttons.
- **Card styling:** prefer `border border-slate-200` or a soft `shadow-sm`/`shadow` over heavy elevation. `shadow-xl` and `shadow-2xl` are banned on cards.

### 5. Overlay Standard (Mandatory for Every Operational Action)

Every Create/Edit/Add/Payout-style action opens as a panel in an overlay layer above the main content — never a full-page navigation or a swap that hides the underlying list.

- **Backdrop:** apply `backdrop-blur-md` to the main content wrapper while the overlay is active, with a `bg-[#0F172A]/80` scrim behind the active panel.
- **Behavior:** the main list/table stays mounted and visible behind the overlay; do not unmount or collapse it while the panel is open.
- **Panel transition (Alpine.js):**

```html
x-transition:enter="ease-out duration-200"
x-transition:enter-start="opacity-0 scale-95"
x-transition:enter-end="opacity-100 scale-100"
x-transition:leave="ease-in duration-150"
x-transition:leave-start="opacity-100 scale-100"
x-transition:leave-end="opacity-0 scale-95"
```

### 6. Module Layout Pattern

- **Left (sidebar, `bg-sidebar`):** navigation, filters, search, and primary action buttons in amber/gold.
- **Center:** main operational data — lists, tables, and records.
- **Right (optional):** selected-item details, transaction history, or previews.

Before shipping any UI change: confirm it matches the color and typography locks, confirm the sidebar stays light (`--color-sidebar`), and confirm no out-of-scope module (Masterlist, Aid Requests, Cash-for-Work) leaked into navigation or permissions.

Before editing:
1. Identify affected Laravel routes, backend workflow, and UI components.
2. List exact files to inspect.
3. List exact files to modify.
4. Explain the smallest safe implementation plan.
5. Do not edit until the plan is clear.

Constraints:
- Preserve existing routes, forms, validation, and actions unless required.
- Do not touch unrelated features.
- Do not refactor unrelated code.
- Do not install packages unless asked.
- Keep changes minimal.

Verification:
Run the smallest relevant Laravel test, lint, or frontend build command.

## Login Form Implementation Prompt

Implement the Laravel 12 login page to match the existing eKalinga+ login experience in the repository. Use Blade, Tailwind CSS v4, Laravel validation, session authentication, and Alpine.js only where interactive state is needed. Reproduce the existing layout, content, and behavior as a responsive web page without carrying over desktop-specific implementation details.

### Visual Layout

- Build a centered, two-panel authentication card with a maximum width close to the existing desktop proportion and `rounded-xl` corners.
- Keep the left panel as the LGU identity and branding area. Use a configurable background image with a dark brand overlay, a thin amber/gold accent bar at the top, the LGU seal/logo, the eKalinga+ logo, government identity text, municipality/owner name, optional address, installation serial, active connection summary when applicable, and the tagline `Better Service, Better Care`.
- Keep the right panel white and dedicated to the authentication form. Include the eKalinga+ logo, system name, `Ayuda Management System` subtitle, a divider, a form title, and a form subtitle.
- Use the theme tokens from `resources/css/app.css`: `brand`, `accent`, `sidebar`, `surface`, `page-bg`, `neutral-strong`, `success`, `error`, and `warning`. Do not introduce blue chrome or unrelated colors.
- On small screens, stack the panels or hide only secondary branding details; never make the form unusable or require horizontal scrolling.

### Login Mode

Display the normal login form when the application already has an administrator account:

- Title: `Admin Login`.
- Subtitle: `Sign in to manage barangay operations.`
- Required field: `Username or email`, with placeholder `Enter your username or email`.
- Required field: `Password`, with placeholder `Enter your password`.
- Use a real password input, an accessible show/hide password control if implemented, and a primary amber/gold `SIGN IN` button with dark text.
- Submit on Enter, disable the button while processing, preserve old input for the username/email only, and never repopulate a password.
- Show validation and authentication feedback in a clearly visible status area. Use the `error` token for failures and the `success` token only for successful actions.
- Include the existing demo-account notice only when the application is explicitly running in a local/demo environment; never expose demo credentials in production.
- Keep the registration area as a `Register here` link or action only if the existing application enables it. The current behavior is that self-registration is disabled and users must contact an administrator, so do not create an open registration flow without explicit approval.

### Initial Administrator Setup Mode

When the database has no administrator account, show an initial setup form instead of the normal login form:

- Title: `Initial Admin Setup`.
- Subtitle: `Create the first admin account for the selected database.`
- Fields: `Full Name`, `Admin Username`, `Admin Email`, `Admin Password`, and `Confirm Password`.
- Use the existing defaults where appropriate: `Barangay Administrator`, `admin`, and `admin@barangay.local`; never hard-code a password.
- Show `Minimum 8 characters` and `Re-enter password` guidance through labels or placeholders.
- Submit with a green `CREATE ADMIN ACCOUNT` button with white text.
- Validate required values, email format, password length, password confirmation, unique username/email, and the rule that only the first administrator can be created through this setup route.
- Hash the password with Laravel's approved password hashing mechanism. Never store or log plaintext passwords.
- After successful creation, redirect to the login state and show a safe success message without revealing sensitive data.

### Backend and Security Behavior

- Use named Laravel routes, a dedicated form request for validation where appropriate, and Laravel's authentication/session mechanisms rather than custom plaintext authentication.
- Accept either username or email, and authenticate only active accounts. Return the same generic failure message for an unknown account or invalid password to avoid account enumeration.
- Record successful and failed login attempts through the project's existing audit/logging mechanism when one exists.
- Add throttling/rate limiting, CSRF protection, session regeneration after login, and logout/session invalidation according to Laravel conventions.
- Authorize the application after login using only the supported `SuperAdmin` and `Admin` roles. Do not add other roles.
- Keep branding, logo, background, address, serial, and connection-summary values server-provided and safely escaped in Blade.
- Provide a connection/settings action only if the Laravel application already supports it; do not expose credentials or raw connection strings in the UI.

### Implementation Deliverables

Create or update only the necessary Laravel files: routes, controller/action, form requests, authentication or setup service, Blade view/components, Alpine state if needed, and focused feature tests. Preserve existing route names and authentication behavior when they already exist. Test normal login, invalid credentials, inactive users, validation errors, first-admin setup, duplicate account protection, session regeneration, authorization, and responsive rendering.

## Obsidian Daily Reporting Rules
At the end of every active coding session or day, the agent must document all changes after a prompt/query was done immediately in the Obsidian wiki:
1. **Location:** `C:\Users\ASUS\OneDrive\Desktop\Projects-wiki\Daily Logs\<YYYY-MM-DD>.md`
2. **Content Required:**
   - A list of modified files.
   - The purpose of each change.
   - A comprehensive summary of what was done.
3. **Trigger:** The developer can ask for this explicitly, or the agent should auto-generate it if requested to do a summary at the end of work.