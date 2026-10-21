# Relatório de Diagnóstico: Controle de Acesso e Limites por Plano de Assinatura

**Data:** 21 de Outubro de 2026  
**Branch:** `fix/plan_limits`  
**Autor:** Antigravity AI  
**Status do Diagnóstico:** Concluído  

---

## 1. Resumo Executivo

Ao analisar a arquitetura e o código-fonte da plataforma **Lavaway**, identificou-se a causa exata pela qual **todas as funcionalidades do sistema continuam ativas para os estabelecimentos, independentemente do plano contratado** (Básico, Pro ou Enterprise).

Em síntese:
1. **As flags de funcionalidades e limites numéricos existem apenas como dados cadastrais**: As propriedades `HasCustomerSubscriptions`, `HasLoyalty`, `HasCommissions`, `HasAiChatbot`, `MaxTeamMembers` e `MaxBoxes` foram modeladas na entidade `SaasPlan` e nos DTOs, porém foram consumidas unicamente para **fins de exibição comercial** (na Landing Page `/` e no painel de administração da plataforma `/platform/billing`).
2. **O Backend valida apenas cotas de consumo e inadimplência**: Os únicos controles de plano efetivamente conectados à lógica de execução são o limite mensal de Ordens de Serviço (`MaxWorkOrdersPerCycle`), o limite de mensagens WhatsApp (`MaxWhatsAppMessagesPerCycle`) e a suspensão financeira (`Delinquent`).
3. **Não existem travas em Endpoints nem no Frontend**: Nem as rotas da API (Backend) nem os componentes de tela/menu (Frontend em Blazor Web) realizam qualquer checagem das permissões do plano contratado pelo estabelecimento logado antes de liberar a visualização ou a execução de ações (como criar regras de comissão, emitir selos de fidelidade, criar planos de assinaturas para clientes ou rodar o chatbot no WhatsApp).

---

## 2. Matriz de Conformidade: Plano Comercial vs. Comportamento Real do Sistema

| Funcionalidade / Limite | Básico (R$ 149) | Pro (R$ 299) | Enterprise (R$ 599) | Situação Real no Código |
| :--- | :---: | :---: | :---: | :--- |
| **Cota Mensal de Ordens de Serviço** | Até 150 OS | Até 600 OS | Ilimitado | 🟢 **Validado:** `WorkOrderApplicationService` bloqueia ao atingir o limite. |
| **Cota Mensal de Mensagens WhatsApp** | Até 300 msgs | Até 1.500 msgs | Ilimitado | 🟢 **Validado:** `WhatsAppMessageApplicationService` bloqueia ao atingir o limite. |
| **Bloqueio por Inadimplência** | Sim | Sim | Sim | 🟢 **Validado:** Status `Delinquent` bloqueia OS e WhatsApp. |
| **Clube de Assinaturas de Clientes (`HasCustomerSubscriptions`)** | ❌ Não | ✔️ Sim | ✔️ Sim | 🔴 **Falha:** Totalmente liberado no plano Básico. Endpoints e páginas não verificam a flag. |
| **Programa de Fidelidade e Selos (`HasLoyalty`)** | ❌ Não | ✔️ Sim | ✔️ Sim | 🔴 **Falha:** Totalmente liberado no plano Básico. OS continua pontuando e clientes resgatam prêmios. |
| **Gestão e Rateio de Comissões (`HasCommissions`)** | ❌ Não | ✔️ Sim | ✔️ Sim | 🔴 **Falha:** Totalmente liberado no plano Básico. Regras e relatórios de comissão acessíveis. |
| **Chatbot com IA no WhatsApp (`HasAiChatbot`)** | ❌ Não | ✔️ Sim | ✔️ Sim | 🔴 **Falha:** Chatbot responde e agenda serviços para qualquer loja, inclusive plano Básico. |
| **Limite de Operadores na Equipe (`MaxTeamMembers`)** | Até 3 | Até 10 | Ilimitado | 🔴 **Falha:** Não há limite. Lojas no plano Básico podem cadastrar operadores ilimitados. |
| **Limite de Boxes de Pátio (`MaxBoxes`)** | Até 2 | Até 5 | Ilimitado | 🔴 **Falha:** Não há limite. Lojas no plano Básico podem cadastrar quantos boxes desejarem. |

---

## 3. Diagnóstico Técnico Detalhado por Camada

### 3.1. Camada de Frontend (Blazor WebAssembly)

