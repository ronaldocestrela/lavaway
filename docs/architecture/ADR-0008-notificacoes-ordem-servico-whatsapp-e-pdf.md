# ADR-0008: Notificações da Ordem de Serviço, Mensagens com Mídia e Emissão de Comprovante em PDF

## Status
Aceito

## Contexto
Na subfase 4.2 do Lavaway SaaS, tornou-se mandatória a automação do fluxo de notificações operacionais da ordem de serviço aos clientes finais:
1. **Comprovante de Entrada:** Emissão de voucher digital em PDF contendo dados da OS, itens de serviço contratados, checklist de inspeção de entrada e mapeamento de avarias existentes.
2. **Aviso de Veículo Pronto:** Notificação imediata quando a ordem de serviço transita para o status `Pronto para Retirada`, com dados do veículo, total e local de retirada.
3. **Galeria Antes e Depois:** Envio de fotos pós-serviço (polimento, vitrificação, acabamento) via WhatsApp.

Restrições arquiteturais (`agents.md`):
- O monólito modular proíbe referências diretas a `DbContext` ou entidades internas entre módulos.
- Os modelos de domínio devem ser puros (Hexagonal Architecture), sem dependências externas.
- A aplicação é hospedada em containers Linux/Docker (Ubuntu/Alpine), onde bibliotecas nativas de renderização gráfica (como `SkiaSharp` ou motores WebKit/Chromium) causam frequentes quebras de dependência de runtime.
- Multi-tenancy com isolamento absoluto e garantia de idempotência para evitar envios duplicados.

## Decisões

### 1. Comunicação Desacoplada entre Módulos via `Shared.Contracts`
- O módulo `YardOperations` não acessa o banco do módulo `WhatsApp` nem do módulo `Tenants`.
- Definida a interface `IOutboundWhatsAppDispatcher` no projeto `Shared.Contracts`, implementada pelo `WhatsAppMessageApplicationService` no módulo `WhatsApp`, permitindo envio assíncrono ou imediato de mensagens de texto e mídia.
- Definida a interface `ITenantStoreProfileLookup` no projeto `Shared.Contracts`, implementada por `StoreProfileApplicationService` no módulo `Tenants`, retornando `Result<StoreProfileDto>`.
- O desacoplamento atende 100% às regras do NetArchTest de dependências inter-módulos e retorno em Result Pattern.

### 2. Gerador de PDF em C# Puro sem Dependências Nativas
- Implementado `WorkOrderReceiptPdfGenerator` que constrói um fluxo PDF 1.4 em bytes puros (objetos, dicionários, cross-reference table e streams de texto e formas vetoriais com operadores PDF padrão).
- Não requer `SkiaSharp`, `QuestPDF` com binários C++ não gerenciados ou headless browsers, garantindo inicialização instantânea, baixíssimo consumo de memória e total portabilidade em Linux e Docker.
- O PDF gerado é persistido de forma multi-tenant em `ITenantObjectStorage` na categoria `work-orders/{workOrderId}/receipt.pdf`.

### 3. Extensão do Módulo WhatsApp para Suporte a Mídia (Document / Image)
- A entidade `OutboundWhatsAppMessage` foi estendida com os campos de mídia (`MediaType`, `MediaUrl`, `MediaMimeType`, `MediaFileName`) e o método de fábrica `CreateWithMedia`.
- O cliente `EvolutionApiWhatsAppMessageSender` implementa `SendMediaMessageAsync` invocando o endpoint `POST /message/sendMedia/{instanceName}` da Evolution API.
- Adicionada migration EF Core `AddMediaSupportToWhatsAppMessage` preservando compatibilidade do schema com o snapshot de migrações.

### 4. Processamento Assíncrono com Idempotência Durável
- Eventos de domínio/aplicação `WorkOrderReadyForPickupEvent` e `WorkOrderReceiptRequestedEvent` são enfileirados no `IBackgroundQueue` (RabbitMQ).
- Handlers `WorkOrderReadyNotificationQueueHandler` e `WorkOrderReceiptNotificationQueueHandler` processam os eventos em background sem travar a thread de requisição HTTP do operador.
- As mensagens utilizam chaves de idempotência unívocas por tenant: `receipt-{workOrderId}` e `ready-{workOrderId}`.

## Consequências
- **Positivas:**
  - Zero acoplamento entre os módulos de Pátio, Clientes, Estabelecimento e WhatsApp.
  - Emissão de PDF extremamente rápida, determinística e livre de dependências de SO.
  - Conformidade arquitetural estrita verificada automaticamente por testes de arquitetura (NetArchTest).
  - Resiliência operacional através de filas duráveis e política anti-ban/taxa de envio.
- **Negativas / Trade-offs:**
  - A formatação do PDF requer manutenção das instruções de desenho em baixo nível (operadores PDF), porém o layout é estável e focado em comprovante térmico/A4 padronizado.
