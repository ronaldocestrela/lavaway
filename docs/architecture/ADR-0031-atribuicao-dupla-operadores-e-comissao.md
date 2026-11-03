# ADR-0031: Atribuição de Dupla de Operadores na Lavagem e Apuração de Comissões

## Status
Aceito

## Contexto
Na operação real de centros de estética automotiva e lava-jatos, grande parte das ordens de serviço é executada em dupla (ex.: lavador principal cuidando de chassi/carroceria e segundo colaborador/auxiliar cuidando de secagem, vidros e aspiração interna).
Anteriormente, o sistema permitia atribuir apenas um operador (`AssignedOperatorId`), limitando a rastreabilidade da equipe e impedindo a apuração justa de comissões para o segundo colaborador envolvido no serviço.

## Decisão de Arquitetura

### 1. Modelo de Domínio (`YardOperations.Domain`)
* A entidade raiz `WorkOrder` foi expandida com as propriedades:
  * `AssignedSecondaryOperatorId`: identificador do segundo colaborador (UUID/Guid, nullable).
  * `AssignedSecondaryOperatorName`: snapshot imutável do nome do segundo colaborador no momento da atribuição.
* O método de domínio `AssignOperator(Guid? operatorId, string? operatorName, Guid? secondaryOperatorId = null, string? secondaryOperatorName = null)`:
  * Valida identificadores nulos/vazios (`work_order.operator.invalid`, `work_order.secondary_operator.invalid`).
  * Impede a atribuição da mesma pessoa simultaneamente como operador principal e secundário (`work_order.duplicate_assigned_operator`).
  * Permite atribuir 1 operador, 2 operadores ou desatribuir qualquer uma das posições independentemente.
* O método `ChangeStatus(...)` também recebe os operadores de forma unificada e preserva os dados da dupla.

### 2. Persistência e Multi-Tenancy (`YardOperations.Infrastructure`)
* Mapeamento EF Core em `WorkOrderConfiguration.cs` com colunas na tabela `yard.WorkOrders`:
  * `AssignedSecondaryOperatorId` (`uniqueidentifier`, null).
  * `AssignedSecondaryOperatorName` (`nvarchar(200)`, null).
* Criada a migração EF Core `AddWorkOrderSecondaryOperator`.
* O repositório `WorkOrderRepository.ListForCommissionReportAsync` ajusta os filtros para incluir ordens onde o colaborador conste como operador principal ou secundário.

### 3. Regra de Comissões (`CommissionApplicationService`)
* Cada colaborador ativo vinculado à ordem de serviço (principal ou secundário) recebe **100% da sua respectiva comissão** calculada sobre os itens executados, com base nas regras configuradas para o seu cargo específico (`TeamMember.Role`).

### 4. Notificações em Tempo Real (`SignalRYardRealtimeNotifier` & `Shared.Contracts`)
* Os contratos compartilhados (`AssignOperatorRequest`, `WorkOrderDto`, `WorkOrderKanbanCardDto`, `WorkOrderMovedNotification`) foram estendidos com os campos do segundo operador.
* O canal SignalR transmite os eventos `OperatorAssigned` e `WorkOrderMoved` contendo os dados de ambos os operadores para atualização instantânea nas estações de trabalho do pátio.

### 5. Frontend Blazor (`Client.Components` & `Client.Web`)
* **`YardKanbanCard.razor`:**
  * Exibe badges estilizados de ambos os colaboradores (badge primário esmeralda e badge secundário azul).
  * Quando houver apenas 1 operador atribuído, exibe botão rápido `+2º` para adicionar o segundo lavador com um clique.
* **`YardKanbanBoard.razor`:**
  * Modal responsivo de atribuição dupla (`.modal-dialog-wide`) permitindo escolher o 1º Operador e o 2º Operador com prevenção visual de duplicidade (desabilitando na lista o colaborador já escolhido no outro slot).
  * Filtro de operador na barra de ferramentas do Kanban passa a filtrar cards onde o operador selecionado é o principal OU o secundário.

## Consequências
* **Positivas:**
  * Suporte nativo à realidade operacional de lavagens em duplas sem sobrecarga de joins 1:N.
  * Cálculo automatizado e justo de 100% da comissão para cada operador participante.
  * Interface ágil com feedback visual instantâneo para a equipe do pátio.
  * Preservação integral do isolamento multi-tenant e das convenções arquiteturais do `agents.md`.

