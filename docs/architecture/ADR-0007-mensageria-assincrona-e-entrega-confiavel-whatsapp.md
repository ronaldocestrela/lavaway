# ADR-0007: Mensageria Assíncrona e Entrega Confiável no WhatsApp

- **Status:** Aceita para a entrega 4.1 do Roadmap
- **Data:** 2026-10-04

## Contexto

A subfase 4.1 define a base de integração e entrega confiável para comunicações via WhatsApp. Com o canal pareado via Evolution API na subfase 2.5, o sistema precisa enviar e rastrear mensagens de forma desacoplada, resiliente e segura contra bloqueios de operadora/Meta (anti-ban), respeitando o consentimento do cliente (LGPD/Opt-out) e garantindo isolamento total por tenant.

O envio síncrono acoplado ao ciclo de vida da requisição HTTP foi descartado por introduzir fragilidade de rede, riscos de timeout e falta de rastreabilidade de tentativas de entrega.

## Decisão

1. **Arquitetura Hexagonal & Desacoplamento:**
   - O domínio puro gerencia as regras de negócio de `OutboundWhatsAppMessage`, `TenantWhatsAppQuota` e `CustomerCommunicationPreference`.
   - A camada de aplicação orquestra os casos de uso e depende da abstração de saída `IWhatsAppMessageSender`.
   - A infraestrutura implementa o envio HTTP para a Evolution API em `EvolutionApiWhatsAppMessageSender`.

2. **Processamento Assíncrono com RabbitMQ:**
   - As mensagens a serem enviadas são persistidas inicialmente no SQL Server com status `Queued` e enfileiradas no RabbitMQ via `IBackgroundQueue` como `TenantQueueMessage` com `EventType = "whatsapp.message.dispatch"`.
   - O `TenantQueueWorker` consome a mensagem em background, estabelecendo o escopo do DI e `CurrentTenantAccessor.SetTenant(tenantId)` antes de invocar `OutboundWhatsAppMessageQueueHandler`.

3. **Garantia de Idempotência:**
   - Cada mensagem possui uma `IdempotencyKey` única por tenant.
   - Tentativas repetidas com a mesma chave retornam a mensagem já existente, evitando cobrança duplicada ou envios repetidos ao cliente final.

4. **Governança de Quotas e Anti-Ban (Rate Limiting):**
   - O agregado `TenantWhatsAppQuota` impõe limites de envio por minuto (burst control, padrão 20/min) e por dia (padrão 500/dia), com possibilidade de configuração por plano/tenant.
   - Mensagens que excedam o limite são rejeitadas com erro de negócio tipado (`whatsapp.quota_exceeded`).

5. **Consentimento e Opt-Out:**
   - O agregado `CustomerCommunicationPreference` mantém o registro de opt-in/opt-out por telefone do destinatário.
   - Destinatários que realizaram opt-out têm seus envios rejeitados com erro `whatsapp.recipient.opted_out`.

6. **Ingestão de Webhooks de Status:**
   - O endpoint anônimo de webhook da Evolution API recebe eventos `MESSAGES_UPDATE` / `SEND_MESSAGE` autenticados por `X-Webhook-Secret`.
   - A atualização de status para `Delivered` ou `Read` é realizada de forma idempotente, associando o ID retornado pelo provedor ao identificador interno da mensagem.

## Consequências e Controles

- **Resiliência:** Falhas de rede ou indisponibilidade temporária da Evolution API acionam retentativas com backoff exponencial no RabbitMQ sem perder o estado da mensagem.
- **Rastreabilidade:** Cada tentativa de envio registra um `WhatsAppDeliveryAttempt` com timestamp, código de erro e status HTTP.
- **Isolamento de Tenant:** Todas as tabelas no schema `whatsapp` implementam `IMustHaveTenant`, com filtros globais de consulta no EF Core e validação compulsória em `SaveChangesAsync`.
- **Padrão Result:** Nenhuma exceção de negócio é propagada; todas as operações retornam `Result<T>` com erros expressivos.
