using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261112160000_AddOrderPayments")]
public sealed class AddOrderPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Payments" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "OrderId" uuid NOT NULL,
                "OperationId" uuid NOT NULL,
                "Method" character varying(16) NOT NULL,
                "Amount" numeric(18,2) NOT NULL,
                "Tendered" numeric(18,2) NOT NULL,
                "Reference" text NULL,
                "ConfirmedByUserId" uuid NOT NULL,
                "ConfirmedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "FK_Payments_Tenants_TenantId"
                    FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Payments_Orders_TenantId_OrderId"
                    FOREIGN KEY ("TenantId", "OrderId") REFERENCES "Orders" ("TenantId", "Id") ON DELETE RESTRICT,
                CONSTRAINT "CK_Payments_Amount" CHECK ("Amount" > 0 AND "Tendered" >= "Amount")
            );
            CREATE UNIQUE INDEX "IX_Payments_TenantId_OperationId" ON "Payments" ("TenantId", "OperationId");
            CREATE INDEX "IX_Payments_TenantId_OrderId" ON "Payments" ("TenantId", "OrderId");
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Payments");
    }
}
