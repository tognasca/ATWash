# Demo seed (fase 1)

A rotina `DemoSeeder.SeedAsync` carrega os 8 serviços, 6 produtos, 3 funcionários e 3 comandas com clientes e veículos fictícios.

- O tenant de demonstração tem ID fixo `11111111-1111-4111-8111-111111111111`.
- A rotina não altera registros existentes nem é executada em tenants reais.
- Deve ser chamada **somente após aplicar migrations** e **somente quando** `ATWASH_SEED_DEMO=true`.
- Nunca habilite a carga em produção sem provisionamento explícito de ambiente de demonstração.
- Migrations EF Core e testes automatizados ainda estão pendentes. Não usar `EnsureCreated` como substituto.

Para gerar a migration inicial quando o SDK .NET 10 estiver instalado:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add InitialCreate --project src/ATWash.Api --startup-project src/ATWash.Api
dotnet ef database update --project src/ATWash.Api --startup-project src/ATWash.Api
```

Configure `ConnectionStrings__Default` antes de executar os comandos.
