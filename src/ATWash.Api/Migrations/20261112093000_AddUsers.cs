using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261112093000_AddUsers")]
public sealed class AddUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Users" (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Email" text NOT NULL,
                "PasswordHash" text NOT NULL,
                "Role" varchar(32) NOT NULL,
                "IsActive" boolean NOT NULL
            );
            CREATE UNIQUE INDEX "IX_Users_TenantId_Email" ON "Users"("TenantId","Email");
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""DROP TABLE "Users";""");
}
