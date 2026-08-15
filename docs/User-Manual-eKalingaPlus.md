# eKalinga+ / Ayuda Management System
## Comprehensive Operations & Flow User Manual
**Official Desktop Edition (v1.0.6)**  •  *Municipality of Sulop, Province of Davao del Sur*

---

## Table of Contents

- [01. System Architecture & Login Authentication](#01-system-architecture--login-authentication)
- [02. Database Connection & Offline Preset Configuration](#02-database-connection--offline-preset-configuration)
- [03. Executive Dashboard & Operational Command Center](#03-executive-dashboard--operational-command-center)
- [04. Validated Beneficiaries Masterlist & Digital ID System](#04-validated-beneficiaries-masterlist--digital-id-system)
- [05. Aid Requests & Assistance Case State Machine](#05-aid-requests--assistance-case-state-machine)
- [06. Budget Management & Dual-Stream Waterfall Ledger](#06-budget-management--dual-stream-waterfall-ledger)
- [07. Project Distribution & Disbursement Execution](#07-project-distribution--disbursement-execution)
- [08. Cash-for-Work Attendance & Payout Operations](#08-cash-for-work-attendance--payout-operations)
- [09. Cash-for-Work Payout Disbursement Ledger](#09-cash-for-work-payout-disbursement-ledger)
- [10. Seminar & Training Attendance Verification](#10-seminar--training-attendance-verification)
- [11. GGMS Consolidated Transactions & Sync Audit](#11-ggms-consolidated-transactions--sync-audit)
- [12. Reports, Analytics & Official Export Engine](#12-reports-analytics--official-export-engine)
- [13. Scanning Portal & Remote Scanner Gateway](#13-scanning-portal--remote-scanner-gateway)
- [14. System Settings, Security & Database Backup Recovery](#14-system-settings-security--database-backup-recovery)

---

## 01. System Architecture & Login Authentication `[SYSTEM SETUP]`

The eKalinga+ application launches with a dual-branded, security-hardened authentication portal linking the official LGU Seal and the eKalinga+ operational core.

![Figure 01.1: System Architecture & Login Authentication](manual_screenshots/01_login_window.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Identity & Connectivity Display:** The left panel surfaces the Province of Davao del Sur / Municipality of Sulop seal, system serial, and active database connection indicator.
- 2. **Credentials Entry:** Enter your assigned username/email and password. The system supports full keyboard navigation (Press Enter to execute sign in).
- 3. **Demo & Default Credentials:** For local offline deployment, pre-configured roles are available (`admin@barangay.local` / `admin123`).
- 4. **OTP Multi-Factor Security:** Operators can toggle email OTP verification for enhanced authorization during staff shifts.

> **🛡️ System Policy:** Only authorized staff accounts may sign in. Password lockouts occur after consecutive failed attempts.

> **💡 Pro-Tip:** Keep the Local MySQL service running in XAMPP or Windows Services before launching the application.

---

## 02. Database Connection & Offline Preset Configuration `[SYSTEM SETUP]`

eKalinga+ features seamless multi-tier database synchronisation across Local MySQL, LAN Server, and Remote Cloud tiers with auto-migration.

![Figure 02.1: Database Connection & Offline Preset Configuration](manual_screenshots/02_connection_settings.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Connection Presets:** Switch between `Local (127.0.0.1:3306)`, `Network (LAN)`, or `Remote Cloud` directly from the login settings window.
- 2. **Live Connectivity Test:** Click 'Test Connection' to verify socket reachability, schema integrity, and response latency before committing changes.
- 3. **Zero-Configuration Bootstrap:** Missing database tables and migrations are automatically created by the `StartupMigrationCoordinator` upon initial startup.

> **🛡️ System Policy:** Standard workstations must keep the active preset on 'Local' unless explicitly directed by the IT Administrator.

> **💡 Pro-Tip:** Click 'Test Connection' before saving credentials to prevent authentication lockouts.

---

## 03. Executive Dashboard & Operational Command Center `[OPERATIONS]`

The centralized landing hub providing real-time KPI metrics, assistance totals, remaining budget capacity, and one-click navigation to all active modules.

![Figure 03.1: Executive Dashboard & Operational Command Center](manual_screenshots/03_dashboard_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Summary Analytics:** Review total registered beneficiaries (40,561), civil registry links, active assistance cases, and remaining funding pools.
- 2. **Direct Module Dispatch:** Click any module tile (Masterlist, Aid Requests, Budget Waterfall, Project Distribution, Cash-for-Work, Seminar Attendance, GGMS Transactions, Reports) to open its operational workspace.
- 3. **Background Sync Status:** Bottom indicator shows real-time online/offline and GGMS central server synchronization states.

> **🛡️ System Policy:** The Dashboard layout is locked to maintain interface consistency across all municipal workstations.

> **💡 Pro-Tip:** Use the sidebar navigation drawer for rapid multitasking across different active distribution events.

---

## 04. Validated Beneficiaries Masterlist & Digital ID System `[OPERATIONS]`

The master registry of all validated citizens in the municipality, featuring comprehensive profiling, demographic tagging, and QR/Barcode digital ID generation.

![Figure 04.1: Validated Beneficiaries Masterlist & Digital ID System](manual_screenshots/04_masterlist_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Real-time Search & Filter:** Filter by demographics (Senior Citizens, PWDs, 4Ps, Solo Parents, Youth, Farmers, Fisherfolk).
- 2. **Photo Capture & Upload:** Directly capture beneficiary portrait photos via attached USB PC Camera or upload image files with integrated square cropping.
- 3. **Digital eKard Printing:** Select a beneficiary and click 'Print Digital ID' to generate compliant QR/Barcode badges for offline rapid scanning during distribution events.
- 4. **CRS Municipal Sync:** Synchronize citizen validation status directly against the central Civil Registry System.

> **🛡️ System Policy:** Never create duplicate entries. Use the CRS Sync ID or National ID number for deduplication.

> **💡 Pro-Tip:** Batch print QR digital badges prior to large distribution events to maximize onsite throughput.

---

## 05. Aid Requests & Assistance Case State Machine `[FINANCIAL]`

Manages individual walk-in assistance claims (Medical, Burial, Educational, Emergency Food) following a strict 4-stage approval and release pipeline.

![Figure 05.1: Aid Requests & Assistance Case State Machine](manual_screenshots/05_aid_request_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **State Pipeline:** Requests progress strictly through: `Pending` ➔ `Under Review` ➔ `Approved` ➔ `Released` (or `Rejected`/`Cancelled`).
- 2. **New Request Filing:** Search beneficiary from masterlist, select Assistance Category, enter requested amount, and attach required proof documents.
- 3. **Approval & Allocation:** Admin reviews the case and sets the official `Approved Amount`. Funds cannot be disbursed without prior approval.
- 4. **Fund Release & Budget Waterfall Deduction:** Releasing assistance automatically creates a `BudgetLedgerEntry` that consumes from the earmarked Assistance Case pool, maintaining strict fiscal accountability.

> **🛡️ System Policy:** Every released assistance case must be coupled with an explicit ApprovedAmount and authorized officer.

> **💡 Pro-Tip:** Attach verified digital copies of hospital bills or death certificates to accelerate the approval stage.

---

## 06. Budget Management & Dual-Stream Waterfall Ledger `[FINANCIAL]`

The financial engine of eKalinga+, orchestrating government appropriations (GGMS) and private donations through an automated waterfall disbursement hierarchy.

![Figure 06.1: Budget Management & Dual-Stream Waterfall Ledger](manual_screenshots/06_budget_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Dual Inflow Streams:** Tracks GGMS Government Allocations alongside Private Donations with reference numbers and proof receipts.
- 2. **Budget Earmarking:** Funds are categorized into sub-budgets (Aid Requests, Cash-for-Work, Project Distributions, Seminars).
- 3. **Waterfall Hierarchy:** Releases automatically deduct first from the specific earmarked project fund, then cascade to the general assistance pool.
- 4. **Create New Project (1:1 Funding):** Directly spawn projects from a selected donation/GGMS fund without breaking ledger continuity.

> **🛡️ System Policy:** Independent or disconnected budget records that bypass the waterfall ledger are strictly forbidden.

> **💡 Pro-Tip:** Audit remaining project budgets weekly to ensure adequate reserve for incoming emergency claims.

---

## 07. Project Distribution & Disbursement Execution `[FINANCIAL]`

High-throughput operational module for disbursing bulk goods and financial assistance to enrolled masterlist beneficiaries.

![Figure 07.1: Project Distribution & Disbursement Execution](manual_screenshots/07_project_distribution_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Project Selection:** Load active distribution project (Cash / In-Kind Goods) linked to its funding source.
- 2. **Participant Queue:** Displays full participant roster categorized into `RELEASED / CLAIMED`, `PENDING (Requirements Lacking)`, and `UNRELEASED`.
- 3. **Rapid Identification:** Scan physical QR ID card, type Beneficiary ID, or double-click list item.
- 4. **Requirements Verification:** Check mandatory attachments (Cedula, Barangay Certificate). Click 'Confirm Release' to payout or 'Mark Pending' if documents are missing.

> **🛡️ System Policy:** Beneficiary enrollment is manual or batch-selected from approved masterlist records only (no demographic auto-enrollment).

> **💡 Pro-Tip:** Use a 2D barcode scanner gun set to USB HID mode for sub-second verification per beneficiary.

---

## 08. Cash-for-Work Attendance & Payout Operations `[FINANCIAL]`

Complete lifecycle management for Cash-for-Work community projects, daily worker attendance logging, and automated wage payout disbursement.

![Figure 08.1: Cash-for-Work Attendance & Payout Operations](manual_screenshots/08_cash_for_work_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Event Workspace:** Create work activity (e.g. Barangay Clean-up, Tree Planting), assign daily wage rate and scheduled days.
- 2. **Beneficiary Enrollment:** Add registered workers from the masterlist to the event roster.
- 3. **Barcode/OCR Attendance Scan:** Capture morning time-in and afternoon time-out using USB barcode gun or live PC camera OCR.
- 4. **Automated Payroll Calculation:** System aggregates verified attendance logs into the Payout view (Calculates Days Rendered × Daily Wage Rate).

> **🛡️ System Policy:** Payouts require verified attendance records and pull directly from the CashForWork budget bucket.

> **💡 Pro-Tip:** Ensure camera lighting is adequate when utilizing the built-in OCR visual scanner.

---

## 09. Cash-for-Work Payout Disbursement Ledger `[FINANCIAL]`

The payroll disbursement terminal for releasing wages to Cash-for-Work participants upon event completion.

![Figure 09.1: Cash-for-Work Payout Disbursement Ledger](manual_screenshots/09_cash_for_work_payout_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Review Earned Wages:** System computes exact payout per participant based on logged attendance days.
- 2. **Disbursement Confirmation:** Process individual or batch payouts with receipt generation.
- 3. **Audit Trail:** Every released wage logs a ledger entry tied to the Cash-for-Work budget stream.

> **🛡️ System Policy:** Ensure all daily attendance logs are finalized before initiating payout disbursement.

> **💡 Pro-Tip:** Print the disbursement liquidation summary immediately after completing payroll.

---

## 10. Seminar & Training Attendance Verification `[OPERATIONS]`

Specialized module for conducting training sessions, workshops, and livelihood seminars with real-time barcode/QR scan tracking.

![Figure 10.1: Seminar & Training Attendance Verification](manual_screenshots/10_seminar_attendance_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Seminar Setup:** Define seminar topic, venue, date, and target participants.
- 2. **Rapid Scanner Entry:** Position camera or handheld scanner to log attendees as they arrive.
- 3. **Instant Attendance Summary:** Real-time stats surface total attendees, check-in timestamps, and demographic breakdown.

> **🛡️ System Policy:** All seminar projects originate from an earmarked funding stream in the Budget module.

> **💡 Pro-Tip:** Keep the live log window open to verify successful scans as attendees enter the venue.

---

## 11. GGMS Consolidated Transactions & Sync Audit `[OPERATIONS]`

Centralized transaction monitoring linking local eKalinga+ disbursements with the municipal Government Grants Management System (GGMS).

![Figure 11.1: GGMS Consolidated Transactions & Sync Audit](manual_screenshots/11_ggms_transactions_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Synced Record Inspection:** Review all assistance claims, distributions, and payouts transmitted to GGMS.
- 2. **Filter by Sync State:** Filter by Synchronized, Pending Sync, or Conflict.
- 3. **Reconciliation Audit:** Inspect timestamp, batch reference, transaction hash, and approving officer details.

> **🛡️ System Policy:** Ensure periodic sync is active when operating on connected LAN/Remote networks.

> **💡 Pro-Tip:** Check the conflict tab after reconnecting an offline workstation to resolve duplicate claims.

---

## 12. Reports, Analytics & Official Export Engine `[GOVERNANCE]`

Comprehensive document generation engine creating formatted government reports, audit sheets, and statistical summaries.

![Figure 12.1: Reports, Analytics & Official Export Engine](manual_screenshots/12_reports_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Select Report Template:** Masterlist Summary, Distribution Liquidation Sheet, Aid Assistance Ledger, or Cash-for-Work Payroll.
- 2. **Date & Category Filtering:** Define report timeframes and program categories.
- 3. **Print Preview & Export:** Preview formatted documents with official LGU headers and export directly to PDF or Excel.

> **🛡️ System Policy:** All official exported reports automatically include the municipal watermark and signature fields.

> **💡 Pro-Tip:** Use PDF export for archived official submissions and Excel for statistical aggregation.

---

## 13. Scanning Portal & Remote Scanner Gateway `[OPERATIONS]`

Network terminal for connecting mobile devices and secondary hardware scanners across the local barangay hall network.

![Figure 13.1: Scanning Portal & Remote Scanner Gateway](manual_screenshots/13_scanning_portal_page.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **Scanner Session PIN:** Generate secure, time-expiring PIN codes to pair remote Android/iOS scanning terminals.
- 2. **Live Feed Monitoring:** Monitor multiple active scanner gates simultaneously during massive distribution events.
- 3. **Hardware Scanner Integration:** Compatible with USB 1D/2D Barcode Guns, PC Webcams, and Serial scanners without extra drivers.

> **🛡️ System Policy:** Session PINs automatically expire after the configured timeout period for security.

> **💡 Pro-Tip:** Assign separate scanner PINs to each gate operator during municipal-wide relief operations.

---

## 14. System Settings, Security & Database Backup Recovery `[SECURITY]`

Central administration console governing system identity, SuperAdmin user management, password-protected overlays, and database backup chains.

![Figure 14.1: System Settings, Security & Database Backup Recovery](manual_screenshots/14_settings_window.png)

### Operational Workflow & Step-by-Step Flow:

- 1. **System Profile:** Set municipal name, address, branding logo, and theme preferences.
- 2. **User Management & Granular Permissions:** SuperAdmin manages staff accounts and assigns granular module access checkboxes.
- 3. **Password Protection:** Sensitive areas (App Database, Remote Snapshot, GGMS Source) require admin password re-entry.
- 4. **Database Backup & Recovery:** Execute Full Baseline Backups, Incremental Backups, and point-in-time Restores directly from the UI.

> **🛡️ System Policy:** Never delete database rows. Only soft deletions (`IsDeleted`) are permitted. Always create a Full Backup before major server migrations.

> **💡 Pro-Tip:** Schedule incremental daily backups to an external encrypted USB drive for disaster recovery.

---
