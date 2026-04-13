# Mailbox

Mailbox files are created per worker on demand.

## Message Types

- `assign_task`
- `progress_update`
- `blocker`
- `scope_change_request`
- `handoff_ready`
- `verification_request`
- `verification_result`
- `shutdown`

## Example

```json
{"type":"assign_task","from":"coordinator@attendance-shifting-management","to":"dashboard-nav@attendance-shifting-management","taskId":"DASH-02","summary":"Wire dashboard tiles to the shell navigation flow."}
```
