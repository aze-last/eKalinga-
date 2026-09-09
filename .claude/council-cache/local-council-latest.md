# Local Council Session: Porting eKalinga+ to Laravel (Scope & Architecture)

> **Local council** — these perspectives all come from Claude playing different roles, not from different AI vendors. Treat agreement as a shared starting point to pressure-test, not as independent confirmation.

---

## 🗳️ Devil's Advocate

### Position
Porting a desktop WPF app with hardware scanner integration (QR/OCR) and offline-tolerant flows to a web environment introduces web concurrency risks, field connectivity challenges, and scanner integration hurdles.

### Key points
- **Hardware Integration Shift:** Desktop barcode/QR scanning (USB HID / DirectShow webcam) must be replaced with browser WebRTC/HTML5 QR scanner APIs or mobile device scanning.
- **Race Condition Vulnerabilities:** Multi-admin distribution checkout during simultaneous aid payouts can overdraw earmarked budget buckets without database-level row locking (`lockForUpdate()`).
- **Dry/Duplication Trap:** Cash-for-Work and Seminars share identical workflows; treating them as separate database tables/controllers in Laravel will double technical debt.
- **Connectivity Reality:** Barangay distribution sites often lack reliable internet; a pure webapp without offline caching/PWA queuing risks operational halt during distributions.

### Risks & blind spots
- Failure to account for spotty field internet connectivity during mass distribution days where desktop previously held local SQLite cache.

### Confidence
`high` — Hardware integration, offline field use, and concurrency are common pitfalls in desktop-to-web conversions.

---

## 🗳️ Simplicity Champion

### Position
Avoid complex SPA frameworks, microservices, or event sourcing; use a streamlined Laravel 11/12 monolith with Livewire (or Blade + Alpine.js) and a unified polymorphic event schema.

### Key points
- **Laravel Monolith + Livewire:** Replicates the modal overlay/instant reactivity UX of WPF without API maintenance overhead.
- **Unified Event Schema:** Create a single `events` table with `type` enum (`cash_for_work`, `seminar`), sharing `event_participants`, `attendances`, and `payouts`.
- **Single Waterfall Service:** Encapsulate the entire budget waterfall in one `BudgetWaterfallService` class within standard `DB::transaction()`.
- **Immutable Ledger:** A simple append-only `budget_ledger_entries` table with soft-deleted funding records satisfies all audit and waterfall requirements.

### Risks & blind spots
- Over-engineering with event-sourcing or microservices when standard ACID SQL transactions with clean service classes fully suffice.

### Confidence
`high` — Laravel's Eloquent, Service layers, and Livewire map 1:1 to WPF MVVM concepts.

---

## 🗳️ Security Auditor

### Position
A web-based financial disbursement platform requires strict database concurrency locking, immutable financial records, granular authorization policies, and PII protection for beneficiary data.

### Key points
- **Pessimistic Concurrency Locking:** Enforce `SELECT ... FOR UPDATE` (`lockForUpdate()`) on source funds during allocation and disbursement to prevent double-spending.
- **Immutable Ledger Entries:** `budget_ledger_entries` must be strictly append-only (no `UPDATE` or `DELETE` permissions at the DB/ORM policy level).
- **Soft Deletion & Role Policies:** Implement Laravel `SoftDeletes` across all models (preserving the hard rule: never delete DB rows). Restrict user management to `SuperAdmin`.
- **GGMS API / Transaction Integrity:** Secure GGMS transaction import endpoints with signed webhooks/tokens and idempotency keys to prevent duplicate grant credits.

### Risks & blind spots
- IDOR or missing authorization checks on distribution claiming endpoints allowing unauthorized claims or payout triggers.

### Confidence
`high` — Disbursement and ledger systems must be protected against race conditions and unauthorized status updates.

---

## 🗳️ Scalability Architect

### Position
High-volume disbursement events and bulk beneficiary enrollments require queued background processing, indexed ledger tables, and asynchronous sync routines.

### Key points
- **Queued Bulk Enrollment:** Bulk assigning hundreds of masterlist beneficiaries to projects should use chunked batch inserts or queued jobs (`Dispatchable`).
- **GGMS Sync in Background:** Process GGMS synchronization and transaction polling via scheduled console commands/queue workers with retry backoff.
- **Indexed Ledger Architecture:** Composite indexing on `[source_fund_id, created_at]` and cached balance calculations to prevent slow aggregations during peak payouts.
- **Asynchronous Document Generation:** Payroll generation, distribution vouchers, and PDF reports must be queued to avoid PHP HTTP worker timeouts.

### Risks & blind spots
- Synchronous PDF generation and bulk payroll processing causing HTTP 504 timeouts during active distribution events.

### Confidence
`high` — Barangay distribution events create burst traffic during payout windows.

---

# Synthesis & Strategic Direction

### Shared Starting Points
- **Architecture**: Laravel 11/12 monolith (Blade/Livewire) is the optimal match to preserve the desktop app's overlay/reactive workflows.
- **Data Unification**: Cash-for-Work and Seminars should share a unified `events` table/domain model with a `type` differentiator.
- **Budget Integrity**: The Budget Waterfall must be an isolated, transaction-wrapped service managing an immutable ledger (`budget_ledger_entries`).

### Genuine Tensions
- **Field Scanner & Offline vs. Simplicity**: A pure webapp is simplest to build, but field distribution requires barcode/QR scanning and offline tolerance. 
  - *Recommendation*: Start with HTML5 browser camera scanning (`html5-qrcode`) and standard responsive web; consider PWA local storage queuing only if offline field distribution is an explicit requirement.

### Blind Spots Flagged
1. **Concurrency during Payouts**: Web allows concurrent claims; explicit DB transactions with row-level locks are mandatory.
2. **Batch Processing Timeouts**: Bulk beneficiary enrollment and voucher PDF printing must be handled asynchronously via Laravel Queues.

### Suggested Next Steps for Laravel Architecture
1. **Database Schema Design**:
   - `funding_sources` (GGMS grants & Private donations)
   - `budget_ledger_entries` (immutable waterfall transaction log)
   - `projects` & `project_beneficiaries` (distribution events & claiming status)
   - `events`, `event_participants`, `event_attendances`, `event_payouts` (CFW & Seminars)
   - `ggms_transactions` (sync log & status)
2. **Core Domain Services**:
   - `BudgetWaterfallService`
   - `DistributionClaimService`
   - `EventPayoutService`
   - `GgmsSyncService`
