# Budget Module Design

## Understanding Summary

- The Ayuda system needs one new `Budget` module in the sidebar.
- Budget is shared across operational features, especially `Assistance Cases` and `Cash-for-work`.
- The system uses two internal budget buckets:
  - `Government` from `GGMS`
  - `Private Sector` maintained locally in Ayuda
- Users must see one combined available budget and must not manually choose the funding source.
- Budget is deducted only on actual `release/disbursement`.
- Releases must use `government first` and `private second` as overflow.
- Insufficient combined budget must block release.

## Assumptions

- `GGMS` remains read-only and sync-only for the Ayuda app.
- Ayuda maps to the GGMS office code `OFF-2026-0006`.
- Private donations are Ayuda-only and do not need an office picker.
- Every release should be linked to a reusable `Ayuda Program/Event`.
- Private donation proof files are optional because some donations are cash-based.
- `Cash-for-work` currently has attendance and release-ready summaries but no final money-release action, so budget release needs to be added there.

## Decision Log

- Chosen approach: one `Budget` module instead of separate buttons for government and private budgets.
- User-facing budget behavior: combined total, with no manual source picker.
- Internal allocation rule: `Government first, Private second`.
- Budget enforcement point: `release/disbursement`, not approval.
- Shortfall handling: hard block, not warning-only.
- Private donations: dedicated Ayuda-only form with donor attribution and optional proof.
- `Cash-for-work` stays as an operations module; `Budget` is the finance and control module.

## Final Design

### Core Records

- `GovernmentBudgetSnapshot`
  - local synced copy of the latest GGMS Ayuda allocation
- `PrivateDonation`
  - donor record with amount, receipt details, proof metadata, and optional proof file path
- `AyudaProgram`
  - reusable master record for assistance programs and C4W events
- `BudgetLedgerEntry`
  - shared audit trail for donations and releases across features

### Budget Rules

- Combined available budget is computed from:
  - latest government allocation snapshot
  - plus total private donations
  - minus all prior release ledger entries
- Releases are auto-allocated:
  - government balance first
  - private balance for any overflow
- If requested release amount is greater than combined available budget:
  - block the release

### Assistance Case Integration

- Assistance cases must select an `Ayuda Program`.
- Releasing an assistance case consumes the case `ApprovedAmount`.
- A successful release writes one `BudgetLedgerEntry`.
- The case should keep a reference to the resulting ledger entry to prevent duplicate release deductions.

### Cash-for-Work Integration

- Cash-for-work keeps its event, participant, and attendance workflow.
- Final money release must also pass through the shared budget service.
- The event should link to an `Ayuda Program`.
- The release operation should store the total released amount and resulting budget ledger reference.

### Private Donations

- Private donations must record:
  - donor type
  - donor name
  - amount
  - date received
  - reference number
  - remarks
  - proof type
  - optional proof reference number
  - optional proof file path
  - encoded by
- Every donation should also create a `BudgetLedgerEntry`.

### Government Sync

- GGMS sync remains read-only.
- The sync process should refresh the local Ayuda government snapshot using office code `OFF-2026-0006`.
- If GGMS connection settings are missing or unavailable, the UI should surface a clear sync status instead of silently failing.
