using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

// Mercado Pago subscriptions: do not treat checkout redirects or unverified notifications as payment confirmation.
public static class MercadoPagoBilling
{
    public static void MapMercadoPagoBilling(this WebApplication app)
    {
        var api=app.MapGroup("/api/saas/mercadopago").RequireAuthorization();
        api.MapPost("/checkout",async (ClaimsPrincipal principal, AtWashDbContext db,
            IHttpClientFactory clients,IConfiguration config,CancellationToken ct) =>
        {
            if(!principal.IsInRole("Owner") || !Guid.TryParse(principal.FindFirst("tenant_id")?.Value,out var tenantId))
                return Results.Forbid();
            var token=config["MERCADOPAGO_ACCESS_TOKEN"];
            var back=config["MERCADOPAGO_BACK_URL"];
            if(string.IsNullOrWhiteSpace(token)||!Uri.TryCreate(back,UriKind.Absolute,out var returnUri)||
                returnUri.Scheme!=Uri.UriSchemeHttps)
                return Results.Problem("Mercado Pago não configurado.",statusCode:503);
            var subscription=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenantId,ct);
            if(subscription==null)return Results.BadRequest("A empresa ainda não possui um plano.");
            var plan=await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==subscription.PlanId,ct);
            if(plan==null||!plan.IsActive||plan.MonthlyPrice<=0)return Results.BadRequest("Plano inválido para cobrança.");
            var email=principal.FindFirst(ClaimTypes.Email)?.Value??principal.FindFirst("email")?.Value;
            if(string.IsNullOrWhiteSpace(email))
                email=await db.Users.Where(x=>x.TenantId==tenantId&&x.Role==UserRole.Owner).Select(x=>x.Email).FirstOrDefaultAsync(ct);
            if(string.IsNullOrWhiteSpace(email))return Results.BadRequest("E-mail do proprietário não encontrado.");
            using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.mercadopago.com/preapproval");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key",tenantId.ToString("N")+"-"+plan.Id.ToString("N"));
            request.Content=JsonContent.Create(new {
                reason="ATWashControl - "+plan.Name,
                external_reference=tenantId.ToString("D"),
                payer_email=email,
                back_url=back,
                auto_recurring=new {frequency=1,frequency_type="months",transaction_amount=plan.MonthlyPrice,currency_id="BRL"},
                status="pending"
            });
            using var response=await clients.CreateClient("mercadopago").SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)return Results.Problem("Não foi possível iniciar assinatura no Mercado Pago.",statusCode:502);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root=doc.RootElement;
            var id=root.TryGetProperty("id",out var idEl)?idEl.GetString():null;
            var link=root.TryGetProperty("init_point",out var linkEl)?linkEl.GetString():null;
            if(string.IsNullOrWhiteSpace(id)||!Uri.TryCreate(link,UriKind.Absolute,out var checkoutUri)||
                checkoutUri.Scheme!="https"||!checkoutUri.Host.EndsWith("mercadopago.com.br",StringComparison.OrdinalIgnoreCase))
                return Results.Problem("Resposta de assinatura inválida.",statusCode:502);
            return Results.Ok(new{subscriptionId=id,checkoutUrl=link,status="pending"});
        });
    }
}