1. **Menu Principal Incondicional (`src/Frontend/CarWashSaaS.Client.Web/Layout/NavMenu.razor`)**:
   - Os itens de menu **"Assinaturas & Clubes"** (`/subscriptions`), **"Fidelidade & Selos"** (`/loyalty`) e **"Caixa & Comissões"** (`/cashier`) estão liberados para qualquer usuário autenticado com perfil de loja (`Administrator` ou `Receptionist`), sem avaliar o plano da assinatura do estabelecimento.
   - Os links de **"Capacidade & Equipe"** (`settings/team`) e **"WhatsApp"** (`settings/whatsapp`) também não apresentam qualquer sinalização ou restrição de plano.

2. **Páginas sem Guards ou Tratamento de Upgrade**:
   - `SubscriptionsPage.razor`: Carrega normalmente para qualquer tenant e permite cadastrar novos planos mensais (`OpenNewPlanModal`) e criar assinaturas para clientes (`OpenNewSubscriptionModal`).
   - `LoyaltyPage.razor`: Permite ativar e configurar campanhas de selos, consultar pontuações e resgatar recompensas.
   - `CashierPage.razor`: Exibe a aba de cálculo e rateio de comissões para qualquer plano.
   - `CapacityAndTeamPage.razor`: Permite adicionar novos operadores e expandir a quantidade de boxes sem avisar se excedeu o pacote do plano.

3. **Ausência de Dados do Plano no Frontend**:
   - O objeto `TenantSubscriptionOverviewDto` consumido pelo portal do lojista (`/settings/subscription`) **não contém** as propriedades booleanas do plano (`HasCustomerSubscriptions`, `HasLoyalty`, `HasCommissions`, `HasAiChatbot`, `MaxTeamMembers`, `MaxBoxes`).
   - O cliente Web não possui um serviço de injeção de dependência que guarde o estado das funcionalidades liberadas do tenant para bloquear menus e rotas dinamicamente.

---

### 3.2. Camada de Endpoints e API (Backend Minimal APIs)

1. **Módulo de Assinaturas (`src/Backend/CarWashSaaS.Api/Endpoints/SubscriptionEndpoints.cs`)**:
   - `POST /subscriptions/plans` e `POST /subscriptions/`:
     Exigem apenas `RequireAuthorization(AuthorizationPolicyNames.Administrator)` ou `Receptionist`.
     **Nenhuma validação** é feita sobre se o plano contratado pelo estabelecimento possui `HasCustomerSubscriptions == true`.

2. **Módulo de Fidelidade (`src/Backend/CarWashSaaS.Api/Endpoints/LoyaltyEndpoints.cs`)**:
   - `PUT /loyalty/program`, `POST /loyalty/customers/redeem` e `POST /loyalty/customers/adjust`:
     Todos os endpoints respondem sem verificar se o plano do tenant possui `HasLoyalty == true`.

3. **Módulo de Comissões (`src/Backend/CarWashSaaS.Api/Endpoints/YardOperationsEndpoints.cs`)**:
   - `POST /commission-rules`, `PUT /commission-rules/{id}` e `GET /yard/commissions`:
     Permitem criar e listar regras de comissão sem verificar `HasCommissions == true`.

4. **Módulo de Equipe e Capacidade (`src/Backend/CarWashSaaS.Api/Endpoints/YardOperationsEndpoints.cs`)**:
   - `POST /team-members`: Não consulta a contagem de operadores ativos versus `MaxTeamMembers` do plano.
   - `POST /yard/capacity` e `PUT /yard/capacity`: Não compara `command.TotalBoxes` contra `MaxBoxes` do plano.

---

### 3.3. Camada de Serviços de Aplicação e Background Jobs

1. **Chatbot IA no WhatsApp (`src/Backend/Modules/WhatsApp/CarWashSaaS.WhatsApp.Application/InboundWhatsAppMessageHandler.cs` e `ChatbotConversationEngine.cs`)**:
   - Quando um cliente envia uma mensagem no WhatsApp do lava-jato, o evento assíncrono `InboundWhatsAppMessageHandler.HandleAsync` invoca diretamente o `ChatbotConversationEngine.ProcessIncomingMessageAsync`.
   - O motor de conversa não possui referência ou checagem para saber se o tenant contratou o plano com `HasAiChatbot`. Com isso, lojas no plano Básico têm o chatbot de autoatendimento e agendamento respondendo a todos os clientes.
   - Além disso, no passo de consulta de selos/fidelidade do chatbot (`IsLoyaltyKeyword`), o bot responde com o progresso do cartão fidelidade mesmo que a loja não tenha contratado o módulo de fidelidade.

