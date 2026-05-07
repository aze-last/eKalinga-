# Dashboard Agent Team Design

## Understanding Summary

- Build a `dev-time` coordinator-plus-workers system for this repo, not an in-app runtime.
- The first tracked use case is the `Dashboard`.
- The dashboard must keep its currently approved adviser-facing visual direction.
- The immediate gap is wiring: dashboard tiles are not connected to module navigation.
- Navigation should use the existing shell flow in `BarangayMainViewModel`, not direct window launching.
- Later, the same team pattern can be reused to improve other modules one by one.
- Verification must include `dotnet build`, and `dotnet test` when covered logic is touched.

## Assumptions

- The MVP harness is repo-local and file-backed.
- The first version uses `manual session mode` with optional script assistance.
- Workers share the main workspace by default but must stay inside explicit file ownership.
- Worktree isolation is deferred until later module work or overlapping edits justify it.
- `Views/BarangayDashboardPage.xaml` may be structurally refined, but its approved visual design must be preserved.
- `appsettings.json` and other risky environment files are out of scope unless explicitly assigned.

## Decision Log

- Chosen approach: `repo-local file-backed team runtime`.
- Chosen shared truth: `task board files`, not prompt transcript.
- Chosen transport: `mailbox messages`, not informal chat only.
- Chosen dashboard navigation path: reuse the existing shell section-switch flow.
- Chosen MVP isolation: shared workspace with bounded file ownership.
- Chosen completion gate: independent verifier must run build and test checks before the coordinator closes work.

## Final Design

### Coordination Spine

Use a repo-local harness rooted at `.agent-team/`.

```text
.agent-team/
  team.json
  decisions.md
  tasks/
    DASH-01.json
    DASH-02.json
    DASH-03.json
    DASH-04.json
    DASH-05.json
  mailbox/
    coordinator.jsonl
    repo-mapper.jsonl
    dashboard-ui.jsonl
    dashboard-nav.jsonl
    dashboard-data.jsonl
    ui-ux-auditor-redesigner.jsonl
    verifier.jsonl
  templates/
    coordinator.md
    repo-mapper.md
    dashboard-ui.md
    dashboard-nav.md
    dashboard-data.md
    ui-ux-auditor-redesigner.md
    verifier.md
```

- `tasks/` is the durable state store.
- `mailbox/` is the control and notification channel.
- `team.json` defines team identity, allowed modes, and worker roster.
- `decisions.md` records accepted architecture and workflow choices over time.

### Team Roles

- `Coordinator`
  - owns planning, assignment, approval, scope control, and completion
- `Repo Mapper`
  - confirms actual file seams before any worker starts
- `Dashboard UI Worker`
  - owns `Views/BarangayDashboardPage.xaml`
- `Dashboard Navigation Worker`
  - owns `Views/BarangayDashboardPage.xaml.cs`
  - may also touch `ViewModels/BarangayMainViewModel.cs` only if shell wiring requires it
- `Dashboard Data Worker`
  - owns `ViewModels/BarangayDashboardViewModel.cs`
  - touches service-facing dashboard shaping only if redesign needs additional existing data exposure
- `UI/UX Auditor / Redesigner`
  - audits screenshots, XAML, and screen behavior using explicit usability and accessibility criteria
  - may produce redesign specs or bounded UI-facing implementation guidance when assigned
  - should stay idle until a coordinator or user explicitly requests an audit or redesign pass
- `Verifier`
  - read-only across changed files
  - runs build and test gates

### Stable Worker Identity

Use stable IDs to keep ownership and mailbox routing deterministic:

- `coordinator@attendance-shifting-management`
- `repo-mapper@attendance-shifting-management`
- `dashboard-ui@attendance-shifting-management`
- `dashboard-nav@attendance-shifting-management`
- `dashboard-data@attendance-shifting-management`
- `ui-ux-auditor-redesigner@attendance-shifting-management`
- `verifier@attendance-shifting-management`

### Task Board Model

Each work item in `.agent-team/tasks/` should contain:

- `id`
- `title`
- `status`
  - `todo | in_progress | blocked | review | done`
- `owner`
- `goal`
- `allowedPaths`
- `dependencies`
- `verification`
- `notes`
- `artifacts`

Minimal example:

