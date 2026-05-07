# Agent Team Runtime

Repo-local coordinator-plus-workers runtime for engineering work on this repository.

## Purpose

- keep task ownership explicit
- preserve approved UI direction while implementing changes
- verify work before completion
- avoid accidental edits to risky files

## Shared State

- team config: `.agent-team/team.json`
- decision log: `.agent-team/decisions.md`
- task board: `.agent-team/tasks/*.json`
- mailbox transport: `.agent-team/mailbox/`
- worker role prompts: `.agent-team/templates/*.md`

## Dashboard First Workflow

1. Review `docs/dashboard-agent-team-design.md`
2. Claim or assign `DASH-01` through `DASH-05`
3. Keep dashboard visual direction intact
4. Wire dashboard actions through the existing shell path
5. Run `dotnet build` and `dotnet test` before closure

## Documentation Workflow

Use `manual-writer@attendance-shifting-management` when the task is limited to:

- `README.md`
- `docs/User-Manual-Ayuda.docx`
- `.agent-team` documentation or worker prompts

Rules:

- keep the worker passive until the user or coordinator explicitly assigns the documentation task
- do not let it start rewrites, screenshot capture, exports, or file replacement automatically
- rewrite end-user documentation against the live repo, not stale drafts
- keep module names aligned with the current shell and page titles
- do not invent buttons, scanner flows, or settings behavior
- run `dotnet build` before closing the documentation task

## UI/UX Audit Workflow

Use `ui-ux-auditor-redesigner@attendance-shifting-management` when the task is:

- UI heuristic audit from screenshot, description, or code
- screen redesign spec for WPF views already in this repo
- implementation guidance for stronger hierarchy, spacing, contrast, and component consistency

Rules:

- keep the worker passive until the user or coordinator explicitly assigns the audit or redesign task
- ground every audit in the live XAML, viewmodel behavior, screenshots, or user-provided description
- call out critical usability and accessibility failures directly
- prefer root-cause redesign recommendations over cosmetic-only feedback
- keep edits inside assigned UI paths and avoid risky config files unless explicitly reassigned
- widgets and components must never overlap; every interactive element must occupy its own dedicated layout cell or clearly bounded container
- before declaring a UI task done, double-check every margin, row and column assignment, span, alignment, and spacing relationship so no components sapaw and no text or cards are clipped
