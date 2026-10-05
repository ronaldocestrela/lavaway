# Pós-Venda, Satisfação e Campanhas de Reativação — Subfase 4.5

## 1. Visão Geral & Objetivo

Maximizar a retenção de clientes, medir com precisão a qualidade dos serviços prestados (CSAT / NPS) e reconquistar veículos inativos no Lavaway SaaS, garantindo total conformidade com políticas anti-spam e regulamentações de privacidade (LGPD).

O sistema automatiza:
1. **Pesquisa de Satisfação em 1 Hora:** Disparo de pesquisa de 1 a 5 estrelas via WhatsApp exatamente 1 hora após a retirada do veículo (`PickedUpAtUtc`).
2. **Campanhas de Reativação Automáticas (15, 30 e 45 dias):** Réguas configuráveis que identificam clientes inativos e disparam convites personalizados para novos agendamentos.
3. **Filtro Anti-Spam (Frequency Capping de 7 dias):** Garante intervalo mínimo de 7 dias entre envios promocionais e exclui automaticamente clientes que já possuem veículos no pátio ou reservas agendadas.
4. **Gestão de Opt-Out & Opt-In (LGPD):** Interceptação imediata de palavras-chave ("PARAR", "SAIR", "STOP") cessando qualquer envio de marketing, com opção de reativação via "QUERO" ou interface web.

---

## 2. Diagramas de Sequência

### 2.1 Pesquisa de Satisfação 1h Pós-Retirada e Coleta de CSAT

```mermaid
sequenceDiagram
    autonumber
    actor Operador as Operador / Balcão
    actor Cliente as Cliente (WhatsApp)
    participant UI as Kanban do Pátio (Blazor)
    participant AfterApp as AfterSalesApplicationService
    participant Repo as IWorkOrderRepository
    participant Cron as AfterSalesSurveyHostedService (a cada 2 min)
    participant FSM as ChatbotConversationEngine
    participant Outbound as IOutboundWhatsAppDispatcher

    Note over Operador,UI: 1. Registro de Retirada do Veículo
    Operador->>UI: Clica em "🚗 Retirada" no card do veículo
    UI->>AfterApp: RegisterPickupAsync(tenantId, workOrderId)
    AfterApp->>Repo: workOrder.RegisterPickup(nowUtc)
    Repo-->>UI: Status atualizado (Badge: "⏳ Pesquisa 1h")

    Note over Cron,Repo: 2. Varredura e Disparo Após 1 Hora
    Cron->>AfterApp: ScanAndDispatchPendingSurveysAsync(tenantId)
    AfterApp->>Repo: GetWorkOrdersPendingSurveyAsync(tenantId, cutoffUtc = now - 1h)
    Repo-->>AfterApp: [WorkOrders com PickedUpAtUtc <= now - 1h e SurveySentAtUtc == null]
    AfterApp->>Outbound: Envia mensagem: "Olá {nome}, seu veículo {veiculo} foi entregue! Avalie de 1 a 5 estrelas ⭐"
    AfterApp->>Repo: workOrder.RecordSatisfactionSurveySent(nowUtc)

    Note over Cliente,FSM: 3. Resposta e Processamento da Avaliação
    Cliente->>FSM: Envia "5" (ou "5 estrelas" / "⭐⭐⭐⭐⭐")
    FSM->>AfterApp: RecordSatisfactionRatingAsync(tenantId, phone, rating=5)
    AfterApp->>Repo: workOrder.RecordSatisfactionRating(5, nowUtc)
    FSM-->>Cliente: "Muito obrigado pela nota 5! Ficamos felizes em cuidar do seu carro. Conte sempre com a Lavaway!"
```

### 2.2 Régua de Reativação com Frequency Capping e Opt-Out

```mermaid
sequenceDiagram
    autonumber
    actor Gestor as Gestor / Administrador
    actor Cliente as Cliente Inativo
    participant Cron as ReactivationCampaignHostedService
    participant CampApp as ReactivationCampaignApplicationService
    participant FreqCap as IFrequencyCappingService
    participant PrefLookup as ICustomerCommunicationPreferenceLookup
    participant Outbound as IOutboundWhatsAppDispatcher

    Note over Cron,CampApp: 1. Execução Automática da Régua (15, 30 ou 45 dias)
    Cron->>CampApp: ScanAndDispatchCampaignRulesAsync(tenantId)
    CampApp->>FreqCap: GetEligibleInactiveCustomersAsync(tenantId, daysInactive=30)
    
    Note over FreqCap: Valida: Sem visitas há 30d, sem ordens ativas, sem agendamentos futuros
    FreqCap-->>CampApp: Lista de clientes candidatos

    loop Para cada cliente candidato
        CampApp->>PrefLookup: CheckOptInAsync(tenantId, customerPhone)
        alt Cliente fez Opt-Out ("PARAR")
            PrefLookup-->>CampApp: Bloqueado (OptedOut)
            CampApp->>CampApp: Incrementa TotalSkippedOptOut
        else Cliente recebeu campanha nos últimos 7 dias
            CampApp->>FreqCap: HasCoolingOffViolation(customerPhone)
            FreqCap-->>CampApp: Bloqueado (FrequencyCap < 7 dias)
            CampApp->>CampApp: Incrementa TotalSkippedFrequencyCap
        else Cliente 100% Elegível
            CampApp->>Outbound: Dispara mensagem personalizada (IdempotencyKey única diária)
            CampApp->>CampApp: Registra ReactivationCampaignLog
        end
    end
```

