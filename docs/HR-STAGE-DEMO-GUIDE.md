# HR Stage Demo Guide (ASMS)

## 1. Pre-Demo Setup (Do this before class)
1. Open PowerShell in project root:
`c:\Users\ASUS\source\repos\AttendanceShiftingManagement\AttendanceShiftingManagement`
2. Build the app:
`& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" AttendanceShiftingManagement.csproj /t:Build /p:Configuration=Debug`
3. Reset + reseed database:
`Start-Process -FilePath ".\bin\Debug\net9.0-windows\AttendanceShiftingManagement.exe" -ArgumentList "--reset-db"`
4. Wait until Login page appears, then close and relaunch normally:
`Start-Process -FilePath ".\bin\Debug\net9.0-windows\AttendanceShiftingManagement.exe"`

## 2. Demo Accounts
- Admin: `admin@mcdonald.com` / `admin123`
- HR: `hr@mcdonald.com` / `hr123`
- Manager: `manager1@mcdonald.com` / `manager123`
- Crew: `crew@gmail.com` / `crew123`

## 3. Fast Role-Switch Flow (No repeated logout/login)
1. Login as Admin.
2. In left sidebar click `Switch Role`.
3. Select `hr@mcdonald.com`.
4. You are now in HR view with an impersonation banner.
5. After HR demo, click `Return Admin`.

## 4. HR Stage Script (10-12 minutes)

### Opening (1 minute)
1. Say: "This is an event-driven HR dashboard; cards and alerts update based on attendance, leave, payroll, and shift events."
2. Point to top cards:
- Active Employees
- Present Today
- Pending Leaves
- Shift Coverage

### Essential Metrics Buttons (7 modules)
1. `Recruitment`
- Show pipeline table and filters (source/stage/date).
- Say: "We track time-to-hire pipeline stages and candidates by source."

2. `Retention & Turnover`
- Show active vs inactive distribution and exit records.
- Say: "This supports voluntary/involuntary turnover analysis."

3. `Performance`
- Show goals completion, review scores, training effectiveness.
- Say: "Performance data is tied to employee-level goals and outcomes."

4. `Engagement & Wellbeing`
- Show eNPS, engagement score, wellbeing score, burnout alerts.
- Say: "This helps detect absenteeism or burnout risk early."

5. `DEI`
- Show representation per area and pay equity gap.
- Say: "We monitor workforce representation and compensation spread by area."

6. `Compensation & Benefits`
- Show payroll totals, overtime cost trend, approved leave impact.
- Say: "Payroll and leave signals are consolidated for compensation control."

7. `Workforce Planning`
- Show required vs assigned shift slots and coverage gaps.
- Say: "This projects staffing pressure and upcoming headcount gaps."

### Closing (1 minute)
1. Say: "HR can monitor all critical KPIs in one dashboard, while Admin/Manager/Crew roles remain isolated via RBAC."
2. If asked about multi-role demo speed, show `Switch Role` and `Return Admin`.

## 5. If Something Fails During Stage
1. If login fails, close app and run with `--reset-db` once.
2. If dashboard is empty, wait 3-5 seconds then click module `Refresh` button.
3. If teacher asks for role separation, log in directly as:
- HR account for HR metrics
- Manager account for manager view
- Crew account for crew view

## 6. What Was Seeded for Demo
- Users: 26
- Employees: 26
- Recruitment candidates: 5
- Employee exits: 2
- Shifts: 56
- Shift assignments: 154
- Attendance records: 73
- Leave requests: 10
- Payroll records: 56
- Performance goals: 14
- Training records: 16
- Engagement surveys: 18
