using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ATWash.Api.Migrations;
[DbContext(typeof(AtWashDbContext))]
[Migration("20261112190000_OperationsAndRecipes")]
public sealed class OperationsAndRecipes : Migration {
 protected override void Up(MigrationBuilder m) => m.Sql("""
 ALTER TABLE "Employees" ADD COLUMN "Phone" text NULL;
 ALTER TABLE "Employees" ADD COLUMN "IsActive" boolean NOT NULL DEFAULT true;
 ALTER TABLE "Employees" ADD COLUMN "CommissionPercent" numeric(5,2) NOT NULL DEFAULT 0;
 ALTER TABLE "Employees" ADD COLUMN "UserId" uuid NULL;
 ALTER TABLE "Employees" ADD CONSTRAINT "AK_Employees_TenantId_Id" UNIQUE ("TenantId","Id");
 ALTER TABLE "Employees" ADD CONSTRAINT "FK_Employees_Users" FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
 CREATE UNIQUE INDEX "IX_Employees_TenantId_UserId" ON "Employees" ("TenantId","UserId") WHERE "UserId" IS NOT NULL;
 CREATE TABLE "Suppliers" (
  "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
  "Name" text NOT NULL,"Phone" text NULL,"TaxId" text NULL,"IsActive" boolean NOT NULL DEFAULT true,
  CONSTRAINT "AK_Suppliers_TenantId_Id" UNIQUE ("TenantId","Id"));
 ALTER TABLE "Products" ADD COLUMN "MinimumStock" numeric(18,3) NOT NULL DEFAULT 0;
 ALTER TABLE "Products" ADD COLUMN "AverageCost" numeric(18,2) NOT NULL DEFAULT 0;
 ALTER TABLE "Products" ADD COLUMN "Unit" text NOT NULL DEFAULT 'un';
 ALTER TABLE "Products" ADD COLUMN "IsSupply" boolean NOT NULL DEFAULT false;
 ALTER TABLE "Products" ADD COLUMN "SupplierId" uuid NULL;
 ALTER TABLE "Products" ADD CONSTRAINT "FK_Products_Suppliers" FOREIGN KEY ("TenantId","SupplierId") REFERENCES "Suppliers"("TenantId","Id") ON DELETE RESTRICT;
 ALTER TABLE "Orders" ADD COLUMN "EmployeeId" uuid NULL;
 ALTER TABLE "Orders" ADD CONSTRAINT "FK_Orders_Employees" FOREIGN KEY ("TenantId","EmployeeId") REFERENCES "Employees"("TenantId","Id") ON DELETE RESTRICT;
 CREATE TABLE "ServiceIngredients" (
  "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
  "ServiceId" uuid NOT NULL,"ProductId" uuid NOT NULL,"Quantity" numeric(18,3) NOT NULL,
  CONSTRAINT "FK_ServiceIngredients_Services" FOREIGN KEY ("TenantId","ServiceId") REFERENCES "Services"("TenantId","Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_ServiceIngredients_Products" FOREIGN KEY ("TenantId","ProductId") REFERENCES "Products"("TenantId","Id") ON DELETE RESTRICT,
  CONSTRAINT "CK_ServiceIngredients_Quantity" CHECK ("Quantity" > 0),
  CONSTRAINT "AK_ServiceIngredients_Unique" UNIQUE ("TenantId","ServiceId","ProductId"));
 CREATE TABLE "ServiceExecutions" (
  "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
  "OrderId" uuid NOT NULL,"ServiceId" uuid NOT NULL,"EmployeeId" uuid NULL,
  "ExecutedAt" timestamptz NOT NULL,
  CONSTRAINT "FK_ServiceExecutions_Orders" FOREIGN KEY ("TenantId","OrderId") REFERENCES "Orders"("TenantId","Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_ServiceExecutions_Services" FOREIGN KEY ("TenantId","ServiceId") REFERENCES "Services"("TenantId","Id") ON DELETE RESTRICT,
  CONSTRAINT "FK_ServiceExecutions_Employees" FOREIGN KEY ("TenantId","EmployeeId") REFERENCES "Employees"("TenantId","Id") ON DELETE RESTRICT,
  CONSTRAINT "AK_ServiceExecutions_Unique" UNIQUE ("TenantId","OrderId","ServiceId"));
 """);
 protected override void Down(MigrationBuilder m) => m.Sql("""
 DROP TABLE "ServiceExecutions"; DROP TABLE "ServiceIngredients";
 ALTER TABLE "Orders" DROP CONSTRAINT "FK_Orders_Employees";
 ALTER TABLE "Orders" DROP COLUMN "EmployeeId";
 ALTER TABLE "Products" DROP CONSTRAINT "FK_Products_Suppliers";
 ALTER TABLE "Products" DROP COLUMN "SupplierId";
 ALTER TABLE "Products" DROP COLUMN "IsSupply";
 ALTER TABLE "Products" DROP COLUMN "Unit";
 ALTER TABLE "Products" DROP COLUMN "AverageCost";
 ALTER TABLE "Products" DROP COLUMN "MinimumStock";
 DROP TABLE "Suppliers";
 DROP INDEX "IX_Employees_TenantId_UserId";
 ALTER TABLE "Employees" DROP CONSTRAINT "FK_Employees_Users";
 ALTER TABLE "Employees" DROP CONSTRAINT "AK_Employees_TenantId_Id";
 ALTER TABLE "Employees" DROP COLUMN "UserId";
 ALTER TABLE "Employees" DROP COLUMN "CommissionPercent";
 ALTER TABLE "Employees" DROP COLUMN "IsActive";
 ALTER TABLE "Employees" DROP COLUMN "Phone";
 """);
}
