# ADR-0030: Modificação Administrativa do Plano do Tenant via Backoffice com Trilha de Auditoria

## Status
Aceito

## Contexto
No modelo SaaS multi-tenant do Lavaway, os lojistas gerenciam suas contratações primariamente via autoatendimento com Stripe ou Pix (`/settings/subscription`). No entanto, cenários operacionais, comerciais e de suporte exigem que operadores da plataforma intervenham diretamente no plano de um estabelecimento:
1. **Upgrades / Downgrades Comerciais Manuais:** Contratações via faturamento direto, negociações personalizadas B2B, acordos corporativos ou concessão de cortesias comerciais.
2. **Desbloqueios Emergenciais & Recuperação de Inadimplência:** Liberação manual de cotas e transição de status (`Active`, `Delinquent`, `Canceled`) vinculada à troca de plano mediante comprovação externa.
3. **Sincronização com Provedor de Cobrança:** Caso a assinatura já possua vínculo ativo com um gateway de pagamento (ex.: Stripe), a alteração manual deve refletir no provedor externo para evitar cobranças recorrentes desalinhadas.
4. **Governança e Trilha de Auditoria (Audit Trail):** Toda alteração manual no plano de um cliente é uma operação de alto impacto que altera cotas operacionais, limites de equipe, quantidade de boxes e recursos do sistema. Portanto, deve ser obrigatoriamente auditada com identificação do operador (`actorId`, `actorEmail`, `actorRole`), IP, User-Agent, justificativa obrigatória e detalhes em formato JSON estruturado.

## Decisão de Arquitetura

### 1. Camada de Domínio e Aplicação (`Billing`)
* A raiz de agregação `TenantSaasSubscription` no módulo `Billing` mantém o método `ChangePlan(SaasPlan newPlan)` garantindo as invariantes de negócio (ex.: impedindo alteração em assinaturas canceladas).
* O serviço `PlatformSaasBillingApplicationService` orquestra o caso de uso `AdminOverridePlanAsync(Guid tenantId, AdminOverridePlanRequest request)`:
  - Valida o `tenantId` e busca a assinatura existente (ou inicializa trial caso ainda não exista registro persistido).
  - Consulta o catálogo via `ISaasPlanRepository.GetByTierAsync(...)`.
  - Executa a mudança de plano com validação estrita via `Result<T>`.
  - Se houver `GatewaySubscriptionId` configurado, notifica o `ISaasBillingGatewayProvider.ChangeSubscriptionPlanAsync(...)`.
  - Se for informado novo status comercial (`Active`, `Delinquent`, `Canceled`), atualiza a assinatura e notifica o `IGlobalTenantLookup` para manter a coerência global do estabelecimento.
  - Persiste as alterações no banco via `subscriptionRepository.SaveChangesAsync(...)`.

### 2. Trilha de Auditoria e Segurança nos Endpoints (`Api`)
* O endpoint `POST /platform/billing/subscriptions/{tenantId:guid}/override` é protegido pela política `PlatformAuthorizationPolicyNames.PlatformBillingAdmin`.
* Ao concluir a alteração com sucesso, o endpoint compulsoriamente injeta `AuditTrailApplicationService` e registra um evento imutável com:
  - `Action`: `PlatformActionConstants.BillingPlanModified`
  - `TargetType`: `PlatformTargetTypeConstants.Tenant`
  - `TargetId`: ID do tenant em formato UUID
  - Metadados: operador autenticado (`sub`, `email`, `role`, `user_realm`), IP, User-Agent e payload JSON contendo o plano de destino, novo status e o motivo/justificativa administrativa.

### 3. Frontend Blazor WebAssembly (`Client.Components` & `Client.Web`)
* Foi criado o componente modal isolado `OverrideTenantPlanModal.razor` (e `OverrideTenantPlanModal.razor.css`) na biblioteca `CarWashSaaS.Client.Components`.
* O modal apresenta:
  - Identificação clara do estabelecimento e ID do tenant.
  - Painel com o plano e status comercial atuais.
  - Seleção do novo plano com valores e cotas mensais.
  - Seleção opcional de novo status da assinatura.
  - Campo obrigatório de motivo/justificativa para fins de compliance.
  - Feedback visual com estados de salvamento (`isSubmitting`), exibição de erros e notificações via `IToastService`.
* O modal foi integrado:
  - Na página de **Faturamento Global** (`/platform/billing` - `PlatformBillingPage.razor`), com a nova coluna de Ações e botão `⚙️ Alterar Plano`.
  - Na página de **Gestão Global de Estabelecimentos** (`/platform/tenants` - `GlobalTenantsPage.razor`), como ação rápida `💳 Plano` acessível aos administradores de plataforma.

## Consequências
* **Positivas:**
  - Pleno controle administrativo sobre os planos e cotas de qualquer lava-jato da base.
  - 100% de auditabilidade de intervenções manuais em planos comerciais.
  - Sincronização segura com gateways de pagamento externos.
  - Conformidade estrita com o padrão `Result<T>`, isolamento modular hexagonal e princípios DDD.
  - Cobertura de testes unitários (Application), testes de componente (bUnit) e validação de arquitetura (NetArchTest).
* **Mitigações:**
  - A justificativa administrativa é tornada campo obrigatório na interface para evitar alterações anônimas ou sem rastreabilidade.

