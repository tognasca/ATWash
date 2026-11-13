using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public static class MercadoPagoBilling
{
    private static bool Owner(ClaimsPrincipal p,out Guid tenantId) {
        tenantId=Guid.Empty;
        return p.IsInRole("Owner") && Guid.TryParse(p.FindFirst("tenant_id")?.Value,out tenantId) && tenantId!=Guid.Empty;
    }

    private static string MapStatus(string? status) => status switch {
        "authorized"=>"active", "paused"=>"suspended",
        "cancelled" or "canceled"=>"cancelled", "pending"=>"pending", _=>"past_due"
    };

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length<=100 &&
        id.All(ch=>char.IsAsciiLetterOrDigit(ch)||ch=='-'||ch=='_');

    private static async Task<JsonDocument?> GetProviderAsync(string id,IHttpClientFactory clients,
        IConfiguration config,CancellationToken ct)
    {
        var token=config["MERCADOPAGO_ACCESS_TOKEN"];
        if(string.IsNullOrWhiteSpace(token)||!ValidId(id))return null;
        using var req=new HttpRequestMessage(HttpMethod.Get,"https://api.mercadopago.com/preapproval/"+id);
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await clients.CreateClient("mercadopago").SendAsync(req,ct);
        if(!response.IsSuccessStatusCode)return null;
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    public static async Task<bool> ReconcileAsync(string providerId,AtWashDbContext db,
        IHttpClientFactory clients,IConfiguration config,CancellationToken ct)
    {
        var binding=await db.MercadoPagoSubscriptions.SingleOrDefaultAsync(x=>x.ProviderId==providerId,ct);
        if(binding==null)return false;
        using var doc=await GetProviderAsync(providerId,clients,config,ct);
        if(doc==null)return false;
        var root=doc.RootElement;
        if(!root.TryGetProperty("id",out var idEl)||idEl.GetString()!=providerId||
            !root.TryGetProperty("external_reference",out var reference)||
            reference.GetString()!=binding.TenantId.ToString("D"))return false;
        var providerStatus=root.TryGetProperty("status",out var status)?status.GetString():null;
        var state=MapStatus(providerStatus);
        // Only the latest created subscription may control the tenant's access.
        var latest=await db.MercadoPagoSubscriptions.AsNoTracking()
            .Where(x=>x.TenantId==binding.TenantId)
            .OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id)
            .Select(x=>x.Id).FirstAsync(ct);
        binding.ProviderStatus=providerStatus??"unknown";
        binding.LocalStatus=state;
        binding.LastSyncedAt=DateTimeOffset.UtcNow;
        if(latest==binding.Id){
            var sub=await db.TenantSubscriptions.SingleOrDefaultAsync(x=>x.TenantId==binding.TenantId,ct);
            if(sub!=null)sub.Status=state;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool VerifyWebhook(HttpRequest request,IConfiguration config,string id)
    {
        var secret=config["MERCADOPAGO_WEBHOOK_SECRET"];
        if(string.IsNullOrWhiteSpace(secret)||!ValidId(id))return false;
        var signature=request.Headers["x-signature"].ToString();
        var requestId=request.Headers["x-request-id"].ToString();
        if(string.IsNullOrWhiteSpace(requestId)||requestId.Length>200)return false;
        string? ts=null,hash=null;
        foreach(var part in signature.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)){
            var pair=part.Split('=',2);
            if(pair.Length!=2)continue;
            if(pair[0]=="ts")ts=pair[1];
            if(pair[0]=="v1")hash=pair[1];
        }
        if(!long.TryParse(ts,out var milliseconds)||string.IsNullOrWhiteSpace(hash)||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-milliseconds)>300000)return false;
        // Mercado Pago's signed manifest uses the data.id query value in lowercase.
        var manifest=$"id:{id.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var digest=HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(manifest));
        try{
            var supplied=Convert.FromHexString(hash);
            return supplied.Length==digest.Length&&CryptographicOperations.FixedTimeEquals(supplied,digest);
        }catch(FormatException){return false;}
    }

    public static void MapMercadoPagoBilling(this WebApplication app)
    {
        var api=app.MapGroup("/api/saas/mercadopago").RequireAuthorization();
        api.MapGet("/subscriptions",async(ClaimsPrincipal principal,AtWashDbContext db,CancellationToken ct)=>{
            if(!Owner(principal,out var tenantId))return Results.Forbid();
            var items=await db.MercadoPagoSubscriptions.AsNoTracking()
                .Where(x=>x.TenantId==tenantId).OrderByDescending(x=>x.CreatedAt)
                .Select(x=>new{x.ProviderId,x.ProviderStatus,x.LocalStatus,x.CreatedAt,x.LastSyncedAt})
                .ToListAsync(ct);
            return Results.Ok(items);
        });
        api.MapPost("/checkout",async (ClaimsPrincipal principal,AtWashDbContext db,
            IHttpClientFactory clients,IConfiguration config,CancellationToken ct) =>
        {
            if(!Owner(principal,out var tenantId))return Results.Forbid();
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
            // A previously pending checkout can be reused; avoid issuing duplicate subscriptions.
            var pending=await db.MercadoPagoSubscriptions.AsNoTracking()
                .Where(x=>x.TenantId==tenantId&&x.ProviderStatus=="pending")
                .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
            if(pending!=null&&pending.CreatedAt>DateTimeOffset.UtcNow.AddMinutes(-15))
                return Results.Conflict("Já existe uma assinatura pendente. Aguarde ou tente novamente após 15 minutos.");
            using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.mercadopago.com/preapproval");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key",Guid.NewGuid().ToString());
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
            if(!ValidId(id)||!Uri.TryCreate(link,UriKind.Absolute,out var checkoutUri)||
                checkoutUri.Scheme!="https"||!checkoutUri.Host.EndsWith(".mercadopago.com.br",StringComparison.OrdinalIgnoreCase)
                && !string.Equals(checkoutUri.Host,"mercadopago.com.br",StringComparison.OrdinalIgnoreCase))
                return Results.Problem("Resposta de assinatura inválida.",statusCode:502);
            db.MercadoPagoSubscriptions.Add(new MercadoPagoSubscription{
                TenantId=tenantId,ProviderId=id!,ProviderStatus="pending",LocalStatus="pending"
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new{subscriptionId=id,checkoutUrl=link,status="pending"});
        });
        api.MapPost("/sync/{subscriptionId}",async (string subscriptionId,ClaimsPrincipal principal,
            AtWashDbContext db,IHttpClientFactory clients,IConfiguration config,CancellationToken ct) =>
        {
            if(!Owner(principal,out var tenantId))return Results.Forbid();
            if(!ValidId(subscriptionId))return Results.BadRequest("Identificador inválido.");
            if(string.IsNullOrWhiteSpace(config["MERCADOPAGO_ACCESS_TOKEN"]))
                return Results.Problem("Mercado Pago não configurado.",statusCode:503);
            if(!await db.MercadoPagoSubscriptions.AnyAsync(x=>x.ProviderId==subscriptionId&&x.TenantId==tenantId,ct))
                return Results.NotFound();
            var success=await ReconcileAsync(subscriptionId,db,clients,config,ct);
            return success?Results.Ok(new{subscriptionId,reconciled=true}):
                Results.Problem("Não foi possível confirmar assinatura.",statusCode:502);
        });
        // Public notification: verify signature AND fetch provider state; never trust payload status.
        app.MapPost("/api/webhooks/mercadopago",async(HttpRequest request,AtWashDbContext db,
            IHttpClientFactory clients,IConfiguration config,CancellationToken ct)=>{
            var id=request.Query["data.id"].ToString();
            var type=request.Query["type"].ToString();
            if(type!="subscription_preapproval"&&type!="preapproval")return Results.Ok();
            if(!VerifyWebhook(request,config,id))return Results.Unauthorized();
            var binding=await db.MercadoPagoSubscriptions.SingleOrDefaultAsync(x=>x.ProviderId==id,ct);
            if(binding==null)return Results.Ok();
            binding.LastNotificationAt=DateTimeOffset.UtcNow;
            var success=await ReconcileAsync(id,db,clients,config,ct);
            return success?Results.Ok():Results.StatusCode(503);
        });
    }
}

public sealed class MercadoPagoReconciliationWorker(IServiceScopeFactory scopes,IConfiguration config,
    ILogger<MercadoPagoReconciliationWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(15));
        while(await timer.WaitForNextTickAsync(stoppingToken)){
            if(string.IsNullOrWhiteSpace(config["MERCADOPAGO_ACCESS_TOKEN"]))continue;
            try{
                using var scope=scopes.CreateScope();
                var db=scope.ServiceProvider.GetRequiredService<AtWashDbContext>();
                var clients=scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
                var ids=await db.MercadoPagoSubscriptions.AsNoTracking()
                    .Where(x=>x.LastSyncedAt==null||x.LastSyncedAt<DateTimeOffset.UtcNow.AddMinutes(-15))
                    .OrderBy(x=>x.LastSyncedAt).Take(100).Select(x=>x.ProviderId).ToListAsync(stoppingToken);
                foreach(var id in ids){
                    try{await MercadoPagoBilling.ReconcileAsync(id,db,clients,config,stoppingToken);}
                    catch(Exception e){logger.LogWarning(e,"Mercado Pago reconciliation failed for subscription");}
                }
            }catch(Exception e){logger.LogError(e,"Mercado Pago reconciliation cycle failed");}
        }
    }
}
