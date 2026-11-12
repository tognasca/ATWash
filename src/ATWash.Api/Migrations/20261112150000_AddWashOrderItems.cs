using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ATWash.Api.Migrations;

[DbContext(typeof(AtWashDbContext))]
[Migration("20261112150000_AddWashOrderItems")]
public sealed class AddWashOrderItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_TenantId_Id"
            ON "Orders" ("TenantId", "Id");

            CREATE TABLE IF NOT EXISTS "OrderItems" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "OrderId" uuid NOT NULL,
                "ServiceId" uuid NOT NULL,
                "ServiceName" character varying(160) NOT NULL,
                "UnitPrice" numeric(18,2) NOT NULL,
                "Quantity" integer NOT NULL DEFAULT 1,
                "Total" numeric(18,2) NOT NULL,
                CONSTRAINT "FK_OrderItems_Tenants_TenantId"
                    FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OrderItems_Orders_TenantId_OrderId"
                    FOREIGN KEY ("TenantId", "OrderId") REFERENCES "Orders" ("TenantId", "Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_OrderItems_Services_TenantId_ServiceId"
                    FOREIGN KEY ("TenantId", "ServiceId") REFERENCES "Services" ("TenantId", "Id") ON DELETE RESTRICT,
                CONSTRAINT "CK_OrderItems_Quantity" CHECK ("Quantity" > 0),
                CONSTRAINT "CK_OrderItems_Price" CHECK ("UnitPrice" >= 0 AND "Total" >= 0)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrderItems_TenantId_OrderId_ServiceId"
            ON "OrderItems" ("TenantId", "OrderId", "ServiceId");

            INSERT INTO "OrderItems" ("Id", "TenantId", "OrderId", "ServiceId", "ServiceName", "UnitPrice", "Quantity", "Total")
            SELECT gen_random_uuid(), o."TenantId", o."Id", s."Id", s."Name", o."Total", 1, o."Total"
            FROM "Orders" o
            JOIN "Services" s ON s."Id" = o."ServiceId" AND s."TenantId" = o."TenantId"
            WHERE NOT EXISTS (SELECT 1 FROM "OrderItems" i WHERE i."OrderId" = o."Id" AND i."TenantId" = o."TenantId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "OrderItems"; DROP INDEX IF EXISTS "IX_Orders_TenantId_Id";""");
    }
}
