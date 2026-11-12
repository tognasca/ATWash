using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ATWash.Api.Migrations;
[DbContext(typeof(AtWashDbContext))]
[Migration("20261112170000_AddCashAndReconciliation")]
public sealed class AddCashAndReconciliation : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
        CREATE UNIQUE INDEX "IX_Payments_TenantId_Id" ON "Payments" ("TenantId","Id");
        CREATE TABLE "CashSessions" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
            "OpenedBy" uuid NOT NULL, "OpenedAt" timestamptz NOT NULL, "OpeningFloat" numeric(18,2) NOT NULL,
            "ClosedBy" uuid NULL, "ClosedAt" timestamptz NULL, "CountedCash" numeric(18,2) NULL,
            "ExpectedCash" numeric(18,2) NULL, "ClosingNote" text NULL,
            CONSTRAINT "CK_CashSessions_Opening" CHECK ("OpeningFloat" >= 0)
        );
        CREATE UNIQUE INDEX "IX_CashSessions_TenantId_Id" ON "CashSessions" ("TenantId","Id");
        CREATE UNIQUE INDEX "IX_CashSessions_TenantId" ON "CashSessions" ("TenantId") WHERE "ClosedAt" IS NULL;
        CREATE TABLE "CashMovements" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
            "SessionId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Amount" numeric(18,2) NOT NULL,
            "Reason" text NOT NULL, "CreatedBy" uuid NOT NULL, "CreatedAt" timestamptz NOT NULL,
            CONSTRAINT "FK_CashMovements_CashSessions_TenantId_SessionId"
                FOREIGN KEY ("TenantId","SessionId") REFERENCES "CashSessions"("TenantId","Id") ON DELETE RESTRICT,
            CONSTRAINT "CK_CashMovements_Amount" CHECK ("Amount" > 0)
        );
        CREATE INDEX "IX_CashMovements_TenantId_SessionId" ON "CashMovements" ("TenantId","SessionId");
        CREATE TABLE "PixSettings" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
            "PixKey" text NOT NULL, "MerchantName" text NOT NULL, "MerchantCity" text NOT NULL
        );
        CREATE UNIQUE INDEX "IX_PixSettings_TenantId" ON "PixSettings" ("TenantId");
        CREATE TABLE "PaymentRefunds" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
            "PaymentId" uuid NOT NULL, "Amount" numeric(18,2) NOT NULL,
            "Reason" text NOT NULL, "ExternalReference" text NOT NULL,
            "ConfirmedBy" uuid NOT NULL, "ConfirmedAt" timestamptz NOT NULL,
            CONSTRAINT "FK_PaymentRefunds_Payments_TenantId_PaymentId"
                FOREIGN KEY ("TenantId","PaymentId") REFERENCES "Payments"("TenantId","Id") ON DELETE RESTRICT,
            CONSTRAINT "CK_PaymentRefunds_Amount" CHECK ("Amount" > 0)
        );
        CREATE INDEX "IX_PaymentRefunds_TenantId_PaymentId" ON "PaymentRefunds" ("TenantId","PaymentId");
        CREATE TABLE "BankReconciliations" (
            "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Tenants"("Id") ON DELETE RESTRICT,
            "PaymentId" uuid NOT NULL, "BankReference" text NOT NULL,
            "ReconciledAt" timestamptz NOT NULL, "ReconciledBy" uuid NOT NULL,
            CONSTRAINT "FK_BankReconciliations_Payments_TenantId_PaymentId"
                FOREIGN KEY ("TenantId","PaymentId") REFERENCES "Payments"("TenantId","Id") ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX "IX_BankReconciliations_TenantId_PaymentId" ON "BankReconciliations" ("TenantId","PaymentId");
        CREATE UNIQUE INDEX "IX_BankReconciliations_TenantId_BankReference" ON "BankReconciliations" ("TenantId","BankReference");
        """);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
        DROP TABLE "BankReconciliations";
        DROP TABLE "PaymentRefunds";
        DROP TABLE "PixSettings";
        DROP TABLE "CashMovements";
        DROP TABLE "CashSessions";
        DROP INDEX "IX_Payments_TenantId_Id";
        """);
    }
}
