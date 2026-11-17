using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public sealed record PublicRegistrationInput(string CompanyName,string Email,string Password,Guid PlanId);
public sealed record ResumeRegistrationInput(string Email,string Password);

public static class PublicRegistration
{
    public static async Task SeedPlansAsync(AtWashDbContext db,CancellationToken ct=default)
    {
        // Stable names: existing plans are not repriced or overwritten.
        var specs=new (string Name,decimal Price,int Devices)[]{
            ("Essencial",59.90m,1),("Profissional",99.90m,3),("Empresarial",179.90m,10)};
        foreach(var (name,price,devices) in specs)
            if(!await db.SubscriptionPlans.AnyAsync(x=>x.Name==name,ct))
                db.SubscriptionPlans.Add(new SubscriptionPlan{Name=name,MonthlyPrice=price,MaxDevices=devices,IsActive=true});
        await db.SaveChangesAsync(ct);
    }

    public static void MapPublicRegistration(this WebApplication app)
    {
        var group=app.MapGroup("/api/public").RequireRateLimiting("login");
        group.MapGet("/plans",async(AtWashDbContext db,CancellationToken ct)=>
            Results.Ok(await db.SubscriptionPlans.AsNoTracking()
                .Where(x=>x.IsActive&&(x.Name=="Essencial"||x.Name=="Profissional"||x.Name=="Empresarial"))
                .OrderBy(x=>x.MonthlyPrice).Select(x=>new{x.Id,x.Name,x.MonthlyPrice,x.MaxDevices}).ToListAsync(ct)));

        group.MapPost("/resume",async(ResumeRegistrationInput input,AtWashDbContext db,
            IPasswordHasher<AppUser> hasher,IHttpClientFactory clients,IConfiguration config,CancellationToken ct)=>{
            var email=input.Email?.Trim().ToLowerInvariant();
            if(string.IsNullOrWhiteSpace(email)||string.IsNullOrWhiteSpace(input.Password))
                return Results.Unauthorized();
            var matches=await db.Users.Where(x=>x.Email==email&&x.IsActive).Take(2).ToListAsync(ct);
            var owner=matches.Count==1?matches[0]:null;
            if(owner==null||owner.Role!=UserRole.Owner||
                hasher.VerifyHashedPassword(owner,owner.PasswordHash,input.Password)==PasswordVerificationResult.Failed)
                return Results.Unauthorized();
            var sub=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==owner.TenantId,ct);
            if(sub==null)return Results.NotFound();
            if(sub.Status=="active")return Results.Conflict("Sua assinatura já está ativa. Entre no sistema.");
            if(sub.Status!="pending")return Results.Conflict("Sua assinatura exige atendimento do suporte.");
            var binding=await db.MercadoPagoSubscriptions.AsNoTracking()
                .Where(x=>x.TenantId==owner.TenantId)
                .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
            if(binding==null)return Results.NotFound();
            var token=config["MERCADOPAGO_ACCESS_TOKEN"];
            if(string.IsNullOrWhiteSpace(token))return Results.Problem("Assinaturas temporariamente indisponíveis.",statusCode:503);
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.mercadopago.com/preapproval/"+Uri.EscapeDataString(binding.ProviderId));
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            using var response=await clients.CreateClient("mercadopago").SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)return Results.Problem("Não foi possível consultar a assinatura.",statusCode:502);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root=doc.RootElement;
            if(!root.TryGetProperty("id",out var idEl)||idEl.GetString()!=binding.ProviderId||
               !root.TryGetProperty("external_reference",out var refEl)||refEl.GetString()!=owner.TenantId.ToString("D"))
                return Results.Problem("Assinatura não corresponde à empresa.",statusCode:502);
            var status=root.TryGetProperty("status",out var statusEl)?statusEl.GetString():null;
            if(status=="authorized"){
                var success=await MercadoPagoBilling.ReconcileAsync(binding.ProviderId,db,clients,config,ct);
                return success?Results.Ok(new{subscriptionStatus="active",checkoutUrl=(string?)null}):
                    Results.Problem("Não foi possível confirmar a assinatura.",statusCode:502);
            }
            if(status!="pending")return Results.Conflict("Esta tentativa não está mais pendente. Contate o suporte para gerar outra cobrança.");
            var url=root.TryGetProperty("init_point",out var linkEl)?linkEl.GetString():null;
            if(!Uri.TryCreate(url,UriKind.Absolute,out var checkoutUri)||checkoutUri.Scheme!="https"||
               !(checkoutUri.Host.Equals("mercadopago.com.br",StringComparison.OrdinalIgnoreCase)||
                 checkoutUri.Host.EndsWith(".mercadopago.com.br",StringComparison.OrdinalIgnoreCase)))
                return Results.Conflict("Não foi possível recuperar o link da assinatura. Contate o suporte.");
            return Results.Ok(new{subscriptionStatus="pending",checkoutUrl=url});
        });

        group.MapPost("/register",async(PublicRegistrationInput input,AtWashDbContext db,
            IPasswordHasher<AppUser> hasher,IHttpClientFactory clients,IConfiguration config,CancellationToken ct)=>{
            var email=input.Email?.Trim().ToLowerInvariant();
            var company=input.CompanyName?.Trim();
            if(string.IsNullOrWhiteSpace(company)||company.Length>160||
               string.IsNullOrWhiteSpace(email)||email.Length>254||
               !System.Net.Mail.MailAddress.TryCreate(email,out var parsed)||parsed.Address!=email||
               string.IsNullOrWhiteSpace(input.Password)||input.Password.Length is <12 or >128)
                return Results.BadRequest("Informe empresa, e-mail válido e senha com 12 a 128 caracteres.");
            var plan=await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.PlanId&&x.IsActive,ct);
            if(plan==null||plan.Name is not ("Essencial" or "Profissional" or "Empresarial")||plan.MonthlyPrice<=0)
                return Results.BadRequest("Plano indisponível.");
            var token=config["MERCADOPAGO_ACCESS_TOKEN"];
            var back=config["MERCADOPAGO_BACK_URL"];
            if(string.IsNullOrWhiteSpace(token)||!Uri.TryCreate(back,UriKind.Absolute,out var returnUri)||
                returnUri.Scheme!=Uri.UriSchemeHttps)
                return Results.Problem("Assinaturas temporariamente indisponíveis.",statusCode:503);
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({email}, 0))",ct);
            if(await db.Users.AnyAsync(x=>x.Email==email,ct))
                return Results.Conflict("E-mail já cadastrado. Faça login ou entre em contato com o suporte.");
            var tenant=new Tenant{Name=company,IsActive=true,IsDemo=false};
            var owner=new AppUser{TenantId=tenant.Id,Email=email,PasswordHash="",Role=UserRole.Owner};
            owner.PasswordHash=hasher.HashPassword(owner,input.Password);
            // Never enable a new tenant until Mercado Pago confirms the subscription.
            var sub=new TenantSubscription{TenantId=tenant.Id,PlanId=plan.Id,Status="pending"};
            using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.mercadopago.com/preapproval");
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key",tenant.Id.ToString("D"));
            request.Content=JsonContent.Create(new {
                reason="ATWashControl - "+plan.Name,
                external_reference=tenant.Id.ToString("D"),
                payer_email=email,back_url=back,
                auto_recurring=new{frequency=1,frequency_type="months",transaction_amount=plan.MonthlyPrice,currency_id="BRL"},
                status="pending"
            });
            using var response=await clients.CreateClient("mercadopago").SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)
                return Results.Problem("Não foi possível iniciar o pagamento. Tente novamente.",statusCode:502);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root=doc.RootElement;
            var id=root.TryGetProperty("id",out var idEl)?idEl.GetString():null;
            var url=root.TryGetProperty("init_point",out var urlEl)?urlEl.GetString():null;
            if(string.IsNullOrWhiteSpace(id)||id.Length>100||!id.All(ch=>char.IsAsciiLetterOrDigit(ch)||ch=='-'||ch=='_')||
               !Uri.TryCreate(url,UriKind.Absolute,out var checkoutUri)||
               checkoutUri.Scheme!=Uri.UriSchemeHttps||
               !(checkoutUri.Host.Equals("mercadopago.com.br",StringComparison.OrdinalIgnoreCase)||
                 checkoutUri.Host.EndsWith(".mercadopago.com.br",StringComparison.OrdinalIgnoreCase)))
                return Results.Problem("Resposta inválida do provedor de pagamento.",statusCode:502);
            db.Tenants.Add(tenant);db.Users.Add(owner);db.TenantSubscriptions.Add(sub);
            db.MercadoPagoSubscriptions.Add(new MercadoPagoSubscription{
                TenantId=tenant.Id,ProviderId=id!,ProviderStatus="pending",LocalStatus="pending"});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return Results.Ok(new{checkoutUrl=url,subscriptionStatus="pending"});
        });
    }
}