2. **Acúmulo Automático de Fidelidade (`src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Application/WorkOrderApplicationService.cs`)**:
   - Nas linhas 272-274 e 857-859, ao concluir ou receber pagamento de uma OS, o serviço chama:
     ```csharp
     await loyaltyApplicationService.ProcessWorkOrderLoyaltyAccrualAsync(tenantId, workOrder.Id, ct);
     ```
   - Essa chamada é feita incondicionalmente, sem checar se o plano da loja tem `HasLoyalty`.

3. **Contrato de Quotas Limitado (`src/Shared/CarWashSaaS.Shared.Contracts/ITenantPlanQuotaLookup.cs`)**:
   - A interface atual possui apenas 5 métodos:
     - `CheckWorkOrderQuotaAsync`
     - `ConsumeWorkOrderQuotaAsync`
     - `CheckWhatsAppQuotaAsync`
     - `ConsumeWhatsAppQuotaAsync`
     - `GetSubscriptionOverviewAsync`
   - Não existe método para consultar permissões de recursos funcionais (ex.: `CheckFeatureAccessAsync(tenantId, feature)`) nem validações para tamanho de equipe e boxes.

---

## 4. Plano de Ação Recomendado (Para Implementação na Branch `fix/plan_limits`)

Para solucionar o problema de forma robusta e alinhada à arquitetura do projeto, recomenda-se a execução das seguintes etapas:

### Etapa 1: Enriquecer Contratos e Serviços de Validação de Plano (Shared & Billing) — 🟢 **Concluída**
1. **Adicionar propriedades no DTO de Assinatura (`TenantSubscriptionOverviewDto`)**:
   - Incluídos `bool HasCustomerSubscriptions`, `bool HasLoyalty`, `bool HasCommissions`, `bool HasAiChatbot`, `int MaxTeamMembers`, `int MaxBoxes`.
2. **Expandir a interface `ITenantPlanQuotaLookup` e criar o enum `SaasFeature`**:
   - Adicionado enum `SaasFeature` em `Shared.Contracts`.
   - Adicionados métodos para verificação de permissão e limites em `ITenantPlanQuotaLookup`:
     - `Task<Result<bool>> HasFeatureAsync(Guid tenantId, SaasFeature feature, CancellationToken ct = default);`
     - `Task<Result> ValidateTeamMemberLimitAsync(Guid tenantId, int currentTeamMemberCount, CancellationToken ct = default);`
     - `Task<Result> ValidateBoxesLimitAsync(Guid tenantId, int requestedBoxes, CancellationToken ct = default);`
3. **Implementar a verificação em `TenantPlanQuotaApplicationService`**:
   - Implementadas as checagens com validação de `tenantId`, bloqueio por inadimplência (`Delinquent`), tratamento para limites ilimitados (`<= 0`) e mapeamento completo no `GetSubscriptionOverviewAsync`.
   - Coberto por suíte de testes unitários TDD (100% verde).

### Etapa 2: Aplicar Travas no Backend (Proteção de Endpoints e Handlers) — 🟢 **Concluída**
1. **Assinaturas de Clientes**:
   - Em `SubscriptionApplicationService`, validação centralizada com `EnsureFeatureAccessAsync(tenantId, SaasFeature.CustomerSubscriptions)` retornando `ErrorType.Unauthorized` (`tenant.plan.customer_subscriptions_not_allowed`).
   - Em `SubscriptionEndpoints.cs`, mapeamento de `ErrorType.Unauthorized` para HTTP `403 Forbidden` (`{ error.Code, error.Description }`).
2. **Fidelidade e Conclusão de OS**:
   - Em `LoyaltyApplicationService`, proteção integral de consultas e resgates com `tenant.plan.loyalty_not_allowed` (`403 Forbidden` em `LoyaltyEndpoints.cs`).
   - Em `WorkOrderApplicationService.cs`, bypass silencioso: ao concluir a OS (`UpdateStatusAsync` -> `ReadyForPickup`) e no pagamento (`RecordPaymentAsync`), verifica se o plano possui `Loyalty`; caso não possua, conclui a OS normalmente sem travar o lojista e sem gerar pontuação.
3. **Comissões**:
   - Em `YardSetupApplicationService` e `CommissionApplicationService`, métodos de regras e relatórios de comissões protegidos com `tenant.plan.commissions_not_allowed` (`403 Forbidden` nos endpoints de comissão em `YardOperationsEndpoints.cs`).
