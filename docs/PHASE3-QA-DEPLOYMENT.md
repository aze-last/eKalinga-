# Phase 3 QA and Deployment Runbook

## A. QA Regression Checklist

### 1. Authentication and RBAC
- [ ] Admin login works: `admin@mcdonald.com / admin123`
- [ ] HR login works: `hr@mcdonald.com / hr123`
- [ ] Manager login works: `manager1@mcdonald.com / manager123`
- [ ] Crew login works: `crew@gmail.com / crew123`
- [ ] HR cannot open User Management (hidden in sidebar + blocked in command)
- [ ] Admin can access User Management

### 2. Attendance
- [ ] Employee can time in only with scheduled shift for today
- [ ] Employee cannot time in while on approved leave
- [ ] Employee cannot double time-in (existing open attendance)
- [ ] Time out computes total and overtime hours
- [ ] Attendance status list shows correct status with shared shifts

### 3. Leave Management
- [ ] Submit leave validates date range and reason
- [ ] Submit leave blocks overlap with non-rejected/non-cancelled leave
- [ ] Approve leave updates leave balances
- [ ] Reject leave requires reason
- [ ] Cancel approved leave restores leave balance safely

### 4. Payroll
- [ ] Generate payroll for a date range
- [ ] Gross pay = regular + overtime + holiday
- [ ] Deduction = late + early leave adjustments
- [ ] Net pay is shown and saved to payroll table (`total_pay`)
- [ ] Save payroll stores records with current user as `generated_by`

### 5. Exports
- [ ] Payroll CSV export works and includes totals row
- [ ] Manager attendance CSV export works

### 6. Audit Trail
- [ ] Login success/failure logs exist
- [ ] Time in/out logs exist
- [ ] Leave submit/approve/reject/cancel logs exist
- [ ] Payroll save logs exist

## B. Test Data and Reset
1. Set `Database.ResetOnStartup` to `true` in `appsettings.json`.
2. Run app once.
3. Set `Database.ResetOnStartup` back to `false`.
4. Re-login and execute checklist.

## C. Deployment / Demo Startup
1. Ensure MySQL is running.
2. Verify `appsettings.json` connection string.
3. Start application.
4. Use seeded accounts for each actor role.

## D. Known Environment Issue (Current Machine)
`dotnet build` fails in this machine due .NET host/runtime loader mismatch:
- `hostfxr.dll` load failure (`HRESULT 0x800700C1`)

Use Visual Studio runtime profile that matches installed architecture/runtime.
