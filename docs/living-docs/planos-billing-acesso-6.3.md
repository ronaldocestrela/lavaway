# Planos Comerciais, Billing Recorrente do SaaS e Gestão de Acesso — Subfase 6.3

Este documento descreve a arquitetura, modelos, diagramas de sequência e especificações executáveis implementados para gestão de planos comerciais, cobrança recorrente B2B da plataforma, aplicação de cotas e política de inadimplência.

---

## 1. Máquina de Estados da Assinatura do SaaS (`TenantSubscriptionStatus`)

```mermaid
stateDiagram-v2
    [*] --> Trial: Criação do Estabelecimento (14 dias padrão)
    Trial --> Active: Contratação de Plano / Pagamento Inicial
    Trial --> Delinquent: Fim do prazo de testes sem contratação

    Active --> GracePeriod: Vencimento de fatura sem confirmação
    Active --> Active: Renovação do ciclo confirmada (InvoicePaid)
    
    GracePeriod --> Active: Quitação dentro da carência (<= 5 dias)
    GracePeriod --> Delinquent: Carência expirada (> 5 dias)

    Delinquent --> Active: Liquidação de pendência / Desbloqueio imediato
    Delinquent --> Canceled: Inadimplência prolongada (> 30 dias)

    Active --> Canceled: Cancelamento solicitado
    Canceled --> Active: Reativação manual
```

---

## 2. Fluxo de Processamento de Webhooks, Atualização de Cotas e Auditoria

```mermaid
sequenceDiagram
    autonumber
    participant Gateway as Provedor de Pagamento (Asaas/Stripe/Simulado)
    participant Endpoint as POST /platform/billing/webhooks
    participant WebhookSvc as SaasBillingWebhookApplicationService
    participant SubRepo as ITenantSaasSubscriptionRepository
    participant QuotaRepo as ITenantQuotaUsageRepository
    participant InvoiceRepo as ISaasInvoiceRepository
    participant TenantLookup as IGlobalTenantLookup

    Gateway->>Endpoint: Envia webhook (ex: invoice.paid, signature)
    Endpoint->>WebhookSvc: ProcessWebhookAsync(payload, rawBody, signature)
    WebhookSvc->>WebhookSvc: Valida assinatura criptográfica (HMAC-SHA256)
    WebhookSvc->>WebhookSvc: Verifica idempotência contra reentregas
    
    alt Evento: invoice.paid
        WebhookSvc->>InvoiceRepo: Marca fatura como "Paid"
        WebhookSvc->>SubRepo: Renova ciclo (novo ciclo de 30 dias) -> Status Active
        WebhookSvc->>QuotaRepo: Reseta contadores de OS e WhatsApp para novo ciclo
        WebhookSvc->>TenantLookup: Atualiza status do tenant -> Active
    else Evento: invoice.overdue
        WebhookSvc->>InvoiceRepo: Marca fatura como "Overdue"
        WebhookSvc->>SubRepo: Avalia carência (até 5 dias -> GracePeriod; > 5 dias -> Delinquent)
        opt Status Delinquent
            WebhookSvc->>TenantLookup: Atualiza status do tenant -> Delinquent
        end
    end

    WebhookSvc-->>Endpoint: Result.Success()
    Endpoint-->>Gateway: 200 OK
```

---

## 3. Matriz de Planos Comerciais e Cotas de Operação

| Recurso / Cota | **Básico (Basic)** | **Pro (Pro)** | **Enterprise (Enterprise)** |
| :--- | :--- | :--- | :--- |
| **Valor Mensal** | R$ 149,00 / mês | R$ 299,00 / mês | R$ 599,00 / mês |
| **Cota de Ordens de Serviço** | Até **150 OS** / ciclo | Até **600 OS** / ciclo | **Ilimitado** (0) |
| **Cota de Mensagens WhatsApp**| Até **300 msgs** / ciclo | Até **1.500 msgs** / ciclo | **Ilimitado** (0) |
| **Pátio, Kanban & Vistoria**  | Sim | Sim | Sim |
| **Clube de Assinaturas (5.5)**| Não | Sim | Sim |
| **Fidelidade & Comissões**    | Não | Sim | Sim |
| **Chatbot IA & Agendamentos**| Não | Sim | Sim |
| **Equipe e Operadores**      | Até 3 operadores | Até 10 operadores | Ilimitado |

---

## 4. Diagrama de Entidades (Schema `billing`)

