-- ─────────────────────────────────────────────────────────────────────────────
-- SULOP OFFLINE-FIRST: SYNC ID BACKFILL SCRIPT (Hostinger MySQL)
-- ─────────────────────────────────────────────────────────────────────────────
-- Run this script BEFORE applying the EF Core AddSyncIdToAllEntities migration
-- to ensure existing records receive a UUID. Alternatively, you can run this
-- immediately AFTER the migration if the migration generates warnings about
-- default values.
-- ─────────────────────────────────────────────────────────────────────────────

-- Set safe mode off temporarily if needed
-- SET SQL_SAFE_UPDATES = 0;

UPDATE users SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE user_permissions SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE activity_logs SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE user_profiles SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE system_registrations SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE households SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE household_members SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE assistance_cases SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE assistance_case_budgets SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE cash_for_work_budgets SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE beneficiary_staging SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE beneficiary_assistance_ledger_entries SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE beneficiary_digital_ids SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE ayuda_programs SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE ayuda_project_beneficiaries SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE ayuda_project_claims SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE ayuda_project_budget_sources SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE government_budget_snapshots SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE private_donations SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE budget_ledger_entries SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE cash_for_work_events SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE cash_for_work_participants SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE cash_for_work_attendances SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE scanner_sessions SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE barangay_assets SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';
UPDATE equipment_borrowings SET SyncId = UUID() WHERE SyncId = '00000000-0000-0000-0000-000000000000';

-- SET SQL_SAFE_UPDATES = 1;
