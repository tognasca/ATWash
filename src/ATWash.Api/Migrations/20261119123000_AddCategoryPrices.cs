using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261119123000_AddCategoryPrices")]
public sealed class AddCategoryPrices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Services" ADD COLUMN "CategoryPrices" jsonb NULL;
        ALTER TABLE "Orders" ADD COLUMN "VehicleCategory" integer NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Orders" DROP COLUMN "VehicleCategory";
        ALTER TABLE "Services" DROP COLUMN "CategoryPrices";
        """);
}