```json
{
  "id": "DASH-02",
  "title": "Wire dashboard tiles to shell navigation",
  "status": "in_progress",
  "owner": "dashboard-nav@attendance-shifting-management",
  "goal": "Use the existing main shell section switch path for dashboard tile clicks.",
  "allowedPaths": [
    "Views/BarangayDashboardPage.xaml.cs",
    "ViewModels/BarangayMainViewModel.cs",
    "Views/BarangayDashboardPage.xaml"
  ],
  "dependencies": ["DASH-01"],
  "verification": [
    "dotnet build AttendanceShiftingManagement.sln"
  ],
  "notes": [],
  "artifacts": []
}
```

### Mailbox Protocol

Mailbox messages are append-only JSON lines in `.agent-team/mailbox/<worker>.jsonl`.

Required message types:

- `assign_task`
- `progress_update`
- `blocker`
- `scope_change_request`
- `handoff_ready`
- `verification_request`
- `verification_result`
- `shutdown`

Protocol rules:

- Only the `Coordinator` sends `assign_task`.
- Only the `Coordinator` approves `scope_change_request`.
- Only the `Verifier` sends the final `verification_result`.
- Workers must update both `tasks/*.json` and mailbox state; mailbox alone is not enough.
- Workers must not edit outside `allowedPaths`.

Minimal message shape:

```json
{
  "type": "assign_task",
  "from": "coordinator@attendance-shifting-management",
  "to": "dashboard-nav@attendance-shifting-management",
  "taskId": "DASH-02",
  "summary": "Wire dashboard tiles into the existing shell navigation flow.",
  "timestamp": "2026-04-13T00:00:00Z"
}
```

### Spawn Modes

#### MVP: Manual Session Mode

- The coordinator creates task files and worker prompts.
- You open separate coding sessions only when parallel work is worth it.
- Best fit for dashboard-first work because file overlap is small and verification is simple.

#### Next: Script-Assisted Mode

Add `scripts/agent-team/*.ps1` later for:

- team bootstrap
- task creation
- worker prompt generation
- verification requests
- closeout summaries

#### Later: Worktree Mode

Use only when parallel workers would otherwise collide on the same files.

### Isolation Stance

Default isolation for this repo:

- shared workspace
- strict file ownership
- coordinator-controlled task boundaries

Do not start with per-worker worktrees for the dashboard sprint. Add worktree isolation later for:

- overlapping module redesigns
- refactors touching shared styles
- high-risk code-writing tasks

### Verification Stance

The verifier is independent and must not be the same worker that implemented the change.

Minimum dashboard verification:

```powershell
dotnet build AttendanceShiftingManagement.sln
dotnet test AttendanceShiftingManagement.Tests
```

Verification results flow back into:

- the task file status
- a `verification_result` mailbox message

The coordinator cannot mark a task complete without verifier confirmation.

### Dashboard-First Workflow

Initial dashboard sprint tasks:

- `DASH-01`
  - audit current wiring gap and confirm real integration points
- `DASH-02`
  - wire dashboard tiles to existing shell navigation
- `DASH-03`
  - hook `Settings`, `Updates`, and `Logout` actions using existing handlers
- `DASH-04`
  - preserve approved dashboard UI while cleaning layout structure only where needed for maintainability
- `DASH-05`
  - run build and test verification

Expected ownership:

- `DASH-01` -> `Repo Mapper`
- `DASH-02` -> `Dashboard Navigation Worker`
- `DASH-03` -> `Dashboard Navigation Worker`
- `DASH-04` -> `Dashboard UI Worker`
- `DASH-05` -> `Verifier`

`Dashboard Data Worker` is optional for the first sprint and should stay idle unless the coordinator decides the dashboard needs additional bound data or cleaner VM-driven status presentation.

### Source-Backed Repo Facts

These are grounded in the current repo:

- `BarangayMainViewModel` already switches sections and builds module views.
- `BarangayDashboardPage.xaml` contains module tiles and bottom action buttons, but they are currently not wired.
- `BarangayDashboardPage.xaml.cs` already contains settings/update/logout handlers, but they are not currently hooked from XAML.
- `BarangayDashboardViewModel` currently exposes dashboard data and `RefreshCommand`, but no shell navigation commands.

### Proposed Harness Components

These are design proposals for this repo, not currently implemented source:

- `.agent-team/` runtime folder
- file-backed task board
- file-backed mailbox protocol
- PowerShell bootstrap and verification scripts
- reusable worker prompt templates

## Implementation Handoff

When implementation starts, do it in this order:

1. Create the `.agent-team/` folder structure and baseline files.
2. Add worker prompt templates and task/message schemas.
3. Run the first dashboard sprint through `DASH-01` to `DASH-05`.
4. Keep the dashboard look intact while wiring shell navigation.
5. Require verifier signoff before calling the sprint complete.
