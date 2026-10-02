# ADR-0004: Storage e fila self-hosted

- **Status:** Aceita para a implementação inicial; validação operacional pendente
- **Data:** 2026-10-01

## Contexto

A subfase 1.4 precisa armazenar arquivos privados com isolamento por tenant e processar trabalho assíncrono sem depender de serviços em memória. Os adapters devem ficar atrás de contratos e não introduzir dependências externas nos módulos de domínio.

## Decisão

- Usar MinIO como adapter inicial de `ITenantObjectStorage`, com bucket configurado por ambiente e objetos sob `tenants/{tenantId}/{category}/{fileName}`.
- Manter o bucket privado. Upload e download ocorrem pela API autenticada, que deriva o tenant do contexto validado; não publicar caminhos arbitrários nem expor o bucket como static files.
- Usar RabbitMQ como implementação de `IBackgroundQueue`, com URI AMQP/AMQPS configurada por ambiente.
- Publicar mensagens persistentes com `MessageId` e manter a fila principal como quorum queue, com limite de entregas e dead-letter queue.
- O worker estabelece o tenant no escopo de cada entrega, confirma após sucesso, rejeita eventos sem handler e aplica backoff exponencial antes de reenfileirar falhas transitórias.
- Tratar as entregas como pelo menos uma vez. Handlers devem ser idempotentes; a deduplicação persistente ainda não é fornecida pelo adapter.

## Consequências e gates

- Os contratos de storage e fila permitem substituir os providers sem alterar regras de domínio.
- Credenciais e endpoints são configurações externas; valores reais não devem ser versionados.
- O build da API, o build dos testes de integração e os testes unitários focados passaram. Os testes de integração RabbitMQ dependem de Docker/Testcontainers e não foram executados neste ambiente.
- O acesso ponta a ponta ao MinIO ainda precisa de teste de integração, e nenhum handler de negócio está registrado. A subfase 1.4 não deve ser marcada como entregue até esses gates passarem.
- Operação de produção ainda requer deployment durável, TLS, backup/recuperação, monitoramento e gestão de credenciais para MinIO e RabbitMQ.

## Alternativas consideradas

- **Sistema de arquivos local e fila em memória:** mantidos apenas como implementação de teste/desenvolvimento; não oferecem durabilidade adequada entre reinícios nem deployment escalável.
- **Storage ou broker gerenciados:** não adotados nesta implementação inicial para manter a opção self-hosted definida para o ambiente atual. Podem ser avaliados futuramente atrás dos mesmos contratos.
