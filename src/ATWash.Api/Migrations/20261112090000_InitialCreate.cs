using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261112090000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Tenants" (
                "Id" uuid PRIMARY KEY, "Name" text NOT NULL,
                "IsDemo" boolean NOT NULL, "IsActive" boolean NOT NULL
            );
            CREATE TABLE "Branches" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Name" text NOT NULL
            );
            CREATE TABLE "Customers" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Name" text NOT NULL, "Phone" text NULL,
                CONSTRAINT "AK_Customers_TenantId_Id" UNIQUE ("TenantId","Id")
            );
            CREATE TABLE "Services" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Name" text NOT NULL, "Price" numeric(18,2) NOT NULL,
                CONSTRAINT "AK_Services_TenantId_Id" UNIQUE ("TenantId","Id")
            );
            CREATE TABLE "Products" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Name" text NOT NULL, "Price" numeric(18,2) NOT NULL, "Stock" numeric(18,3) NOT NULL
            );
            CREATE TABLE "Employees" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Name" text NOT NULL, "Role" text NOT NULL
            );
            CREATE TABLE "Vehicles" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "CustomerId" uuid NOT NULL, "Plate" text NOT NULL, "Model" text NOT NULL,
                "Brand" text NULL, "Color" text NULL,
                CONSTRAINT "AK_Vehicles_TenantId_Id" UNIQUE ("TenantId","Id"),
                CONSTRAINT "FK_Vehicles_Customers_TenantId_CustomerId"
                    FOREIGN KEY ("TenantId","CustomerId") REFERENCES "Customers"("TenantId","Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_Vehicles_TenantId_Plate" ON "Vehicles"("TenantId","Plate");
            CREATE TABLE "Orders" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
                "Number" text NOT NULL, "VehicleId" uuid NOT NULL, "ServiceId" uuid NOT NULL,
                "Status" integer NOT NULL, "PaymentStatus" integer NOT NULL,
                "Total" numeric(18,2) NOT NULL, "CreatedAt" timestamptz NOT NULL,
                CONSTRAINT "FK_Orders_Vehicles_TenantId_VehicleId"
                    FOREIGN KEY ("TenantId","VehicleId") REFERENCES "Vehicles"("TenantId","Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Orders_Services_TenantId_ServiceId"
                    FOREIGN KEY ("TenantId","ServiceId") REFERENCES "Services"("TenantId","Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_Orders_TenantId_Number" ON "Orders"("TenantId","Number");
            CREATE INDEX "IX_Branches_TenantId" ON "Branches"("TenantId");
            CREATE INDEX "IX_Customers_TenantId" ON "Customers"("TenantId");
            CREATE INDEX "IX_Services_TenantId" ON "Services"("TenantId");
            CREATE INDEX "IX_Products_TenantId" ON "Products"("TenantId");
            CREATE INDEX "IX_Employees_TenantId" ON "Employees"("TenantId");
            CREATE INDEX "IX_Vehicles_TenantId_CustomerId" ON "Vehicles"("TenantId","CustomerId");
            CREATE INDEX "IX_Orders_TenantId_VehicleId" ON "Orders"("TenantId","VehicleId");
            CREATE INDEX "IX_Orders_TenantId_ServiceId" ON "Orders"("TenantId","ServiceId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE "Orders";
            DROP TABLE "Vehicles";
            DROP TABLE "Employees";
            DROP TABLE "Products";
            DROP TABLE "Services";
            DROP TABLE "Customers";
            DROP TABLE "Branches";
            DROP TABLE "Tenants";
            """);
    }
}
