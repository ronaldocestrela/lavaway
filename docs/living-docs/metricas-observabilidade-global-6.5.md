# Métricas e Observabilidade Global da Plataforma — Subfase 6.5

Este documento consolida a arquitetura, modelos de dados, fluxo de integração desacoplada, contratos, fórmulas matemáticas e especificações executáveis implementados para observabilidade contínua, telemetria de negócio e investigação operacional no Backoffice do Lavaway.

---

## 1. Arquitetura Hexagonal de Agregação e Observabilidade

```mermaid
flowchart TD
    subgraph Frontend ["Frontend Blazor WebAssembly (/platform/metrics)"]
        UI[PlatformObservabilityPage.razor]
        Client[PlatformObservabilityApiClient]
        UI --> Client
    end

    subgraph Host ["API Host (CarWashSaaS.Api)"]
        Endpoints["GET /platform/metrics/overview<br/>GET /platform/observability/webhooks<br/>GET /platform/observability/whatsapp-failures"]
        ObsService[PlatformObservabilityApplicationService]
        Endpoints --> ObsService
    end

    subgraph Contracts ["CarWashSaaS.Shared.Contracts (Ports Públicas)"]
        PBilling["IPlatformBillingMetricsLookup"]
        PYard["IPlatformYardMetricsLookup"]
        PWhatsApp["IPlatformWhatsAppObservabilityLookup"]
    end

    subgraph BillingModule ["Módulo Billing"]
        BillingSvc[PlatformBillingMetricsService]
        BillingDb[(BillingDbContext)]
        BillingSvc --> BillingDb
    end

    subgraph YardModule ["Módulo YardOperations"]
        YardSvc[PlatformYardMetricsService]
        YardDb[(YardOperationsDbContext)]
        YardSvc --> YardDb
    end

    subgraph WhatsAppModule ["Módulo WhatsApp"]
        WhatsAppSvc[PlatformWhatsAppObservabilityService]
        WhatsAppDb[(WhatsAppDbContext)]
        WhatsAppSvc --> WhatsAppDb
    end

    Client -->|HTTP Authorization: PlatformUser| Endpoints
    ObsService --> PBilling
    ObsService --> PYard
    ObsService --> PWhatsApp

    PBilling -.->|Implementa| BillingSvc
    PYard -.->|Implementa| YardSvc
    PWhatsApp -.->|Implementa| WhatsAppSvc
```

---

## 2. Metodologia de Cálculo das Métricas de Negócio

```mermaid
classDiagram
    class PlatformMetricsOverviewDto {
        +PlatformSaasRevenueMetricsDto SaasRevenue
        +PlatformYardOperationalMetricsDto YardOperations
        +PlatformPixTransactionalMetricsDto PixTransactions
        +PlatformWhatsAppMetricsSummaryDto WhatsAppSummary
        +DateTimeOffset PeriodStartUtc
        +DateTimeOffset PeriodEndUtc
    }

    class PlatformSaasRevenueMetricsDto {
        +decimal Mrr
        +decimal Arr
        +decimal ChurnRatePercentage
        +decimal Ltv
        +int ActiveSubscriptionsCount
        +int TrialSubscriptionsCount
        +int DelinquentSubscriptionsCount
        +int CanceledInPeriodCount
        +List~SaasPlanDistributionDto~ PlanDistribution
    }

    class PlatformYardOperationalMetricsDto {
        +int TotalAttendedVehicles
        +int InProgressWorkOrders
        +decimal AverageDailyAttendedVehicles
        +List~DailyVehicleVolumeDto~ DailyVolume
    }

    class PlatformPixTransactionalMetricsDto {
        +decimal TotalAmountTransacted
        +int TotalTransactionsCount
        +decimal AverageTicket
        +List~DailyPixVolumeDto~ DailyVolume
    }

    PlatformMetricsOverviewDto --> PlatformSaasRevenueMetricsDto
    PlatformMetricsOverviewDto --> PlatformYardOperationalMetricsDto
    PlatformMetricsOverviewDto --> PlatformPixTransactionalMetricsDto
```

