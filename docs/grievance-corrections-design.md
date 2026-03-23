# Grievance / Corrections Design

## Understanding Summary

- Add a separate local `Grievance / Corrections` feature to the project first.
- Support grievance types: `Duplicate`, `WrongIdentity`, `MissingBeneficiary`, and `WrongRelease`.
- Support grievance statuses: `Open`, `UnderReview`, `Resolved`, and `Rejected`.
- Require remarks for `rejection`, `correction`, and `override` actions.
- Link grievance records to the imported beneficiary baseline, using `CivilRegistryId` first and `BeneficiaryId` as fallback.
- Track cumulative ayuda received locally and show warning-only behavior across features.
- Count cumulative ayuda only when assistance is actually `released/disbursed`.
- Keep `CRS` and `GGMS` integration out of scope for this first local version.

## Assumptions

- The imported beneficiary side is the identity anchor for cross-feature checks.
- The first version stores grievance records and assistance totals only in this project database.
- Existing `ActivityLog` remains the audit layer, while grievance and ledger tables are the business records.
- Warning behavior never auto-declines records; it only informs the acting user.
- The large-assistance threshold will be admin-configurable through settings.

## Final Design

### 1. New module

Add a dedicated `Grievance / Corrections` page to the main shell.

The page should include:

- summary cards for `Open`, `Under Review`, `Resolved`, `Rejected`
- searchable grievance list
- grievance detail panel
- linked beneficiary snapshot
- cumulative ayuda received
- threshold warning banner
- actions for `Open Review`, `Resolve`, and `Reject`

### 2. Grievance record

Create a local grievance entity with these fields:

- `Id`
- `GrievanceNumber`
- `CivilRegistryId`
- `BeneficiaryId`
- `StagingId` nullable
- `AssistanceCaseId` nullable
- `CashForWorkEventId` nullable
- `Type`
- `Status`
- `Title`
- `Description`
- `FiledByUserId`
- `AssignedToUserId` nullable
- `ResolutionRemarks` nullable
- `CreatedAt`
- `UpdatedAt`
- `ResolvedAt` nullable

This record is the main business object for complaints and correction handling.

### 3. Shared ayuda ledger

Create a local beneficiary assistance ledger keyed by imported-beneficiary identity:

- primary lookup: `CivilRegistryId`
- fallback lookup: `BeneficiaryId`

Suggested fields:

- `Id`
- `CivilRegistryId`
- `BeneficiaryId`
- `SourceModule`
- `SourceRecordId`
- `ReleaseDate`
- `Amount`
- `Remarks`
- `RecordedByUserId`
- `CreatedAt`

The ledger is the only source used to calculate how much ayuda a beneficiary has already received.

### 4. Threshold warning

Add a configurable `LargeAssistanceWarningThreshold` in settings.

Shared warning logic:

1. Resolve beneficiary identity with `CivilRegistryId` first, then `BeneficiaryId`.
2. Sum matching ledger records.
3. Compare total against the threshold.
4. Show warning only.
5. Allow the user to continue, but require remarks when doing override-sensitive actions.

### 5. Remarks rules

Require remarks for:

- grievance rejection
- correction actions
- override actions taken after warning

Remarks should be written both to the grievance/business record and to `ActivityLog`.

### 6. Cross-feature behavior

For this phase:

- grievance creation and review are local only
- cumulative totals are local only
- future release/disbursement flows should write to the ledger when actual release happens

The first version does not need live `CRS` or `GGMS` integration.

## Decision Log

### Decision 1

- Decision: use a separate grievance module
- Alternatives: embed inside beneficiary verification; back-end only
- Reason: cleaner demo, less UI overload, easier future growth

### Decision 2

- Decision: store grievance records separately from audit logs
- Alternatives: use `ActivityLog` only
- Reason: `ActivityLog` is audit, not a queryable grievance/business record

### Decision 3

- Decision: track cumulative ayuda in a separate local ledger
- Alternatives: infer totals from logs or per-feature tables only
- Reason: one shared place is needed for cross-feature warnings

### Decision 4

- Decision: warning-only behavior for prior large assistance
- Alternatives: hard stop or auto-decline
- Reason: aligns with grievance handling and teacher requirement to record, not auto-reject

### Decision 5

- Decision: identity anchor is the imported beneficiary side
- Alternatives: household member or household aggregate
- Reason: matches project direction and imported-baseline presentation needs

### Decision 6

- Decision: ledger totals increase only on actual release/disbursement
- Alternatives: count on approval; count approval and release separately
- Reason: released aid is the accurate measure of what was actually received

## Initial Implementation Plan

1. Add grievance and ledger models plus EF registration and migration support.
2. Add service tests for grievance creation, status transitions, remarks requirements, and threshold checks.
3. Implement grievance and warning services.
4. Add the new grievance page and viewmodel.
5. Add settings support for the warning threshold.
6. Wire the new page into navigation.
7. Verify with tests and release build.
