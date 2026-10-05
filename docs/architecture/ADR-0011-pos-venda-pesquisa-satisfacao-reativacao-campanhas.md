# ADR-0011: Pós-Venda, Pesquisa de Satisfação 1h e Campanhas de Reativação com Anti-Spam e Opt-Out

## Status
Aceito

## Contexto
Na subfase 4.5 do Lavaway SaaS, tornou-se necessária a implementação de um mecanismo estruturado de pós-venda, avaliação de satisfação (CSAT / NPS) e campanhas ativas de reativação para clientes inativos, garantindo retenção contínua e conformidade estrita com políticas anti-spam e regulamentações de privacidade (LGPD).

Requisitos fundamentais:
1. **Pesquisa de Satisfação em 1 Hora:** Disparo automático de pesquisa avaliativa de 1 a 5 estrelas via WhatsApp exatamente 1 hora após a retirada do veículo pelo cliente (`PickedUpAtUtc`).
2. **Campanhas de Reativação Escalonadas:** Réguas automatizadas para clientes sem retorno há 15, 30 e 45 dias, estimulando novos agendamentos com mensagens personalizadas.
3. **Filtro Anti-Spam & Frequency Capping:** Janela de resfriamento (*cooling-off period*) de no mínimo 7 dias entre envios promocionais para evitar sobrecarga e bloqueios de número no WhatsApp.
4. **Gestão de Opt-Out & Opt-In (LGPD):** Interceptação imediata de palavras-chave ("PARAR", "SAIR", "STOP") revogando o consentimento para mensagens de marketing, e reativação via "QUERO" ou interface administrativa.
5. **Multi-Tenancy e Concorrência:** Isolamento absoluto entre lojas SaaS (`IMustHaveTenant`), sem vazamento de dados de audiência ou preferências.

## Decisões

### 1. Entidade de Domínio `WorkOrder` e Ciclo de Retirada (`PickedUpAtUtc`)
- A entidade `WorkOrder` no módulo `YardOperations.Domain` foi estendida com os campos:
  - `PickedUpAtUtc`: Carimbo UTC registrado no momento em que o veículo é liberado e entregue ao cliente.
  - `SurveySentAtUtc`: Carimbo UTC do envio da pesquisa de satisfação.
  - `SurveyRating`: Nota avaliativa de 1 a 5 estrelas concedida pelo cliente.
  - `SurveyFeedback`: Comentário opcional ou justificativa enviada pelo cliente.
  - `SurveyRespondedAtUtc`: Carimbo UTC do recebimento da avaliação.
- Métodos ricos de domínio adicionados:
  - `RegisterPickup(DateTimeOffset? pickedUpAtUtc, string? notes)`
  - `RecordSatisfactionSurveySent(DateTimeOffset sentAtUtc)`
  - `MarkSurveySkippedOptOut(DateTimeOffset skippedAtUtc)`
  - `RecordSatisfactionRating(int rating, string? feedback, DateTimeOffset respondedAtUtc)`
- Índice otimizado criado no EF Core: `IX_WorkOrders_TenantId_PickedUpAtUtc_SurveySentAtUtc` para varredura ultra-rápida.

### 2. Entidades de Retenção: `ReactivationCampaignRule` e `ReactivationCampaignLog`
- `ReactivationCampaignRule`:
  - Modela as réguas de retenção (15, 30 e 45 dias) com campos `DaysInactive`, `Title`, `MessageTemplate`, `IsEnabled`, `PromotionalOffer`, `EligibleAudienceCount`, `FrequencyCappedCount`, `OptedOutCount`.
- `ReactivationCampaignLog`:
  - Rastreia o histórico de disparos de campanhas por cliente com chave de idempotência determinística (`reactivation-{ruleId}-{customerId}-{year}-{month}-{day}`) e data de envio para validação do período de resfriamento.

### 3. Comunicação Intermodular via Portas e Adaptadores (`Shared.Contracts`)
- Criadas as interfaces desacopladas:
  - `IAfterSalesLookup`: Permite ao motor de conversação do WhatsApp e serviços externos registrar retirada, registrar avaliação e buscar ordens concluídas por telefone.
  - `ICustomerCommunicationPreferenceLookup`: Gerencia opt-in e opt-out entre os módulos WhatsApp e YardOperations.
- Garantia de independência arquitetural: o módulo `WhatsApp` não acopla diretamente aos repositórios internos do `YardOperations`.