### Fórmulas Matemáticas
1. **MRR (Monthly Recurring Revenue):**
   $$\text{MRR} = \sum_{\text{status} \in \{\text{Active}, \text{GracePeriod}\}} \text{MonthlyPrice}$$
2. **ARR (Annual Recurring Revenue):**
   $$\text{ARR} = \text{MRR} \times 12$$
3. **Churn Rate (%):**
   $$\text{Churn Rate} = \frac{\text{Cancelamentos no Período}}{\text{Ativos} + \text{Cancelamentos no Período}} \times 100$$
4. **LTV (Lifetime Value):**
   $$\text{ARPU} = \frac{\text{MRR}}{\text{Ativos}} \quad\implies\quad \text{LTV} = \frac{\text{ARPU}}{\text{Churn Rate (em decimal)}}$$
   *(Fallback para Churn = 0%: média histórica de faturas pagas por tenant)*
5. **Veículos Atendidos:**
   Contagem de ordens de serviço (`WorkOrder`) com `PickedUpAtUtc` dentro do intervalo especificado.
6. **Volume Pix:**
   Soma e contagem de cobranças Pix (`PixCharge`) liquidadas com status `Paid` dentro do intervalo especificado.

---

## 3. Fluxo de Investigação de Incidentes Operacionais

```mermaid
sequenceDiagram
    autonumber
    participant Op as Operador de Plataforma (Backoffice)
    participant UI as PlatformObservabilityPage.razor
    participant Api as GET /platform/observability/webhooks
    participant Service as PlatformObservabilityApplicationService
    participant Port as IPlatformBillingMetricsLookup
    participant Modal as WebhookPayloadInspectionModal.razor

    Op->>UI: Seleciona aba "Logs de Webhooks Recebidos"
    UI->>Api: Requisita logs paginados
    Api->>Service: GetWebhookLogsAsync(request)
    Service->>Port: ListWebhookLogsAsync(from, to, provider, status)
    Port-->>Service: Coleção de logs unificados
    Service-->>Api: Result.Success(PlatformWebhookLogDto[])
    Api-->>UI: HTTP 200 OK
    UI-->>Op: Exibe tabela de eventos e status

    Op->>UI: Clica em "Inspecionar" em um evento suspeito
    UI->>Modal: Abre modal com TxId, PayloadHash e Notas
    Modal-->>Op: Exibe dados técnicos sanitizados para diagnóstico
```

---

## 4. Endpoints REST da Plataforma

| Método | Endpoint | Política de Autorização | Descrição |
| :--- | :--- | :--- | :--- |
| `GET` | `/platform/metrics/overview` | `PlatformUserPolicy` | Consolida MRR, ARR, Churn, LTV, veículos e Pix na janela temporal. |
| `GET` | `/platform/observability/webhooks` | `PlatformUserPolicy` | Lista logs de webhooks recebidos dos provedores externos com paginação. |
| `GET` | `/platform/observability/whatsapp-failures` | `PlatformUserPolicy` | Lista histórico de mensagens de WhatsApp com falha, motivo e tentativas. |

---

## 5. Especificações Executáveis (Suíte de Testes)

- **UnitTests:** `PlatformBillingMetricsServiceTests`, `PlatformYardMetricsServiceTests` e `PlatformWhatsAppObservabilityServiceTests` validam cálculos matemáticos, tratamentos de casos de borda e mascaramento LGPD de telefones.
- **ArchitectureTests:** `ModuleBoundaryTests` garante que não há acoplamento direto entre os bancos de dados dos módulos e que toda a comunicação de observabilidade respeita as portas hexagonais em `CarWashSaaS.Shared.Contracts`.
- **IntegrationTests:** `PlatformObservabilityIntegrationTests` valida o isolamento de dados por tenant e a agregação global de dados no SQL Server via Testcontainers.
- **ComponentTests:** `PlatformObservabilityComponentTests` e `RouteAuthorizationTests` validam renderização de modais e proteção de rotas com controle de perfis administrativos.
