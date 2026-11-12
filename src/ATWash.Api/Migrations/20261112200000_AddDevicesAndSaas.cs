using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ATWash.Api.Migrations;
[DbContext(typeof(AtWashDbContext))]
[Migration("20261112200000_AddDevicesAndSaas")]
public sealed class AddDevicesAndSaas : Migration {
 protected override void Up(MigrationBuilder m)=>m.Sql("""
 CREATE TABLE "RegisteredDevices" (
 "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
 "UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
 "Name" text NOT NULL,"CreatedAt" timestamptz NOT NULL,"RevokedAt" timestamptz NULL);
 CREATE INDEX "IX_RegisteredDevices_TenantId_UserId" ON "RegisteredDevices" ("TenantId","UserId");
 CREATE TABLE "SubscriptionPlans" (
 "Id" uuid PRIMARY KEY,"Name" text NOT NULL,"MonthlyPrice" numeric(18,2) NOT NULL,
 "MaxDevices" integer NOT NULL,"IsActive" boolean NOT NULL);
 CREATE TABLE "TenantSubscriptions" (
 "Id" uuid PRIMARY KEY,"TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
 "PlanId" uuid NOT NULL REFERENCES "SubscriptionPlans"("Id") ON DELETE RESTRICT,
 "Status" text NOT NULL,"StartsAt" timestamptz NOT NULL,"ExpiresAt" timestamptz NULL);
 CREATE UNIQUE INDEX "IX_TenantSubscriptions_TenantId" ON "TenantSubscriptions" ("TenantId");
 """);
 protected override void Down(MigrationBuilder m)=>m.Sql("""
 DROP TABLE "TenantSubscriptions";DROP TABLE "SubscriptionPlans";DROP TABLE "RegisteredDevices";
 """);
}
