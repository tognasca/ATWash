using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ATWash.Api.Migrations;
[DbContext(typeof(AtWashDbContext))]
[Migration("20261112180000_AddInventoryLedger")]
public sealed class AddInventoryLedger : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.Sql("""
  CREATE UNIQUE INDEX "IX_Products_TenantId_Id" ON "Products" ("TenantId","Id");
  CREATE TABLE "StockMovements" (
    "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
    "ProductId" uuid NOT NULL, "OrderId" uuid NULL,
    "Kind" varchar(24) NOT NULL, "Quantity" numeric(18,3) NOT NULL,
    "BalanceAfter" numeric(18,3) NOT NULL, "Reason" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_StockMovements_Products" FOREIGN KEY ("TenantId","ProductId")
      REFERENCES "Products"("TenantId","Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_StockMovements_Orders" FOREIGN KEY ("TenantId","OrderId")
      REFERENCES "Orders"("TenantId","Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_StockMovements_Quantity" CHECK ("Quantity" <> 0),
    CONSTRAINT "CK_StockMovements_Balance" CHECK ("BalanceAfter" >= 0)
  );
  CREATE INDEX "IX_StockMovements_TenantId_ProductId_CreatedAt"
    ON "StockMovements" ("TenantId","ProductId","CreatedAt");
  CREATE TABLE "OrderProductItems" (
    "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
    "OrderId" uuid NOT NULL, "ProductId" uuid NOT NULL, "ProductName" text NOT NULL,
    "Quantity" numeric(18,3) NOT NULL, "UnitPrice" numeric(18,2) NOT NULL,
    "Total" numeric(18,2) NOT NULL, "IsConsumption" boolean NOT NULL,
    CONSTRAINT "FK_OrderProductItems_Orders" FOREIGN KEY ("TenantId","OrderId")
      REFERENCES "Orders"("TenantId","Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_OrderProductItems_Products" FOREIGN KEY ("TenantId","ProductId")
      REFERENCES "Products"("TenantId","Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_OrderProductItems_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "CK_OrderProductItems_Total" CHECK ("Total" >= 0)
  );
  CREATE INDEX "IX_OrderProductItems_TenantId_OrderId" ON "OrderProductItems" ("TenantId","OrderId");
  INSERT INTO "StockMovements" ("Id","TenantId","ProductId","Kind","Quantity","BalanceAfter","Reason","CreatedAt")
    SELECT gen_random_uuid(),"TenantId","Id",'Opening',"Stock","Stock",'Saldo inicial migrado',now()
    FROM "Products" WHERE "Stock" > 0;
  ALTER TABLE "Products" ADD CONSTRAINT "CK_Products_Stock_Nonnegative" CHECK ("Stock" >= 0);
  """);
 }
 protected override void Down(MigrationBuilder m)
 {
  m.Sql("""
  ALTER TABLE "Products" DROP CONSTRAINT "CK_Products_Stock_Nonnegative";
  DROP TABLE "OrderProductItems";
  DROP TABLE "StockMovements";
  DROP INDEX "IX_Products_TenantId_Id";
  """);
 }
}