### 4. Motor de Frequência Anti-Spam (`FrequencyCappingService`)
- Implementado `IFrequencyCappingService` com a regra:
  - Um cliente só é elegível a campanhas promocionais se não tiver recebido nenhuma mensagem promocional nos últimos 7 dias (`CoolingOffDays = 7`).
  - Clientes que possuem ordens de serviço ativas no pátio (`Waiting`, `InWashing`, `Finishing`, `QualityControl`, `ReadyForPickup`) ou reservas futuras ativas são automaticamente excluídos da régua de inatividade.

### 5. Interceptação Conversacional no Chatbot WhatsApp (`ChatbotConversationEngine`)
- Interceptação global antes de qualquer processamento de estado:
  - Mensagens contendo "PARAR", "SAIR", "STOP" -> O sistema registra imediatamente o Opt-Out no repositório de preferências e envia mensagem confirmando o cancelamento de comunicações promocionais com instruções para reativar ("QUERO").
  - Mensagens contendo "QUERO" -> Reativa o Opt-In do cliente com mensagem de boas-vindas.
- Interceptação de Avaliação de Satisfação:
  - Se a mensagem do cliente contiver uma nota de 1 a 5 (por exemplo, "5", "5 estrelas", "⭐⭐⭐⭐⭐"), e houver uma ordem de serviço pendente de avaliação para aquele telefone:
    - O sistema registra a nota na ordem via `IAfterSalesLookup.RecordSatisfactionRatingAsync`.
    - Se a nota for 4 ou 5 estrelas: agradece calorosamente e convida para o próximo serviço.
    - Se a nota for 1, 2 ou 3 estrelas: pede desculpas pelo ocorrido, registra o feedback e notifica que o gerente entrará em contato para prestar suporte.

### 6. Trabalhadores em Segundo Plano (`HostedServices`)
- `AfterSalesSurveyHostedService`: Varre periodicamente (a cada 2 minutos) ordens de serviço onde `PickedUpAtUtc <= now - 1h` e `SurveySentAtUtc == null`, despachando a pesquisa e respeitando filtros de opt-out.
- `ReactivationCampaignHostedService`: Varre periodicamente as réguas ativas (15, 30 e 45 dias) e despacha mensagens para a audiência elegível respeitando frequency capping e opt-out.

### 7. Interface de Usuário no Blazor WebAssembly
- Atualização do Kanban (`YardKanbanCard.razor`):
  - Botão de ação "🚗 Retirada" para veículos em `ReadyForPickup` cujo `PickedUpAtUtc` ainda não foi registrado.
  - Badge informativo quando retirado indicando status da pesquisa ("⏳ Pesquisa 1h", "📩 Enviada", "⭐ 5/5").
- Página Dedicada `AfterSalesPage.razor` (`/after-sales`):
  - **Aba 1 (CSAT & Pesquisas):** KPIs de nota média, taxa de resposta, CSAT promotores (4 e 5★), gráfico de barras por estrela e tabela detalhada de avaliações.
  - **Aba 2 (Campanhas de Reativação):** Réguas de 15, 30 e 45 dias com switch ON/OFF, editor de template com variáveis (`{nome}`, `{veiculo}`, `{dias}`), simulador de balão do WhatsApp em tempo real, consulta de audiência elegível vs bloqueada e disparo manual sob demanda.
  - **Aba 3 (Anti-Spam & Opt-Out LGPD):** Explicação da política anti-spam de 7 dias, barra de busca por telefone e tabela de preferências com alternância manual de consentimento.

## Consequências

- **Positivas:**
  - Ciclo fechado de pós-venda automatizado sem custos adicionais de mão de obra.
  - Coleta precisa de CSAT 1 hora após a experiência, momento ideal de engajamento do cliente.
  - Reativação inteligente e contextual de clientes inativos, aumentando o LTV (Lifetime Value).
  - Conformidade plena com LGPD e prevenção de denúncias de spam na Meta / WhatsApp.
  - Métricas e visibilidade em tempo real para a gestão tomar decisões operacionais baseadas em dados.

- **Mitigações de Risco:**
  - O cooling-off de 7 dias e chaves de idempotência diárias evitam que clientes recebam disparos duplicados ou excessivos em múltiplos gatilhos.
  - Falhas pontuais no envio via WhatsApp não abortam a varredura das demais ordens/campanhas devido ao isolamento de transação por item.
