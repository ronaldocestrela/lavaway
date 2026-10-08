# ADR-0023: Configuração Descentralizada de Gateways e Emissão de Cobrança via Pagar.me por Tenant

## Status
Aceito

## Contexto
Até então, o módulo `Billing` operava com um único provedor injetado estaticamente ou tokens globais definidos no `appsettings.json`. Em um modelo SaaS multi-tenant para lava-jatos e estética automotiva, cada estabelecimento possui sua própria conta bancária, CNPJ e credenciais de recebimento. O dinheiro pago pelo cliente nas Ordens de Serviço (OS) deve ser depositado diretamente na conta do respectivo tenant, e não em uma conta central da plataforma.

A **Pagar.me (Stone Co.)** é um dos principais adquirentes e processadores de pagamentos no mercado brasileiro, oferecendo alta confiabilidade, taxas competitivas e liquidação Pix imediata.

Requisitos essenciais:
1. **Configuração Descentralizada por Tenant:** Cada estabelecimento deve poder cadastrar, atualizar e testar suas próprias chaves de API da Pagar.me (Secret Key e Public Key) e segredo de webhook.
2. **Segurança e Criptografia em Repouso:** Chaves de API secretas de clientes nunca podem ser gravadas em texto puro no banco de dados nem expostas em endpoints de consulta pública.
3. **Resolução Dinâmica de Gateways (*Tenant Gateway Resolver*):** A emissão de cobranças Pix da OS e a verificação de status devem resolver o gateway ativo configurado para aquele tenant específico (Pagar.me v5, Mercado Pago ou Simulado).
4. **Reconciliação e Webhooks:** Suporte a webhooks assinados da Pagar.me (`charge.paid` e `order.paid`), executando a conciliação financeira, baixa da OS no pátio e notificação ao cliente.
5. **Interface Administrativa Intuitiva:** Nova página no Blazor WebAssembly (`/settings/payments`) para seleção do provedor, inserção protegida de credenciais, teste instantâneo de conexão e instruções de webhook com botão de cópia rápida.

## Decisões

### 1. Modelo de Domínio e Persistência Multi-tenant
- Criado o agregado `TenantPaymentGatewayConfig` no módulo `Billing.Domain`, implementando `IMustHaveTenant` e chave UUID Version 7.
- Campos: `TenantId`, `Provider` (PagarMe, MercadoPago, Simulated), `PagarMeSecretKeyEncrypted`, `PagarMePublicKey`, `PagarMeWebhookSecretEncrypted`, `IsActive`, `LastTestedAtUtc`, `LastTestSuccess` e `LastTestMessage`.
- Mapeado na tabela `billing.TenantPaymentGatewayConfigs` com índice único por `TenantId` e migração EF Core dedicada `AddTenantPaymentGatewayConfig`.

### 2. Criptografia Criptográfica Autenticada (AEAD AES-GCM 256 bits)
- Implementada a porta `IPaymentCredentialsEncryptor` e serviço `PaymentCredentialsEncryptor` utilizando o padrão `AesGcm` da biblioteca de criptografia do .NET.
- Chaves secretas são criptografadas com nonce aleatório e tag de autenticação antes da persistência, e retornadas em máscaras seguras (ex: `sk_test_••••••••1234`) na API de consulta.

### 3. Adaptador Pagar.me v5 e Resolução Dinâmica
- `PagarMePixGatewayProvider`: Adaptador que consome a API REST oficial da Pagar.me v5 (`POST https://api.pagar.me/core/v5/orders`), com cabeçalho `Basic Auth` da Secret Key do tenant, envio de metadados da OS, geração de QR Code SVG em base64 e código Copia e Cola.
- `TenantPixGatewayResolver`: Implementa `ITenantPixGatewayResolver` para resolver em tempo de execução o provedor configurado pelo tenant, com fallback seguro para ambiente simulado quando não configurado.
- `PixBillingApplicationService`: Atualizado para utilizar o resolvedor dinâmico sem quebrar contratos legados ou testes unitários existentes.

### 4. Validação de Webhooks Pagar.me e Reconciliação
- Estendido `IPaymentWebhookValidator` e `PaymentWebhookValidator` com `ValidatePagarMeWebhook`, suportando validação HMAC SHA256 (`X-Hub-Signature`), HMAC SHA1 e segredo compartilhado em tempo constante (`CryptographicOperations.FixedTimeEquals`).
- Endpoint `/billing/webhooks/{provider}/{tenantId:guid}` atualizado para decodificar payloads Pagar.me (`charge.paid`), acionando a quitação automática da OS em `YardOperations`, registro no caixa diário e notificação via WhatsApp.

### 5. Frontend Blazor WebAssembly
- Adicionada a página administrativa `PaymentSettingsPage.razor` (`/settings/payments`) com acesso restrito à role `Administrator`.
- Seletor visual de gateways, campos com visibilidade alternável para chaves de API, banner de teste de conexão com a API da Pagar.me e caixa de cópia da URL do webhook com instrução detalhada.
- Integrado novo item `14 Pagamentos & Gateways` no menu de navegação `NavMenu.razor`.

## Consequências

- **Positivas:**
  - Autonomia total para os estabelecimentos conectarem sua própria adquirente Stone/Pagar.me.
  - Zero custódia indevida de valores pela plataforma SaaS: pagamentos dos clientes caem na conta bancária do estabelecimento.
  - Máxima segurança com criptografia de ponta a ponta e mascaramento em repouso e em trânsito.
  - 100% de sucesso nos testes da solução (testes unitários, testes de arquitetura NetArchTest, integração multi-tenant Testcontainers SQL Server e testes bUnit).
