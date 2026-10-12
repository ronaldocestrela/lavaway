# ADR-0024: Provedor Stripe para Faturamento Recorrente do SaaS via Cartão de Crédito

## Status
Aceito

## Contexto
A plataforma Lavaway opera no modelo B2B SaaS multi-tenant. Conforme estabelecido na ADR-0020, o faturamento das assinaturas dos estabelecimentos (tenants) foi desacoplado por meio da porta hexagonal `ISaasBillingGatewayProvider`. Até o momento, o sistema utilizava a implementação `SimulatedSaasBillingGatewayProvider` para validação interna e testes.

Para o lançamento comercial em escala e viabilização de receitas recorrentes automatizadas, os estabelecimentos necessitam contratar e renovar seus planos (`Basic`, `Pro` e `Enterprise`) através de **Cartão de Crédito** com cobrança recorrente mensal, antifraude robusto, suporte a autenticação 3D Secure / SCA e gestão simplificada de cartões.

A **Stripe** é a plataforma líder global em infraestrutura financeira para faturamento recorrente (Stripe Billing, Checkout e Customer Portal), oferecendo máxima confiabilidade, conformidade PCI-DSS de nível 1 e processamento nativo em moeda brasileira (BRL).

## Decisão de Arquitetura

### 1. Hexagonal Architecture & Isolamento Modular Rigoroso
- O núcleo de domínio (`CarWashSaaS.Billing.Domain`) permanece **100% puro e agnóstico de fornecedor**, sem referências ao SDK da Stripe ou bibliotecas HTTP de terceiros.
- A porta `ISaasBillingGatewayProvider` é estendida com os métodos assíncronos:
  - `CreateSubscriptionCheckoutSessionAsync(...)`: Gera a sessão segura de checkout para contratação/upgrade via cartão de crédito.
  - `CreateCustomerPortalSessionAsync(...)`: Gera link de autoatendimento para que o lojista atualize seu cartão de crédito com segurança no Stripe Customer Portal.
- O SDK `Stripe.net` é adicionado exclusivamente como dependência da camada de infraestrutura (`CarWashSaaS.Billing.Infrastructure`).

### 2. Fluxo de Contratação e Cartão de Crédito via Stripe Checkout
- Na contratação ou upgrade de plano, a aplicação cria um `Customer` da Stripe com metadados do tenant (`tenant_id`) e uma sessão de checkout (`mode: "subscription"`, restrita a `payment_method_types: ["card"]`).
- O lojista é redirecionado para a página segura hospedada pela Stripe, eliminando qualquer tráfego ou custódia de dados de cartão nos servidores da Lavaway (garantindo escopo PCI SAQ-A).

### 3. Webhooks Idempotentes e Concorrência Segura
- O endpoint `/platform/billing/webhooks` recebe e valida a assinatura criptográfica dos eventos da Stripe via cabeçalho `Stripe-Signature` e segredo compartilhado (`whsec_...`).
- O serviço de aplicação `SaasBillingWebhookApplicationService` processa os eventos com idempotência estrita via `IProcessedSaasWebhookEventRepository`:
  - `checkout.session.completed`: Conclui o onboarding, vincula IDs externos e ativa a assinatura do tenant.
  - `invoice.paid` / `invoice.payment_succeeded`: Renova o ciclo de faturamento por 30 dias, gera/baixa a `SaasInvoice` e reseta os limites de cotas de OS e WhatsApp em `TenantQuotaUsage`.
  - `invoice.payment_failed`: Aplica a regra de carência (*grace period* de 5 dias) com banner de alerta.
  - `customer.subscription.deleted`: Transita a assinatura para `Canceled`, bloqueando novas OS e mensagens em `YardOperations` e `WhatsApp`.

### 4. Portal do Lojista no Blazor WebAssembly
- Na página `/settings/subscription`, o lojista pode alternar de plano através de um modal com suporte direto a "Contratar com Cartão de Crédito".
- Um botão de autoatendimento permite acessar o Stripe Customer Portal para trocar de cartão ou consultar faturas anteriores.

## Consequências

### Positivas
- Zero custódia de dados de cartão de crédito no servidor do SaaS.
- Cobrança recorrente mensal automatizada sem necessidade de intervenção manual da equipe.
- Redução drástica de inadimplência por meio de retentativas inteligentes do Stripe Smart Retries.
- Total aderência às regras do `agents.md`, com testes de arquitetura (`NetArchTest`) garantindo desacoplamento modular.

### Mitigações
- Dependência de rede para geração de checkout sessions; mitigada por timeout resiliente e manutenção do `SimulatedSaasBillingGatewayProvider` para ambientes de desenvolvimento offline e suítes de testes unitários.
