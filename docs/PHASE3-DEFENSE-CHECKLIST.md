# Phase 3 Defense Checklist and Demo Script

## 1. 10-Minute Demo Flow
1. Login as Admin.
2. Show dashboard KPIs and attendance status.
3. Open User Management (Admin-only proof).
4. Open Employees and add/edit sample employee.
5. Generate payroll and export CSV.
6. Logout, login as HR.
7. Show HR can manage employees/payroll but not users.
8. Logout, login as Manager.
9. Show attendance feed export + leave approval.
10. Logout, login as Crew.
11. Submit leave request and perform time in/out.

## 2. Key Defense Talking Points
- RBAC enforcement was applied in command handlers and page availability.
- Attendance logic handles shared shifts correctly (employee+shift key).
- Leave and payroll now use centralized service rules with audit logging.
- Exports provide evidence-ready records for operations and reporting.

## 3. Evidence to Prepare (Screenshots / Files)
- Login per role
- HR hidden User Management button
- Leave request approval/rejection
- Payroll net pay table and saved records
- Exported CSV files
- Sample activity log rows

## 4. Suggested Appendix Content
- ERD diagram
- SQL schema script
- QA checklist results (pass/fail)
- User credential matrix
- Runbook/reset procedure
