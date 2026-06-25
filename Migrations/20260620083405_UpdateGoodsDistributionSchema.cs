using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceShiftingManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGoodsDistributionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistance_case_budgets",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    budget_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    budget_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    assistance_type = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    budget_cap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistance_case_budgets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "barangay_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    asset_tag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    added_on = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_barangay_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "beneficiary_assistance_ledger",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    civil_registry_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    beneficiary_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    source_module = table.Column<string>(type: "TEXT", nullable: false),
                    source_record_id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    release_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    remarks = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    recorded_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiary_assistance_ledger", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "beneficiary_digital_ids",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "INTEGER", nullable: false),
                    household_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    card_number = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    qr_payload = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    photo_path = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    issued_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    issued_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_printed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beneficiary_digital_ids", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "BeneficiaryStaging",
                columns: table => new
                {
                    StagingID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResidentsId = table.Column<long>(type: "INTEGER", nullable: true),
                    BeneficiaryId = table.Column<string>(type: "TEXT", nullable: true),
                    CivilRegistryId = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: true),
                    MiddleName = table.Column<string>(type: "TEXT", nullable: true),
                    FullName = table.Column<string>(type: "TEXT", nullable: true),
                    Sex = table.Column<string>(type: "TEXT", nullable: true),
                    DateOfBirth = table.Column<string>(type: "TEXT", nullable: true),
                    Age = table.Column<string>(type: "TEXT", nullable: true),
                    MaritalStatus = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    IsPwd = table.Column<bool>(type: "INTEGER", nullable: false),
                    PwdIdNo = table.Column<string>(type: "TEXT", nullable: true),
                    DisabilityType = table.Column<string>(type: "TEXT", nullable: true),
                    CauseOfDisability = table.Column<string>(type: "TEXT", nullable: true),
                    IsSenior = table.Column<bool>(type: "INTEGER", nullable: false),
                    SeniorIdNo = table.Column<string>(type: "TEXT", nullable: true),
                    VerificationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkedHouseholdId = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkedHouseholdMemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewNotes = table.Column<string>(type: "TEXT", nullable: true),
                    PhotoPath = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficiaryStaging", x => x.StagingID);
                });

            migrationBuilder.CreateTable(
                name: "cash_for_work_budgets",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    budget_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    budget_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    budget_cap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_for_work_budgets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ggms_allocation_cache",
                columns: table => new
                {
                    GgmsAllocationCacheId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OfficeCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OfficeName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    YearlyBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SpentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceRowId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ggms_allocation_cache", x => x.GgmsAllocationCacheId);
                });

            migrationBuilder.CreateTable(
                name: "ggms_pending_transaction_cache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ggms_pending_transaction_cache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ggms_transaction_cache",
                columns: table => new
                {
                    GgmsTransactionCacheId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    OfficeCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ggms_transaction_cache", x => x.GgmsTransactionCacheId);
                });

            migrationBuilder.CreateTable(
                name: "households",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    household_code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    head_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    address_line = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    purok = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    contact_number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scanner_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    mode = table.Column<string>(type: "TEXT", nullable: false),
                    session_token = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    pin_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    cash_for_work_event_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_accessed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_scanned_payload = table.Column<string>(type: "TEXT", nullable: true),
                    last_scanned_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scanner_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_metadata",
                columns: table => new
                {
                    TableName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_metadata", x => x.TableName);
                });

            migrationBuilder.CreateTable(
                name: "system_registrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    company_serial_number = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    company_name = table.Column<string>(type: "TEXT", maxLength: 180, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_validated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_borrowings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    asset_id = table.Column<int>(type: "INTEGER", nullable: false),
                    beneficiary_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    beneficiary_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    borrow_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    due_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    return_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    condition_out = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    condition_in = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_borrowings", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipment_borrowings_barangay_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "barangay_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    household_id = table.Column<int>(type: "INTEGER", nullable: false),
                    full_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    relationship_to_head = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    occupation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    is_cash_for_work_eligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_members_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    entity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    entity_id = table.Column<int>(type: "INTEGER", nullable: true),
                    details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ip_address = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_activity_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    can_access_dashboard = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_master_list = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_assistance_cases = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_budget = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_distribution = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_cash_for_work = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_borrowing = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_reports = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_ggms_transactions = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_app_database = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_ggms_budget_source = table.Column<bool>(type: "INTEGER", nullable: false),
                    can_access_scanning_portal = table.Column<bool>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    full_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    nickname = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    emergency_contact_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    emergency_contact_phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    photo_path = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistance_cases",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    case_number = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    household_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    validated_beneficiary_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    validated_beneficiary_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    validated_civil_registry_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    assistance_type = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    release_kind = table.Column<string>(type: "varchar(32)", nullable: false),
                    priority = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    requested_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    approved_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    requested_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    scheduled_release_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    summary = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    resolution_notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    reviewed_by_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    assistance_case_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    budget_ledger_entry_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistance_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_assistance_cases_assistance_case_budgets_assistance_case_budget_id",
                        column: x => x.assistance_case_budget_id,
                        principalTable: "assistance_case_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assistance_cases_household_members_household_member_id",
                        column: x => x.household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_assistance_cases_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ayuda_programs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    program_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    program_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    program_type = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    assistance_type = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    release_kind = table.Column<string>(type: "varchar(32)", nullable: false),
                    unit_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    item_description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    item_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    quantity_per_beneficiary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    unit_of_measure = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    start_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    end_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    budget_cap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    distribution_status = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    source_donation_id = table.Column<int>(type: "INTEGER", nullable: true),
                    source_ggms_budget_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ayuda_project_beneficiaries",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "INTEGER", nullable: false),
                    household_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    beneficiary_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    civil_registry_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    full_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    status_reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    status_updated_by_user_id = table.Column<int>(type: "INTEGER", nullable: true),
                    status_updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    added_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_project_beneficiaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_ayuda_project_beneficiaries_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ayuda_project_budget_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: false),
                    budget_bucket_id = table.Column<int>(type: "INTEGER", nullable: false),
                    budget_bucket_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_project_budget_sources", x => x.id);
                    table.ForeignKey(
                        name: "FK_ayuda_project_budget_sources_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ayuda_project_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "INTEGER", nullable: false),
                    project_beneficiary_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    beneficiary_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    civil_registry_id = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    full_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    assistance_type_snapshot = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    item_description_snapshot = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    item_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    quantity_snapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    unit_of_measure_snapshot = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    unit_amount_snapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    qr_payload = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    remarks = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    claimed_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    claimed_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ayuda_project_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_ayuda_project_claims_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_ledger_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    entry_type = table.Column<string>(type: "TEXT", nullable: false),
                    feature_source = table.Column<string>(type: "TEXT", nullable: false),
                    source_record_id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    assistance_case_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    cash_for_work_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    recipient_count = table.Column<int>(type: "INTEGER", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    government_portion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    private_portion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    entry_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    remarks = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    release_kind = table.Column<string>(type: "varchar(32)", nullable: true),
                    recorded_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_budget_ledger_entries_assistance_case_budgets_assistance_case_budget_id",
                        column: x => x.assistance_case_budget_id,
                        principalTable: "assistance_case_budgets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_budget_ledger_entries_ayuda_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_budget_ledger_entries_cash_for_work_budgets_cash_for_work_budget_id",
                        column: x => x.cash_for_work_budget_id,
                        principalTable: "cash_for_work_budgets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "government_budget_snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    office_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    office_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    yearly_budget_id = table.Column<int>(type: "INTEGER", nullable: false),
                    allocated_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    spent_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    source_row_id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    sync_status = table.Column<string>(type: "TEXT", nullable: false),
                    synced_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    target_program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    target_assistance_case_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    target_cash_for_work_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_government_budget_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_government_budget_snapshots_assistance_case_budgets_target_assistance_case_budget_id",
                        column: x => x.target_assistance_case_budget_id,
                        principalTable: "assistance_case_budgets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_government_budget_snapshots_ayuda_programs_target_program_id",
                        column: x => x.target_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_government_budget_snapshots_cash_for_work_budgets_target_cash_for_work_budget_id",
                        column: x => x.target_cash_for_work_budget_id,
                        principalTable: "cash_for_work_budgets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "private_donations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    donor_type = table.Column<string>(type: "TEXT", nullable: false),
                    donor_name = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_received = table.Column<DateTime>(type: "TEXT", nullable: false),
                    reference_number = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    remarks = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    proof_type = table.Column<string>(type: "TEXT", nullable: false),
                    proof_reference_number = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    proof_file_path = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    received_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    target_program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    target_assistance_case_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    target_cash_for_work_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_donations", x => x.id);
                    table.ForeignKey(
                        name: "FK_private_donations_assistance_case_budgets_target_assistance_case_budget_id",
                        column: x => x.target_assistance_case_budget_id,
                        principalTable: "assistance_case_budgets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_private_donations_ayuda_programs_target_program_id",
                        column: x => x.target_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_private_donations_cash_for_work_budgets_target_cash_for_work_budget_id",
                        column: x => x.target_cash_for_work_budget_id,
                        principalTable: "cash_for_work_budgets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "cash_for_work_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    location = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    event_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    finish_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    benefit_type = table.Column<string>(type: "TEXT", nullable: false),
                    benefit_description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    start_time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    event_kind = table.Column<string>(type: "TEXT", nullable: false),
                    ayuda_program_id = table.Column<int>(type: "INTEGER", nullable: true),
                    cash_for_work_budget_id = table.Column<int>(type: "INTEGER", nullable: true),
                    budget_ledger_entry_id = table.Column<int>(type: "INTEGER", nullable: true),
                    unit_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    release_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    released_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_for_work_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_for_work_events_ayuda_programs_ayuda_program_id",
                        column: x => x.ayuda_program_id,
                        principalTable: "ayuda_programs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_cash_for_work_events_budget_ledger_entries_budget_ledger_entry_id",
                        column: x => x.budget_ledger_entry_id,
                        principalTable: "budget_ledger_entries",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_cash_for_work_events_cash_for_work_budgets_cash_for_work_budget_id",
                        column: x => x.cash_for_work_budget_id,
                        principalTable: "cash_for_work_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cash_for_work_events_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_for_work_participants",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    event_id = table.Column<int>(type: "INTEGER", nullable: false),
                    beneficiary_staging_id = table.Column<int>(type: "INTEGER", nullable: true),
                    household_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    added_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_for_work_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_for_work_participants_BeneficiaryStaging_beneficiary_staging_id",
                        column: x => x.beneficiary_staging_id,
                        principalTable: "BeneficiaryStaging",
                        principalColumn: "StagingID");
                    table.ForeignKey(
                        name: "FK_cash_for_work_participants_cash_for_work_events_event_id",
                        column: x => x.event_id,
                        principalTable: "cash_for_work_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cash_for_work_participants_household_members_household_member_id",
                        column: x => x.household_member_id,
                        principalTable: "household_members",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_cash_for_work_participants_users_added_by_user_id",
                        column: x => x.added_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_for_work_attendance",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    participant_id = table.Column<int>(type: "INTEGER", nullable: false),
                    attendance_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    ocr_extracted_name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    recorded_by_user_id = table.Column<int>(type: "INTEGER", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_for_work_attendance", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_for_work_attendance_cash_for_work_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "cash_for_work_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cash_for_work_attendance_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_user_id",
                table: "activity_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_case_budgets_budget_code",
                table: "assistance_case_budgets",
                column: "budget_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_assistance_case_budget_id",
                table: "assistance_cases",
                column: "assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_ayuda_program_id",
                table: "assistance_cases",
                column: "ayuda_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_budget_ledger_entry_id",
                table: "assistance_cases",
                column: "budget_ledger_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_case_number",
                table: "assistance_cases",
                column: "case_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_household_id",
                table: "assistance_cases",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_assistance_cases_household_member_id",
                table: "assistance_cases",
                column: "household_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_programs_program_code",
                table: "ayuda_programs",
                column: "program_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_programs_source_donation_id",
                table: "ayuda_programs",
                column: "source_donation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_programs_source_ggms_budget_id",
                table: "ayuda_programs",
                column: "source_ggms_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_project_beneficiaries_ayuda_program_id_beneficiary_staging_id",
                table: "ayuda_project_beneficiaries",
                columns: new[] { "ayuda_program_id", "beneficiary_staging_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_project_budget_sources_ayuda_program_id_priority",
                table: "ayuda_project_budget_sources",
                columns: new[] { "ayuda_program_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ayuda_project_claims_ayuda_program_id_beneficiary_staging_id",
                table: "ayuda_project_claims",
                columns: new[] { "ayuda_program_id", "beneficiary_staging_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_barangay_assets_asset_tag",
                table: "barangay_assets",
                column: "asset_tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_assistance_ledger_beneficiary_id",
                table: "beneficiary_assistance_ledger",
                column: "beneficiary_id");

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_assistance_ledger_civil_registry_id",
                table: "beneficiary_assistance_ledger",
                column: "civil_registry_id");

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_digital_ids_beneficiary_staging_id",
                table: "beneficiary_digital_ids",
                column: "beneficiary_staging_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_digital_ids_card_number",
                table: "beneficiary_digital_ids",
                column: "card_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beneficiary_digital_ids_qr_payload",
                table: "beneficiary_digital_ids",
                column: "qr_payload",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryStaging_CivilRegistryId",
                table: "BeneficiaryStaging",
                column: "CivilRegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_BeneficiaryStaging_ResidentsId",
                table: "BeneficiaryStaging",
                column: "ResidentsId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_assistance_case_budget_id",
                table: "budget_ledger_entries",
                column: "assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_cash_for_work_budget_id",
                table: "budget_ledger_entries",
                column: "cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_feature_source_source_record_id_entry_type",
                table: "budget_ledger_entries",
                columns: new[] { "feature_source", "source_record_id", "entry_type" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_ledger_entries_program_id",
                table: "budget_ledger_entries",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_attendance_participant_id",
                table: "cash_for_work_attendance",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_attendance_recorded_by_user_id",
                table: "cash_for_work_attendance",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_budgets_budget_code",
                table: "cash_for_work_budgets",
                column: "budget_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_ayuda_program_id",
                table: "cash_for_work_events",
                column: "ayuda_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_budget_ledger_entry_id",
                table: "cash_for_work_events",
                column: "budget_ledger_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_cash_for_work_budget_id",
                table: "cash_for_work_events",
                column: "cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_events_created_by_user_id",
                table: "cash_for_work_events",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_participants_added_by_user_id",
                table: "cash_for_work_participants",
                column: "added_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_participants_beneficiary_staging_id",
                table: "cash_for_work_participants",
                column: "beneficiary_staging_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_participants_event_id_beneficiary_staging_id",
                table: "cash_for_work_participants",
                columns: new[] { "event_id", "beneficiary_staging_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_for_work_participants_household_member_id",
                table: "cash_for_work_participants",
                column: "household_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_borrowings_asset_id",
                table: "equipment_borrowings",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_borrowings_beneficiary_id",
                table: "equipment_borrowings",
                column: "beneficiary_id");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_assistance_case_budget_id",
                table: "government_budget_snapshots",
                column: "target_assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_cash_for_work_budget_id",
                table: "government_budget_snapshots",
                column: "target_cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_government_budget_snapshots_target_program_id",
                table: "government_budget_snapshots",
                column: "target_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_members_household_id",
                table: "household_members",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_households_household_code",
                table: "households",
                column: "household_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_assistance_case_budget_id",
                table: "private_donations",
                column: "target_assistance_case_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_cash_for_work_budget_id",
                table: "private_donations",
                column: "target_cash_for_work_budget_id");

            migrationBuilder.CreateIndex(
                name: "IX_private_donations_target_program_id",
                table: "private_donations",
                column: "target_program_id");

            migrationBuilder.CreateIndex(
                name: "IX_scanner_sessions_session_token",
                table: "scanner_sessions",
                column: "session_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_registrations_company_serial_number",
                table: "system_registrations",
                column: "company_serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_user_id",
                table: "user_permissions",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_user_id",
                table: "user_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_assistance_cases_ayuda_programs_ayuda_program_id",
                table: "assistance_cases",
                column: "ayuda_program_id",
                principalTable: "ayuda_programs",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_assistance_cases_budget_ledger_entries_budget_ledger_entry_id",
                table: "assistance_cases",
                column: "budget_ledger_entry_id",
                principalTable: "budget_ledger_entries",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_ayuda_programs_government_budget_snapshots_source_ggms_budget_id",
                table: "ayuda_programs",
                column: "source_ggms_budget_id",
                principalTable: "government_budget_snapshots",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_ayuda_programs_private_donations_source_donation_id",
                table: "ayuda_programs",
                column: "source_donation_id",
                principalTable: "private_donations",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_government_budget_snapshots_assistance_case_budgets_target_assistance_case_budget_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_private_donations_assistance_case_budgets_target_assistance_case_budget_id",
                table: "private_donations");

            migrationBuilder.DropForeignKey(
                name: "FK_government_budget_snapshots_ayuda_programs_target_program_id",
                table: "government_budget_snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_private_donations_ayuda_programs_target_program_id",
                table: "private_donations");

            migrationBuilder.DropTable(
                name: "activity_logs");

            migrationBuilder.DropTable(
                name: "assistance_cases");

            migrationBuilder.DropTable(
                name: "ayuda_project_beneficiaries");

            migrationBuilder.DropTable(
                name: "ayuda_project_budget_sources");

            migrationBuilder.DropTable(
                name: "ayuda_project_claims");

            migrationBuilder.DropTable(
                name: "beneficiary_assistance_ledger");

            migrationBuilder.DropTable(
                name: "beneficiary_digital_ids");

            migrationBuilder.DropTable(
                name: "cash_for_work_attendance");

            migrationBuilder.DropTable(
                name: "equipment_borrowings");

            migrationBuilder.DropTable(
                name: "ggms_allocation_cache");

            migrationBuilder.DropTable(
                name: "ggms_pending_transaction_cache");

            migrationBuilder.DropTable(
                name: "ggms_transaction_cache");

            migrationBuilder.DropTable(
                name: "scanner_sessions");

            migrationBuilder.DropTable(
                name: "sync_metadata");

            migrationBuilder.DropTable(
                name: "system_registrations");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "cash_for_work_participants");

            migrationBuilder.DropTable(
                name: "barangay_assets");

            migrationBuilder.DropTable(
                name: "BeneficiaryStaging");

            migrationBuilder.DropTable(
                name: "cash_for_work_events");

            migrationBuilder.DropTable(
                name: "household_members");

            migrationBuilder.DropTable(
                name: "budget_ledger_entries");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "households");

            migrationBuilder.DropTable(
                name: "assistance_case_budgets");

            migrationBuilder.DropTable(
                name: "ayuda_programs");

            migrationBuilder.DropTable(
                name: "government_budget_snapshots");

            migrationBuilder.DropTable(
                name: "private_donations");

            migrationBuilder.DropTable(
                name: "cash_for_work_budgets");
        }
    }
}
