using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public static class FlexSettingsEndpoints
{
    private static readonly string[] DefaultStages = ["Aguardando", "Lavando", "Acabamento", "Pronto"];

    public static void MapFlexSettings(this WebApplication app)
    {
        var api = app.MapGroup("/api/flex").RequireAuthorization();
        api.MapGet("/settings", async (ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            var tenant = await FindTenant(principal, db, ct);
            if (tenant is null) return Results.Forbid();
            return Results.Ok(new FlexSettings(tenant.WashStageNames ?? DefaultStages, tenant.WashPackages ?? []));
        });

        api.MapPut("/settings", async (FlexSettings input, ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        {
            if (!principal.IsInRole(nameof(UserRole.Owner)) && !principal.IsInRole(nameof(UserRole.Manager)))
                return Results.Forbid();
            var tenant = await FindTenant(principal, db, ct);
            if (tenant is null) return Results.Forbid();

            if (input.StageNames is not { Length: 4 } ||
                input.StageNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 40))
                return Results.BadRequest("Informe os nomes das quatro etapas da lavagem, com até 40 caracteres cada.");

            if (input.Packages is null || input.Packages.Length > 50 ||
                input.Packages.Any(package => package is null || string.IsNullOrWhiteSpace(package.Name) ||
                    package.Name.Trim().Length > 120 || package.ServiceIds is not { Length: >= 2 and <= 30 } ||
                    package.ServiceIds.Distinct().Count() != package.ServiceIds.Length))
                return Results.BadRequest("Cada pacote deve ter um nome e de 2 a 30 serviços diferentes.");

            var packages = input.Packages.Select(package => package with { Name = package.Name.Trim() }).ToArray();
            if (packages.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != packages.Length)
                return Results.BadRequest("Use nomes diferentes para os pacotes.");

            var ids = packages.SelectMany(package => package.ServiceIds).Distinct().ToArray();
            if (await db.Services.CountAsync(x => x.TenantId == tenant.Id && ids.Contains(x.Id), ct) != ids.Length)
                return Results.BadRequest("O pacote contém serviços indisponíveis para esta empresa.");

            tenant.WashStageNames = input.StageNames.Select(name => name.Trim()).ToArray();
            tenant.WashPackages = packages;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new FlexSettings(tenant.WashStageNames, tenant.WashPackages));
        });
    }

    private static Task<Tenant?> FindTenant(ClaimsPrincipal principal, AtWashDbContext db, CancellationToken ct) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenant) && tenant != Guid.Empty
            ? db.Tenants.SingleOrDefaultAsync(x => x.Id == tenant, ct)
            : Task.FromResult<Tenant?>(null);
}

public sealed record FlexSettings(string[] StageNames, WashPackage[] Packages);