### 2.3 Opt-Out ("PARAR") e Opt-In ("QUERO") Conversacional

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente (WhatsApp)
    participant FSM as ChatbotConversationEngine
    participant PrefLookup as ICustomerCommunicationPreferenceLookup

    alt Cliente deseja cessar mensagens promocionais
        Cliente->>FSM: Envia "PARAR" (ou "SAIR" / "STOP")
        FSM->>PrefLookup: UpdatePreferenceAsync(tenantId, phone, isOptedIn=false, "Comando PARAR via WhatsApp")
        FSM-->>Cliente: "Você foi removido das nossas mensagens promocionais. Para reativar a qualquer momento, basta responder 'QUERO'."
    else Cliente decide reativar comunicações
        Cliente->>FSM: Envia "QUERO"
        FSM->>PrefLookup: UpdatePreferenceAsync(tenantId, phone, isOptedIn=true, "Comando QUERO via WhatsApp")
        FSM-->>Cliente: "Que ótimo ter você de volta! Suas notificações de promoções e lembretes foram reativadas com sucesso."
    end
```

---

## 3. Diagrama de Estados da Ordem de Serviço & Pós-Venda

```mermaid
stateDiagram-v2
    [*] --> InWashing: Iniciar Serviço
    InWashing --> Finishing: Acabamento
    Finishing --> QualityControl: Controle de Qualidade
    QualityControl --> ReadyForPickup: Veículo Pronto

    ReadyForPickup --> PickedUp: Registrar Retirada (PickedUpAtUtc)
    
    state PickedUp {
        [*] --> WaitingSurvey1h: Aguardando 1 hora
        WaitingSurvey1h --> SurveySent: HostedService despacha WhatsApp
        WaitingSurvey1h --> SurveySkipped: Cliente em Opt-Out
        SurveySent --> RatingReceived: Cliente avalia 1 a 5 estrelas
        SurveySent --> ExpiredWithoutRating: Sem resposta em 48h
    }

    PickedUp --> Inactive15d: 15 dias sem retorno
    Inactive15d --> Inactive30d: 30 dias sem retorno
    Inactive30d --> Inactive45d: 45 dias sem retorno
    
    Inactive15d --> InWashing: Nova Visita (Reativado)
    Inactive30d --> InWashing: Nova Visita (Reativado)
    Inactive45d --> InWashing: Nova Visita (Reativado)
```

---

## 4. Especificações Executáveis (Gherkin)

```gherkin
Funcionalidade: Pós-Venda, Pesquisa de Satisfação e Campanhas de Reativação
  Como gestor de um lava-jato SaaS
  Quero coletar notas de satisfação pós-serviço e reativar clientes ausentes
  Para elevar a retenção (LTV) com respeito integral à LGPD e anti-spam

  Cenário: Disparo automático de pesquisa de satisfação 1 hora após a retirada
    Dado que a ordem de serviço "#WO-901" foi concluída
    E o veículo foi marcado como retirado às 14:00:00 UTC
    Quando o serviço em segundo plano executar às 15:05:00 UTC
    Então uma pesquisa avaliativa de 1 a 5 estrelas deve ser enviada via WhatsApp para o cliente
    E o campo SurveySentAtUtc da ordem de serviço deve ser preenchido
    E o card no Kanban deve exibir o indicador "📩 Enviada"

  Cenário: Cliente responde à pesquisa com nota máxima (5 estrelas)
    Dado que uma pesquisa foi enviada para o telefone "(11) 98888-7777"
    Quando o cliente responder "5" ou "⭐⭐⭐⭐⭐"
    Então o sistema deve registrar a nota 5 na ordem de serviço correspondente
    E o CSAT da loja deve ser recalculado imediatamente
    E o chatbot deve responder agradecendo a confiança

  Cenário: Cliente responde à pesquisa com insatisfação (1 ou 2 estrelas)
    Dado que uma pesquisa foi enviada para o telefone "(11) 98888-7777"
    Quando o cliente responder "1 - o vidro traseiro ficou manchado"
    Então o sistema deve registrar a nota 1 e o feedback na ordem de serviço
    E o chatbot deve responder com pedido de desculpas e garantia de retorno da gerência

  Cenário: Bloqueio de campanha por Frequency Capping (Regra dos 7 dias)
    Dado que o cliente "Mariana Souza" está inativo há 32 dias
    E o cliente recebeu uma mensagem de reativação há 4 dias
    Quando a régua de 30 dias for executada
    Então a mensagem de reativação NÃO deve ser enviada
    E o contador de bloqueios por anti-spam deve ser incrementado

  Cenário: Respeito a Opt-Out imediato via WhatsApp (LGPD)
    Dado que o cliente "(11) 97777-6666" possui opt-in ativo
    Quando o cliente enviar a palavra "PARAR" no WhatsApp
    Então o consentimento para mensagens de marketing deve ser revogado
    E nenhuma régua de 15, 30 ou 45 dias deve enviar mensagens para este telefone
    E o chatbot deve confirmar o opt-out com instruções para reativação ("QUERO")
```