4. **Chatbot IA no WhatsApp**:
   - Em `InboundWhatsAppMessageHandler.cs`, interceptação automática de mensagens recebidas: se o plano não possuir `SaasFeature.AiChatbot`, dispara resposta cordial padrão informando que o atendimento automatizado está indisponível e que um atendente humano responderá em breve, sem iniciar a sessão do bot.
   - Em `ChatbotConversationEngine.cs`, caso um cliente digite comandos de fidelidade em um estabelecimento sem o módulo ativo, o bot responde educadamente informando que a loja não possui programa ativo.
5. **Equipe e Boxes**:
   - Em `YardSetupApplicationService`:
     - Validação de `MaxBoxes` em `CreateCapacityAsync` e `UpdateCapacityAsync` (`tenant.plan.boxes_limit_reached` -> `409 Conflict`).
     - Validação de equipe em `CreateTeamMemberAsync` e na reativação em `ToggleTeamMemberStatusAsync`, contando apenas membros ativos (`IsActive == true`) contra `MaxTeamMembers` do plano (`tenant.plan.team_limit_reached` -> `409 Conflict`).
6. **Testes e Qualidade**:
   - 100% dos testes unitários, testes de componentes, testes de arquitetura e testes de integração com Testcontainers SQL Server passando (827 testes verdes).


### Etapa 3: Aplicar Travas e Experiência de Upgrade no Frontend (Blazor Web) — 🟢 **Concluída**
1. **Serviço de Contexto e Estado do Plano (`TenantPlanStateService`)**:
   - Criado em `CarWashSaaS.Client.Core` e registrado no container DI do Blazor Web.
   - Cacheia o estado do plano ativo via `SaasBillingApiClient.GetTenantOverviewAsync()`, notificando reativamente componentes através do evento `OnChange`.
   - Inicializado automaticamente no `MainLayout.razor` após a autenticação da sessão.
2. **Badges no Menu Principal (`NavMenu.razor`)**:
   - Renderiza badges discretos e elegantes `🔒 PRO` nos links de **Fidelidade & Selos** e **Assinaturas & Clubes** quando o estabelecimento estiver no Plano Básico.
   - Os links permanecem clicáveis para engajar o lojista com a tela de conversão e upgrade.
3. **Componente de Proteção e Conversão (`PlanGate.razor`)**:
   - Bloqueia visualmente áreas exclusivas de planos superiores com design dark moderno, lista de benefícios e botão direto `⚡ Fazer Upgrade Agora`.
   - Conectado diretamente ao `UpgradePlanModal.razor` para troca de plano ou checkout no Stripe sem sair da página.
   - Aplicado integralmente em `SubscriptionsPage.razor` (`HasCustomerSubscriptions`) e `LoyaltyPage.razor` (`HasLoyalty`), evitando também disparos desnecessários à API quando o módulo não estiver contratado.
4. **Proteção em Telas de Gestão e Operação**:
   - **Caixa & Comissões (`CashierPage.razor`)**: Card "Comissões do Dia" exibe selo `🔒 Plano Pro` e a aba "Apuração de Comissões" é protegida pelo `PlanGate`, mantendo o fluxo do caixa diário intacto para estabelecimentos no Plano Básico.
   - **Capacidade & Equipe (`CapacityAndTeamPage.razor`)**:
     - `YardCapacityCard`: Valida e exibe o limite físico de boxes do plano (ex.: até 2 boxes no Plano Básico).
     - Equipe: Exibe indicador de colaboradores ativos versus limite do plano (`MaxTeamMembers`) e substitui o botão por `⚡ Limite Atingido (Fazer Upgrade)` ao atingir o teto.
     - Regras de Comissão: Aba protegida com `PlanGate` para `HasCommissions`.
   - **Canal do WhatsApp (`WhatsAppSettingsPage.razor`)**: Exibe card de status de "Atendimento Inteligente com IA", indicando se o bot conversacional está ativo ou se requer upgrade para o Plano Pro.
5. **Engenharia de Qualidade e TDD**:
   - Suíte de testes com bUnit cobrindo `PlanGatingTests` (bloqueio, liberação, badges no menu, limites de capacidade e reatividade de estado).
   - 100% dos testes da solução passando (834 testes verdes).

---

## 5. Conclusão

O comportamento observado ocorre porque a plataforma Lavaway atualmente possui apenas a **camada de tarifação e cotas numéricas de OS/WhatsApp implementada**, enquanto as **regras de restrição de módulos (feature flags) e limites de infraestrutura física/equipe foram apenas declaradas nas tabelas de banco de dados, sem terem sido conectadas aos endpoints e às telas**.

A implementação das etapas propostas garantirá que cada estabelecimento acesse estritamente o que foi contratado, incentivando upgrades orgânicos para os planos **Pro** e **Enterprise**.
