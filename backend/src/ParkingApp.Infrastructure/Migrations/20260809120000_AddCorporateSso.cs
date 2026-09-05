using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingApp.Infrastructure.Data;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809120000_AddCorporateSso")]
    public partial class AddCorporateSso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Backfill unique slugs from company name + short id suffix
            migrationBuilder.Sql("""
                UPDATE "Companies" c
                SET "Slug" = lower(regexp_replace(trim(c."Name"), '[^a-zA-Z0-9]+', '-', 'g'))
                    || '-' || substr(replace(c."Id"::text, '-', ''), 1, 8)
                WHERE c."Slug" IS NULL AND c."IsDeleted" = false;
                """);

            migrationBuilder.Sql("""
                UPDATE "Companies"
                SET "Slug" = regexp_replace("Slug", '^-+|-+$', '', 'g')
                WHERE "Slug" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE "Companies"
                SET "Slug" = 'company-' || substr(replace("Id"::text, '-', ''), 1, 8)
                WHERE ("Slug" IS NULL OR "Slug" = '' OR "Slug" = '-') AND "IsDeleted" = false;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Slug",
                table: "Companies",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateTable(
                name: "CompanySsoConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protocol = table.Column<short>(type: "smallint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ForceDisabledByPlatform = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ForceDisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForceDisabledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Authority = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientSecretProtected = table.Column<string>(type: "text", nullable: true),
                    MetadataUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttributeMappingJson = table.Column<string>(type: "text", nullable: true),
                    TokenEndpointAuthMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PasswordLoginAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AutoAcceptInvitationsOnSso = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ForceAuthn = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AllowJitProvisioning = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AllowIdpInitiated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SpEntityId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AcsUrlOverride = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IdpSigningCertProtected = table.Column<string>(type: "text", nullable: true),
                    IdpSigningCertSecondaryProtected = table.Column<string>(type: "text", nullable: true),
                    EnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestResult = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySsoConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySsoConfigurations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanySsoDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanySsoConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VerificationToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySsoDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySsoDomains_CompanySsoConfigurations_CompanySsoConfigurationId",
                        column: x => x.CompanySsoConfigurationId,
                        principalTable: "CompanySsoConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorporateSsoIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protocol = table.Column<short>(type: "smallint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IdPIssuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ProviderEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateSsoIdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateSsoIdentityLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorporateSsoAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateSsoAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySsoConfigurations_CompanyId",
                table: "CompanySsoConfigurations",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySsoConfigurations_IsEnabled",
                table: "CompanySsoConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySsoDomains_CompanyId_Domain",
                table: "CompanySsoDomains",
                columns: new[] { "CompanyId", "Domain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySsoDomains_Domain_Verified",
                table: "CompanySsoDomains",
                column: "Domain",
                unique: true,
                filter: "\"IsVerified\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySsoDomains_CompanySsoConfigurationId",
                table: "CompanySsoDomains",
                column: "CompanySsoConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoIdentityLinks_CompanyId_Protocol_Subject",
                table: "CorporateSsoIdentityLinks",
                columns: new[] { "CompanyId", "Protocol", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoIdentityLinks_CompanyId_UserId",
                table: "CorporateSsoIdentityLinks",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoIdentityLinks_UserId",
                table: "CorporateSsoIdentityLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoAuditEvents_CompanyId",
                table: "CorporateSsoAuditEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoAuditEvents_CreatedAt",
                table: "CorporateSsoAuditEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateSsoAuditEvents_CompanyId_CreatedAt",
                table: "CorporateSsoAuditEvents",
                columns: new[] { "CompanyId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CorporateSsoAuditEvents");
            migrationBuilder.DropTable(name: "CompanySsoDomains");
            migrationBuilder.DropTable(name: "CorporateSsoIdentityLinks");
            migrationBuilder.DropTable(name: "CompanySsoConfigurations");
            migrationBuilder.DropIndex(name: "IX_Companies_Slug", table: "Companies");
            migrationBuilder.DropColumn(name: "Slug", table: "Companies");
        }
    }
}
