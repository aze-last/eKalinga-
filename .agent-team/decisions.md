# Agent Team Decisions

- Runtime mode: `manual-session`
- Shared truth: task files in `.agent-team/tasks`
- Transport: mailbox files in `.agent-team/mailbox`
- Default isolation: shared workspace with bounded file ownership
- Dashboard rule: preserve approved adviser UI while wiring behavior
- Navigation rule: use `BarangayMainViewModel` shell commands
- Verification rule: require `dotnet build` and `dotnet test` before completion