```mermaid
erDiagram
    SaasPlan ||--o{ TenantSaasSubscription : "define limites de"
    TenantSaasSubscription ||--o{ TenantQuotaUsage : "rastreia consumo"
    TenantSaasSubscription ||--o{ SaasInvoice : "gera faturas de"
    ProcessedSaasWebhookEvent ||..o{ TenantSaasSubscription : "garante idempotencia"

    SaasPlan {
        guid Id PK
        int Tier "Basic | Pro | Enterprise"
        string Name
        decimal MonthlyPrice
        int MaxWorkOrdersPerCycle
        int MaxWhatsAppMessagesPerCycle
        bool HasCustomerSubscriptions
        bool HasLoyalty
        bool HasCommissions
        bool HasAiChatbot
        int MaxTeamMembers
        int MaxBoxes
        bool IsActive
    }

    TenantSaasSubscription {
        guid Id PK
        guid TenantId UK "IMustHaveTenant"
        int PlanTier
        int Status "Trial | Active | GracePeriod | Delinquent | Canceled"
        decimal MonthlyPrice
        datetimeoffset CurrentPeriodStartUtc
        datetimeoffset CurrentPeriodEndUtc
        datetimeoffset GracePeriodEndsAtUtc
        datetimeoffset NextBillingDateUtc
        string GatewayCustomerId
        string GatewaySubscriptionId
        string StatusReason
    }

    TenantQuotaUsage {
        guid Id PK
        guid TenantId "IMustHaveTenant"
        datetimeoffset CycleStartUtc
        datetimeoffset CycleEndUtc
        int WorkOrdersCreatedCount
        int WhatsAppMessagesSentCount
    }

    SaasInvoice {
        guid Id PK
        guid TenantId "IMustHaveTenant"
        string GatewayInvoiceId UK
        decimal Amount
        datetimeoffset DueDateUtc
        datetimeoffset PaidAtUtc
        string Status "Pending | Paid | Overdue | Canceled"
        string PaymentUrl
        string PixQrCode
        string PixCopiaECola
    }

    ProcessedSaasWebhookEvent {
        guid Id PK
        string EventId UK
        string EventType
        guid TenantId
        datetimeoffset ReceivedAtUtc
    }
```

---

## 5. Especificações Executáveis (Living Specs)

### Cenário 1: Bloqueio progressivo por inadimplência
* **Dado** que um estabelecimento ativo tem uma fatura vencida hoje,
* **Quando** o webhook `invoice.overdue` é recebido,
* **Então** a assinatura entra em período de tolerância (`GracePeriod`) por 5 dias, permitindo a continuidade operacional com alerta visual no topo da aplicação.
* **E quando** passam 6 dias sem confirmação do pagamento,
* **Então** a assinatura é suspensa (`Delinquent`), e qualquer tentativa de abertura de nova OS ou envio de WhatsApp é sumariamente bloqueada com código de conflito `tenant.subscription.delinquent`.

### Cenário 2: Reativação autônoma e instantânea
* **Dado** que um estabelecimento está com operação suspensa (`Delinquent`),
* **Quando** o webhook `invoice.paid` é confirmado ou o lojista liquida a fatura via Pix no modal,
* **Então** a fatura é marcada como paga, a assinatura e o status do tenant são imediatamente restaurados para `Active`, as cotas são resetadas para o novo ciclo de 30 dias e a emissão de ordens de serviço é liberada na hora.

### Cenário 3: Respeito inegociável às cotas de plano
* **Dado** um lava-jato no plano `Básico` (limite de 150 OS/mês),
* **Quando** ele atinge 150 ordens de serviço geradas no ciclo atual,
* **Então** a 151ª tentativa de abertura de OS é rejeitada com o erro `tenant.quota.work_orders_exceeded` informando a necessidade de upgrade.
* **E quando** o lojista faz upgrade para o plano `Pro` (600 OS/mês), a nova capacidade passa a valer imediatamente.

### Cenário 4: Governança e Gestão de Planos no Backoffice (`/platform/billing`)
* **Dado** um operador com a role `PlatformBillingAdmin` acessando a aba "Gestão de Planos do SaaS",
* **Quando** ele visualiza os cards comerciais de cada tier (`Basic`, `Pro`, `Enterprise`),
* **Então** ele pode verificar a quantidade de boxes (`MaxBoxes`), tamanho da equipe (`MaxTeamMembers`), cotas e os módulos habilitados (Clube de Assinaturas, Fidelidade, Comissões e Chatbot IA).
* **E quando** ele abre o modal `EditSaasPlanModal` e altera os limites ou ativa/desativa funções do plano,
* **Então** o endpoint `PUT /platform/billing/plans/{tier}` valida as invariantes de negócio via `Result<SaasPlan>` e persiste a atualização no schema `billing.SaasPlans`, refletindo imediatamente no catálogo da plataforma.

