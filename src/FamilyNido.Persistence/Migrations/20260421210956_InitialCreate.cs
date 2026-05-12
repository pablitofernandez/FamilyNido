using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyNido.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "families",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    location_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_families", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_wall_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "es-ES"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    first_course = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    second_course = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meal_plan_slots", x => x.id);
                    table.ForeignKey(
                        name: "fk_meal_plan_slots_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "school_holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_holidays", x => x.id);
                    table.ForeignKey(
                        name: "fk_school_holidays_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_digest_runs",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_digest_runs", x => new { x.user_id, x.local_date });
                    table.ForeignKey(
                        name: "fk_email_digest_runs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "family_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    member_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    photo_path = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    color_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_family_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_family_members_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_family_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "google_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    encrypted_refresh_token = table.Column<string>(type: "text", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_google_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_google_accounts_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_google_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_credentials", x => x.id);
                    table.CheckConstraint("ck_user_credentials_shape", "(provider = 0 AND provider_key IS NOT NULL AND password_hash IS NULL) OR (provider = 1 AND provider_key IS NULL AND password_hash IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_user_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_dashboard_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    widgets_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_dashboard_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_dashboard_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    digest_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    task_assigned_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    wall_mention_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extracurriculars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    weekly_days = table.Column<short>(type: "smallint", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    default_dropoff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_pickup_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extracurriculars", x => x.id);
                    table.ForeignKey(
                        name: "fk_extracurriculars_family_members_default_dropoff_member_id",
                        column: x => x.default_dropoff_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_extracurriculars_family_members_default_pickup_member_id",
                        column: x => x.default_pickup_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_extracurriculars_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "file_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relative_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    content_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_assets_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_file_assets_family_members_owner_member_id",
                        column: x => x.owner_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blood_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    allergies = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    chronic_conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_health_profiles_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "General"),
                    recurrence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    weekly_days = table.Column<short>(type: "smallint", nullable: true),
                    monthly_day = table.Column<int>(type: "integer", nullable: true),
                    time_of_day = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    points = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_floating = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_household_tasks_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_household_tasks_family_members_created_by_member_id",
                        column: x => x.created_by_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_household_tasks_family_members_responsible_member_id",
                        column: x => x.responsible_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "integration_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_integration_api_keys_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_integration_api_keys_family_members_author_member_id",
                        column: x => x.author_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    role_on_accept = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    dose = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    frequency = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medications", x => x.id);
                    table.ForeignKey(
                        name: "fk_medications_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_agenda_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    transport_mode = table.Column<int>(type: "integer", nullable: false),
                    is_away = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_agenda_patterns", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_agenda_patterns_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "school_day_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    dropoff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pickup_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    morning_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    afternoon_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_day_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_school_day_exceptions_family_members_dropoff_member_id",
                        column: x => x.dropoff_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_school_day_exceptions_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_school_day_exceptions_family_members_pickup_member_id",
                        column: x => x.pickup_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "school_day_schedules",
                columns: table => new
                {
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    dropoff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pickup_member_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_day_schedules", x => new { x.family_member_id, x.day_of_week });
                    table.ForeignKey(
                        name: "fk_school_day_schedules_family_members_dropoff_member_id",
                        column: x => x.dropoff_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_school_day_schedules_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_school_day_schedules_family_members_pickup_member_id",
                        column: x => x.pickup_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "school_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    grade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tutor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    transport_mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    morning_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    afternoon_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_school_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_school_profiles_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vaccinations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vaccinations", x => x.id);
                    table.ForeignKey(
                        name: "fk_vaccinations_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "linked_calendars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_calendar_id = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    color_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    is_imported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sync_token = table.Column<string>(type: "text", nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linked_calendars", x => x.id);
                    table.ForeignKey(
                        name: "fk_linked_calendars_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_linked_calendars_google_accounts_google_account_id",
                        column: x => x.google_account_id,
                        principalTable: "google_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extracurricular_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extracurricular_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    dropoff_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pickup_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extracurricular_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_extracurricular_exceptions_extracurriculars_extracurricular",
                        column: x => x.extracurricular_id,
                        principalTable: "extracurriculars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_extracurricular_exceptions_family_members_dropoff_member_id",
                        column: x => x.dropoff_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_extracurricular_exceptions_family_members_pickup_member_id",
                        column: x => x.pickup_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "wall_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    text_html = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    image_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wall_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_wall_messages_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wall_messages_family_members_author_member_id",
                        column: x => x.author_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wall_messages_file_assets_image_file_id",
                        column: x => x.image_file_id,
                        principalTable: "file_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "household_task_related_members",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_task_related_members", x => new { x.task_id, x.family_member_id });
                    table.ForeignKey(
                        name: "fk_household_task_related_members_family_members_family_member",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_task_related_members_household_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "household_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    completed_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_completions_family_members_completed_by_member_id",
                        column: x => x.completed_by_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_task_completions_household_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "household_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_agenda_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    pattern_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    transport_mode = table.Column<int>(type: "integer", nullable: true),
                    is_away = table.Column<bool>(type: "boolean", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_agenda_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_agenda_exceptions_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_agenda_exceptions_member_agenda_patterns_pattern_id",
                        column: x => x.pattern_id,
                        principalTable: "member_agenda_patterns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ical_uid = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    original_time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    html_link = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_events_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calendar_events_linked_calendars_linked_calendar_id",
                        column: x => x.linked_calendar_id,
                        principalTable: "linked_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wall_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    text_html = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wall_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_wall_comments_family_members_author_member_id",
                        column: x => x.author_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wall_comments_wall_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "wall_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wall_message_mentions",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wall_message_mentions", x => new { x.message_id, x.family_member_id });
                    table.ForeignKey(
                        name: "fk_wall_message_mentions_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wall_message_mentions_wall_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "wall_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wall_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reacted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wall_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_wall_reactions_family_members_member_id",
                        column: x => x.member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wall_reactions_wall_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "wall_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_event_members",
                columns: table => new
                {
                    calendar_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_event_members", x => new { x.calendar_event_id, x.family_member_id });
                    table.ForeignKey(
                        name: "fk_calendar_event_members_calendar_events_calendar_event_id",
                        column: x => x.calendar_event_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_event_members_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wall_comment_mentions",
                columns: table => new
                {
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wall_comment_mentions", x => new { x.comment_id, x.family_member_id });
                    table.ForeignKey(
                        name: "fk_wall_comment_mentions_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalTable: "family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wall_comment_mentions_wall_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "wall_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_members_family_member_id",
                table: "calendar_event_members",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_family_id_end_at",
                table: "calendar_events",
                columns: new[] { "family_id", "end_at" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_family_id_start_at",
                table: "calendar_events",
                columns: new[] { "family_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_linked_calendar_id_external_event_id",
                table: "calendar_events",
                columns: new[] { "linked_calendar_id", "external_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_extracurricular_exceptions_dropoff_member_id",
                table: "extracurricular_exceptions",
                column: "dropoff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracurricular_exceptions_extracurricular_id_date",
                table: "extracurricular_exceptions",
                columns: new[] { "extracurricular_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_extracurricular_exceptions_pickup_member_id",
                table: "extracurricular_exceptions",
                column: "pickup_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracurriculars_default_dropoff_member_id",
                table: "extracurriculars",
                column: "default_dropoff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracurriculars_default_pickup_member_id",
                table: "extracurriculars",
                column: "default_pickup_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracurriculars_family_id_is_archived",
                table: "extracurriculars",
                columns: new[] { "family_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_extracurriculars_family_member_id",
                table: "extracurriculars",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_family_members_family_id_is_active",
                table: "family_members",
                columns: new[] { "family_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_family_members_user_id",
                table: "family_members",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_assets_family_id",
                table: "file_assets",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_assets_owner_member_id",
                table: "file_assets",
                column: "owner_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_assets_relative_path",
                table: "file_assets",
                column: "relative_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_google_accounts_family_id",
                table: "google_accounts",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_google_accounts_user_id_email",
                table: "google_accounts",
                columns: new[] { "user_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_profiles_family_member_id",
                table: "health_profiles",
                column: "family_member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_household_task_related_members_family_member_id",
                table: "household_task_related_members",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_tasks_created_by_member_id",
                table: "household_tasks",
                column: "created_by_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_tasks_family_id",
                table: "household_tasks",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_household_tasks_family_id_is_archived",
                table: "household_tasks",
                columns: new[] { "family_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_household_tasks_responsible_member_id",
                table: "household_tasks",
                column: "responsible_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_api_keys_author_member_id",
                table: "integration_api_keys",
                column: "author_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_api_keys_family_id",
                table: "integration_api_keys",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_api_keys_token_hash",
                table: "integration_api_keys",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_family_id_consumed_at_revoked_at",
                table: "invitations",
                columns: new[] { "family_id", "consumed_at", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invitations_family_member_id",
                table: "invitations",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_linked_calendars_family_member_id",
                table: "linked_calendars",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_linked_calendars_google_account_id_external_calendar_id",
                table: "linked_calendars",
                columns: new[] { "google_account_id", "external_calendar_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_linked_calendars_google_account_id_is_imported",
                table: "linked_calendars",
                columns: new[] { "google_account_id", "is_imported" });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_slots_family_id_date_slot",
                table: "meal_plan_slots",
                columns: new[] { "family_id", "date", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_slots_family_id_first_course",
                table: "meal_plan_slots",
                columns: new[] { "family_id", "first_course" });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_slots_family_id_second_course",
                table: "meal_plan_slots",
                columns: new[] { "family_id", "second_course" });

            migrationBuilder.CreateIndex(
                name: "ix_medications_family_member_id_start_date",
                table: "medications",
                columns: new[] { "family_member_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_member_agenda_exceptions_family_id_date",
                table: "member_agenda_exceptions",
                columns: new[] { "family_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_member_agenda_exceptions_family_member_id_date_pattern_id",
                table: "member_agenda_exceptions",
                columns: new[] { "family_member_id", "date", "pattern_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_agenda_exceptions_pattern_id",
                table: "member_agenda_exceptions",
                column: "pattern_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_agenda_patterns_family_id",
                table: "member_agenda_patterns",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_agenda_patterns_family_member_id_day_of_week",
                table: "member_agenda_patterns",
                columns: new[] { "family_member_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_school_day_exceptions_dropoff_member_id",
                table: "school_day_exceptions",
                column: "dropoff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_day_exceptions_family_id_date",
                table: "school_day_exceptions",
                columns: new[] { "family_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_school_day_exceptions_family_member_id_date",
                table: "school_day_exceptions",
                columns: new[] { "family_member_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_school_day_exceptions_pickup_member_id",
                table: "school_day_exceptions",
                column: "pickup_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_day_schedules_dropoff_member_id",
                table: "school_day_schedules",
                column: "dropoff_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_day_schedules_pickup_member_id",
                table: "school_day_schedules",
                column: "pickup_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_school_holidays_family_id_start_date",
                table: "school_holidays",
                columns: new[] { "family_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_school_profiles_family_member_id",
                table: "school_profiles",
                column: "family_member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_completed_by_member_id",
                table: "task_completions",
                column: "completed_by_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_completions_task_id_occurrence_date",
                table: "task_completions",
                columns: new[] { "task_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_credentials_provider_provider_key",
                table: "user_credentials",
                columns: new[] { "provider", "provider_key" },
                unique: true,
                filter: "provider_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_credentials_user_id",
                table: "user_credentials",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vaccinations_family_member_id_date",
                table: "vaccinations",
                columns: new[] { "family_member_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_wall_comment_mentions_family_member_id",
                table: "wall_comment_mentions",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_comments_author_member_id",
                table: "wall_comments",
                column: "author_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_comments_message_id_created_at",
                table: "wall_comments",
                columns: new[] { "message_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wall_message_mentions_family_member_id",
                table: "wall_message_mentions",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_messages_author_member_id",
                table: "wall_messages",
                column: "author_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_messages_family_id_created_at",
                table: "wall_messages",
                columns: new[] { "family_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wall_messages_family_id_is_pinned_pinned_at",
                table: "wall_messages",
                columns: new[] { "family_id", "is_pinned", "pinned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_wall_messages_image_file_id",
                table: "wall_messages",
                column: "image_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_reactions_member_id",
                table: "wall_reactions",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_wall_reactions_message_id_member_id_emoji",
                table: "wall_reactions",
                columns: new[] { "message_id", "member_id", "emoji" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_event_members");

            migrationBuilder.DropTable(
                name: "email_digest_runs");

            migrationBuilder.DropTable(
                name: "extracurricular_exceptions");

            migrationBuilder.DropTable(
                name: "health_profiles");

            migrationBuilder.DropTable(
                name: "household_task_related_members");

            migrationBuilder.DropTable(
                name: "integration_api_keys");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "meal_plan_slots");

            migrationBuilder.DropTable(
                name: "medications");

            migrationBuilder.DropTable(
                name: "member_agenda_exceptions");

            migrationBuilder.DropTable(
                name: "school_day_exceptions");

            migrationBuilder.DropTable(
                name: "school_day_schedules");

            migrationBuilder.DropTable(
                name: "school_holidays");

            migrationBuilder.DropTable(
                name: "school_profiles");

            migrationBuilder.DropTable(
                name: "task_completions");

            migrationBuilder.DropTable(
                name: "user_credentials");

            migrationBuilder.DropTable(
                name: "user_dashboard_preferences");

            migrationBuilder.DropTable(
                name: "user_notification_preferences");

            migrationBuilder.DropTable(
                name: "vaccinations");

            migrationBuilder.DropTable(
                name: "wall_comment_mentions");

            migrationBuilder.DropTable(
                name: "wall_message_mentions");

            migrationBuilder.DropTable(
                name: "wall_reactions");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "extracurriculars");

            migrationBuilder.DropTable(
                name: "member_agenda_patterns");

            migrationBuilder.DropTable(
                name: "household_tasks");

            migrationBuilder.DropTable(
                name: "wall_comments");

            migrationBuilder.DropTable(
                name: "linked_calendars");

            migrationBuilder.DropTable(
                name: "wall_messages");

            migrationBuilder.DropTable(
                name: "google_accounts");

            migrationBuilder.DropTable(
                name: "file_assets");

            migrationBuilder.DropTable(
                name: "family_members");

            migrationBuilder.DropTable(
                name: "families");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
