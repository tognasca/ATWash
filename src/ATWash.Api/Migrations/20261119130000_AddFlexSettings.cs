using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261119130000_AddFlexSettings")]
public sealed class AddFlexSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Tenants" ADD COLUMN "WashStageNames" text[] NULL;
        ALTER TABLE "Tenants" ADD COLUMN "WashPackages" jsonb NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Tenants" DROP COLUMN "WashPackages";
        ALTER TABLE "Tenants" DROP COLUMN "WashStageNames";
        """);
}