### Cenário 5: Catálogo Dinâmico e Público na Landing Page (`Home.razor`)
* **Dado** um visitante anônimo acessando a landing page principal do Lavaway (`/`),
* **Quando** a seção de preços é renderizada,
* **Então** a página consome o endpoint público `GET /public/saas-plans` e renderiza dinamicamente os planos ativos cadastrados no catálogo oficial (`Basic`, `Pro`, `Enterprise`).
* **E quando** o visitante clica para contratar qualquer plano,
* **Então** o modal de onboarding recebe o `SaasPlanTier` exato do plano escolhido, integrando diretamente com o fluxo de registro e período de testes sem dados estáticos divergentes.

### Cenário 6: Validação Centralizada de Recursos e Limites de Infraestrutura (`ITenantPlanQuotaLookup`)
* **Dado** um estabelecimento autenticado,
* **Quando** uma operação consulta o serviço de autorização via `HasFeatureAsync(tenantId, feature)`:
  * Se o estabelecimento estiver no plano `Basic`, recursos como `CustomerSubscriptions`, `Loyalty`, `Commissions` e `AiChatbot` retornam `false`.
  * Se o estabelecimento estiver em `Pro` ou `Enterprise`, os recursos habilitados retornam `true`.
  * Se a assinatura estiver suspensa por inadimplência (`Delinquent`), retorna falha com código `tenant.subscription.delinquent`.
* **E quando** um estabelecimento tenta cadastrar um novo colaborador via `ValidateTeamMemberLimitAsync(tenantId, currentTeamMemberCount)`:
  * Se `currentTeamMemberCount >= plan.MaxTeamMembers` (para planos com limite fixo, ex: 3 no Básico), a criação é bloqueada com o erro `tenant.plan.team_limit_reached`.
  * Se o plano for ilimitado (`MaxTeamMembers <= 0`, ex: Enterprise), a criação é liberada.
* **E quando** um estabelecimento tenta configurar os boxes do pátio via `ValidateBoxesLimitAsync(tenantId, requestedBoxes)`:
  * Se `requestedBoxes > plan.MaxBoxes` (ex: 3 boxes no Básico que suporta até 2), a operação é bloqueada com `tenant.plan.boxes_limit_reached`.
  * Se o plano for ilimitado (`MaxBoxes <= 0`, ex: Enterprise), qualquer quantidade é aceita.

### Cenário 7: Travas no Backend para Proteção de Endpoints e Handlers (Etapa 2)
* **Dado** um estabelecimento sem permissão para módulos opcionais ou com cotas esgotadas:
  * **Clube de Assinaturas**: Em `SubscriptionApplicationService`, requisições a planos ou assinaturas de clientes são rejeitadas com erro `tenant.plan.customer_subscriptions_not_allowed` (`ErrorType.Unauthorized`), mapeado nos endpoints para HTTP `403 Forbidden` com payload `{ error.Code, error.Description }`.
  * **Fidelidade & Bypass Silencioso na OS**: Em `LoyaltyApplicationService`, todas as consultas e resgates de selos são rejeitados com `tenant.plan.loyalty_not_allowed` (`403 Forbidden`). Ao concluir ou registrar pagamento de uma OS em `WorkOrderApplicationService`, o sistema verifica silenciosamente a existência do recurso: se desabilitado, a OS é finalizada com sucesso sem gerar pontos e sem falhar o fluxo operacional.
  * **Comissões de Operadores**: Em `YardSetupApplicationService` e `CommissionApplicationService`, o cadastro de regras e emissão de relatórios de comissão exigem `SaasFeature.Commissions`, retornando `tenant.plan.commissions_not_allowed` (`403 Forbidden`).
  * **Capacidade de Boxes e Equipe Ativa**: Em `YardSetupApplicationService`, ultrapassar os boxes permitidos retorna `tenant.plan.boxes_limit_reached` (`409 Conflict`). O cadastro de novos membros ou reativação de colaboradores inativos verifica apenas a contagem de ativos (`IsActive == true`), bloqueando com `tenant.plan.team_limit_reached` (`409 Conflict`) quando atingido o limite.
  * **Chatbot IA & Fallback Cordial**: Em `InboundWhatsAppMessageHandler`, se o estabelecimento não possui `SaasFeature.AiChatbot`, a mensagem de entrada é interceptada e uma resposta padrão cordial personalizada é enviada (informando que o atendimento automatizado está indisponível e que um atendente responderá em breve), sem iniciar a máquina de estados do chatbot. Se palavras-chave de fidelidade forem digitadas no bot de um tenant sem módulo de fidelidade, uma resposta explicativa amigável é retornada.

