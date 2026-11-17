using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ATWash.Api;

public sealed record PublicRegistrationInput(string CompanyName,string Email,string Password,Guid PlanId);

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
