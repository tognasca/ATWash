using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ATWash.Api.Migrations;
[DbContext(typeof(AtWashDbContext))]
[Migration("20261113210000_AddMercadoPagoSubscriptions")]
public sealed class AddMercadoPagoSubscriptions : Migration {
 protected override void Up(MigrationBuilder m)=>m.Sql("""
 CREATE TABLE "MercadoPagoSubscriptions" (
 "Id" uuid PRIMARY KEY,
 "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
 "ProviderId" text NOT NULL,
 "ProviderStatus" text NOT NULL,
 "LocalStatus" text NOT NULL,
 "CreatedAt" timestamptz NOT NULL,
 "LastSyncedAt" timestamptz NULL,
 "LastNotificationAt" timestamptz NULL);
 CREATE UNIQUE INDEX "IX_MercadoPagoSubscriptions_ProviderId" ON "MercadoPagoSubscriptions" ("ProviderId");
 CREATE INDEX "IX_MercadoPagoSubscriptions_TenantId_CreatedAt" ON "MercadoPagoSubscriptions" ("TenantId","CreatedAt");
 """);
 protected override void Down(MigrationBuilder m)=>m.Sql("""DROP TABLE "MercadoPagoSubscriptions";""");
}