# ATWash — PWA, tablet Android e limites offline

## Aplicação web e PWA
- O build Vite instala um service worker e armazena **somente os arquivos estáticos** (HTML/JS/CSS/ícones).
- Respostas de `/api` e tokens de autenticação **não são armazenados pelo service worker**.
- Após um login online, o IndexedDB guarda uma cópia por tenant de clientes, veículos, serviços, produtos e comandas para consulta offline no mesmo dispositivo.
- Durante a sessão autenticada, o operador pode cadastrar clientes, veículos e comandas, além de enfileirar mudanças de etapa de lavagem (Aguardando → Lavando → Acabamento → Pronto). Os IDs temporários são remapeados durante a sincronização em ordem de dependência.
- Conflitos de sincronização interrompem o envio e preservam os eventos pendentes; é necessário conferir o erro antes de continuar.
- **Ainda não há** login offline após reiniciar o aplicativo, pagamento offline, serviço extra offline ou entrega offline. Essas ações exigem internet e API. A fila de cadastros pode exigir conferência manual em caso de conflito ou resposta de rede ambígua; não é garantia de sincronização exatamente uma vez.
- O dispositivo deve ter criptografia e bloqueio de tela, pois o cache IndexedDB contém dados de clientes. Use apenas tablets administrados pela empresa.

## Android (Capacitor)
Requer Node.js 24, Android Studio/SDK, JDK 21 e acesso HTTPS à API.

```powershell
cd C:\git\ATWash\src\atwash-web
npm install
$env:VITE_API_BASE_URL="https://SEU-DOMINIO-REAL-DA-API"
npm run build
npx cap add android
npx cap sync android
npx cap open android
```

Configure no backend `ATWASH_CORS_ORIGINS=https://localhost` (acrescente outros domínios web separados por vírgulas). O endereço `VITE_API_BASE_URL` é incorporado ao bundle no build: use o endereço HTTPS real, acessível pelo tablet. **Não utilize localhost ou IP do Docker para a API remota.**

Para gerar APK de teste no Android Studio, use **Build > Build Bundle(s) / APK(s) > Build APK(s)**. Para publicar, gere e assine uma versão release, configure ícones adaptativos Android, políticas de privacidade e teste em tablet físico.

## CI e homologação
O workflow `.github/workflows/build.yml`:
1. Compila API .NET 10.
2. Inicia PostgreSQL 17 limpo, aplica todas as migrations e confere `__EFMigrationsHistory`.
3. Executa testes HTTP integrados com PostgreSQL real (cadastro, comandas, pagamentos, estorno, caixa, conciliação, Pix).
4. Compila PWA frontend.
5. Compila APK Android **debug não configurado** e o publica como artefato temporário.

O APK gerado automaticamente pelo CI não contém URL de API de produção e **não é uma versão pronta para distribuir aos clientes**. É necessário compilar com `VITE_API_BASE_URL` e homologar num tablet real.

## Segurança
- QR Code Pix é gerado a partir do payload EMV/BR Code da API, mas **não confirma o pagamento**.
- Pagamentos, estornos e entrega dependem de validação online.
- Não publicar dados reais de clientes em artefatos ou logs do CI.
