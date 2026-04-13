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
