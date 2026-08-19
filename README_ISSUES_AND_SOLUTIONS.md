# Cash-for-Work & Seminar Modules: Issues, Root Causes, and Solutions

This document details the issues encountered in the **Cash-for-Work** and **Seminar Attendance** modules, the underlying root causes, the technical fixes implemented, and the end-to-end operational workflow.

---

## 1. Issue: "Save attendance before releasing funds" Error

### Problem Description
When clicking **RELEASE PAYOUT** -> **RELEASE BUDGET** to disburse funds for an event, the system showed a dialog error:
> *"Unable to Release Budget: Save attendance before releasing funds."*

### Root Causes
1. **Empty / Fresh Events (0 Attendance Records)**:
   - When a new event is created, there are initially **0 Enrolled Workers** and **0 Logged Attendance Records**.
   - The payout release engine (`ReleaseEventAsync`) queries `GetReleaseReadySummary(eventId)` to calculate disbursements. If `ReleaseReadyParticipantCount <= 0`, it prevents creating an empty PHP 0.00 ledger disbursement entry.
2. **Overly Strict Verification Status & Missing HouseholdMember Linkage in Release Summary**:
   - `GetReleaseReadySummary(eventId)` was strictly filtering present attendees with `attendance.Participant.Beneficiary?.VerificationStatus == VerificationStatus.Approved`.
   - If a participant had `VerificationStatus.Verified` or `VerificationStatus.Pending`, or was linked via `HouseholdMemberId` instead of `BeneficiaryStagingId` (`Participant.Beneficiary == null`), they were silently filtered out, dropping `ReleaseReadyParticipantCount` to `0` and triggering the generic error.
3. **Overly Strict Date Matching (Historical Events)**:
   - Previously, `SaveManualAttendanceByBeneficiaryStagingIdsAsync` enforced that attendance could only be recorded if `DateTime.Today` matched the exact `EventDate`.

### Solutions Applied
- **Supported Both Linkage Paths & Realistic Statuses**:
  - Updated `GetParticipants(eventId)` and `GetAttendanceRecords(eventId)` in `Services/CashForWorkService.cs` to include both `Beneficiary` and `HouseholdMember` (with its parent `Household`).
  - Added `IsParticipantEligibleForRelease(participant)`: All valid non-rejected/non-inactive beneficiaries and household members are eligible for payout releases.
  - Mapped IDs from both sources (`Beneficiary.BeneficiaryId ?? HouseholdMember.Household.HouseholdCode`).
- **Separated & Accurate Error Reporting**:
  - When no attendance records exist: *"No attendance recorded for this event. Record attendance before releasing funds."*
  - When attendance exists but no participants are eligible: *"Attendance was recorded, but no present attendees are eligible for payout (e.g., status is rejected or profile is unlinked)."*
- **Relaxed Date Validation**: Removed strict single-day restriction in `Services/CashForWorkService.cs`.
- **Enhanced Payout Modal UI**: Updated `Views/CashForWorkOcrPage.xaml` so the **RELEASE DISBURSEMENT PAYOUT** modal displays a real-time summary card showing the number of present attendees and clear instructions if attendance is missing.

---

## 2. Issue: Dual / Conflicting Cash-for-Work Pages (Photo 1 vs. Photo 2)

### Problem Description
- Navigating to Cash-for-Work from the **Dashboard** showed the modern **CASH-FOR-WORK & EVENTS** page (Photo 2).
- Creating a Cash-for-Work event from the **Budget** module redirected to an obsolete **Cash-for-Work Payout** page (Photo 1) that lacked the `DASHBOARD` navigation button, scanner dock, and attendance tools.

### Root Cause
- `Views/BudgetPage.xaml.cs` invoked `ShowCashForWorkPayoutCommand`, which was mapped in `ViewModels/BarangayMainViewModel.cs` to a legacy UserControl (`CashForWorkPayoutPage.xaml`).

### Solutions Applied
- **Unified Route**: Updated `Views/BudgetPage.xaml.cs` and `ViewModels/BarangayMainViewModel.cs` to route both `"CashForWork"` and `"CashForWorkPayout"` to `Views/CashForWorkOcrPage.xaml`.
- **Cleaned Obsolete Code**: Deleted legacy files `CashForWorkPayoutPage.xaml`, `CashForWorkPayoutPage.xaml.cs`, and `CashForWorkPayoutViewModel.cs`.

---

## 3. Issue: "RELEASE PAYOUT" Button Visibility

### Problem Description
The **RELEASE PAYOUT** button was hidden or missing in certain views.

### Root Cause
- The sidebar rail button was bound to `Visibility="{Binding PayoutRailVisibility}"`, which evaluated to `Collapsed` when events were loaded or refreshed without explicit notification.
- There was no direct button on the active event header toolbar.

### Solutions Applied
- **Sidebar Action Rail**: Made **`RELEASE PAYOUT`** permanently visible in the sidebar workflow actions.
- **Active Event Header**: Added a prominent **`[💰 RELEASE PAYOUT]`** button directly inside the active event header toolbar next to `EDIT EVENT` and `SAVE PDF SHEET`.

---

## 4. Issue: Missing Creation Date and End Date on Events

### Problem Description
Events in Cash-for-Work and Seminars had no visual indication of when they were created or their start/end date period.

### Solutions Applied
- Updated `Models/CashForWorkEvent.cs` with formatted properties: `CreatedDateFormatted`, `StartDateFormatted`, `EndDateFormatted`, and `DateRangeFormatted`.
- Added high-contrast badges under event titles in `Views/CashForWorkOcrPage.xaml`, `Views/SeminarAttendancePage.xaml`, and `Views/Dialog/CashForWorkEventListDialog.xaml`.

---

## 5. Standard Operational Workflow

To successfully run a Cash-for-Work project and release payouts:

1. **Create Event**: Click **`+ NEW EVENT VIA BUDGET`** (or create from the Budget module).
2. **Log Attendance**:
   - Option A: Click **`LOG MANUAL ATTENDANCE`** on the left rail, select/search attendees, and click **`SAVE ATTENDANCE`**.
   - Option B: Click **`START PC CAMERA SCANNER`** or **`SCAN ATTENDANCE (GATEWAY)`** to scan QR/Digital IDs.
3. **Verify Table**: The main table displays present workers with their Civil ID, Name, Contact, Date, Time In, and Status.
4. **Release Payout**: Click **`RELEASE PAYOUT`**, enter or verify the Amount per Attendee (e.g. `PHP 10,000.00`), and click **`RELEASE BUDGET`**.
