# ADR-0004: Storage e fila self-hosted

- **Status:** Aceita e validada operacionalmente
- **Data:** 2026-10-01 (Atualizada em 2026-10-02)

## Contexto

A subfase 1.4 precisa armazenar arquivos privados com isolamento por tenant e processar trabalho assíncrono sem depender de serviços em memória. Os adapters devem ficar atrás de contratos e não introduzir dependências externas nos módulos de domínio.

## Decisão

- Usar MinIO como adapter inicial de `ITenantObjectStorage`, com bucket configurado por ambiente e objetos sob `tenants/{tenantId}/{category}/{fileName}`.
- Manter o bucket privado. Upload e download ocorrem pela API autenticada, que deriva o tenant do contexto validado; não publicar caminhos arbitrários nem expor o bucket como static files.
- Usar RabbitMQ como implementação de `IBackgroundQueue`, com URI AMQP/AMQPS configurada por ambiente.
- Publicar mensagens persistentes com `MessageId` e manter a fila principal como quorum queue, com limite de entregas e dead-letter queue.
- O worker estabelece o tenant no escopo de cada entrega, confirma após sucesso, rejeita eventos sem handler e aplica backoff exponencial antes de reenfileirar falhas transitórias.
- Tratar as entregas como pelo menos uma vez. Handlers devem ser idempotentes; a deduplicação persistente ainda não é fornecida pelo adapter.

## Consequências e gates validados

- Os contratos de storage e fila permitem substituir os providers sem alterar regras de domínio.
- Credenciais e endpoints são configurações externas; valores reais não devem ser versionados.
- Testes de integração com MinIO real executados com sucesso via Testcontainers, validando gravação, leitura e isolamento multi-tenant.
- Testes de integração com RabbitMQ real executados com sucesso via Testcontainers, validando quorum queue, persistência, reentrega com backoff e roteamento para DLQ (`.dead`).
- O worker `TenantQueueWorker` foi validado ponta a ponta com injeção de escopo de tenant e despacho para o handler de negócio `TenantBrandingAuditQueueHandler`.
- Living docs consolidados em `docs/living-docs/arquivos-processamento-assincrono-1.4.md`.

## Alternativas consideradas

- **Sistema de arquivos local e fila em memória:** mantidos apenas como implementação de teste/desenvolvimento; não oferecem durabilidade adequada entre reinícios nem deployment escalável.
- **Storage ou broker gerenciados:** não adotados nesta implementação inicial para manter a opção self-hosted definida para o ambiente atual. Podem ser avaliados futuramente atrás dos mesmos contratos.
