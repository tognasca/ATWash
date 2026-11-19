using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261119120000_LinkOrdersToBranches")]
public sealed class LinkOrdersToBranches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Branches" ADD CONSTRAINT "AK_Branches_TenantId_Id" UNIQUE ("TenantId", "Id");
        ALTER TABLE "Orders" ADD COLUMN "BranchId" uuid NULL;
        ALTER TABLE "Orders" ADD CONSTRAINT "FK_Orders_Branches_TenantId_BranchId"
            FOREIGN KEY ("TenantId", "BranchId") REFERENCES "Branches" ("TenantId", "Id") ON DELETE RESTRICT;
        CREATE INDEX "IX_Orders_TenantId_BranchId_CreatedAt" ON "Orders" ("TenantId", "BranchId", "CreatedAt");
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Orders" DROP CONSTRAINT "FK_Orders_Branches_TenantId_BranchId";
        DROP INDEX "IX_Orders_TenantId_BranchId_CreatedAt";
        ALTER TABLE "Orders" DROP COLUMN "BranchId";
        ALTER TABLE "Branches" DROP CONSTRAINT "AK_Branches_TenantId_Id";
        """);
}
