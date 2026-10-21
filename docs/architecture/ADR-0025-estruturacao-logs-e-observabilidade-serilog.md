# ADR-0025: Estruturação de Logs e Observabilidade com Serilog

## Status
Aceito

## Contexto
O Lavaway opera como um monolito modular multi-tenant no .NET 10. Conforme a regra inegociável descrita no `agents.md`, cada lava-jato (tenant) compartilha o mesmo banco de dados com isolamento lógico via `TenantId` (UUID).

Anteriormente, o sistema utilizava os provedores de log padrão do ASP.NET Core (`Console` e `Debug`), que geravam saídas de texto não estruturado sem vinculação automática do `TenantId` e sem rastreabilidade de requisições por correlação. Em cenários de produção com múltiplos tenants concorrentes e tarefas assíncronas (RabbitMQ, Hosted Services), a ausência de logs estruturados em formato JSON dificulta a filtragem por tenant, a correlação de erros e o monitoramento centralizado em agregadores (ex.: Seq, Elasticsearch/Logstash/Kibana, Grafana Loki, CloudWatch).

Adicionalmente, os princípios de Arquitetura Hexagonal do projeto proíbem qualquer vazamento de bibliotecas externas para o Domínio e para a Aplicação.

## Decisão de Arquitetura

### 1. Confinamento Estrito no Host (CarWashSaaS.Api)
- O **Serilog** e seus sinks/enrichers (`Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, etc.) são adotados exclusivamente como dependência da camada de apresentação/host (`CarWashSaaS.Api`) e suítes de teste.
- As camadas de **Domínio** e **Aplicação** de todos os módulos permanecem **100% agnósticas de fornecedor de log**, consumindo unicamente as abstrações padrão `Microsoft.Extensions.Logging.ILogger<T>` do ecossistema .NET.
- A regra de isolamento é validada de forma automatizada por testes de arquitetura com `NetArchTest`.

### 2. Enriquecimento Automático do Contexto Multi-Tenant (LogContext & DiagnosticContext)
- No pipeline HTTP, o `TenantResolverMiddleware` adiciona compulsoriamente a propriedade `TenantId` ao `Serilog.Context.LogContext`:
  ```csharp
  using (LogContext.PushProperty("TenantId", currentTenantAccessor.TenantId))
  {
      await next(context);
  }
  ```
- Para usuários com impersonação (administradores da plataforma inspecionando um tenant), o `TenantId` impersonado é propagado para o log context.
- O middleware `UseSerilogRequestLogging` é configurado para enriquecer o contexto de diagnóstico com `TenantId`, `UserId`, `ClientIp` e `UserAgent`, além de registrar métricas de duração (`Elapsed`) e status HTTP.
- Nas filas RabbitMQ (`TenantQueueWorker`), as mensagens que contêm `TenantId` têm seu processamento envelopado com o respectivo `LogContext`, garantindo rastreabilidade dos handlers em background.

### 3. Configuração Declarativa & Sinks
- A configuração do Serilog é declarada via `appsettings.json`, permitindo ajuste de níveis de log (`MinimumLevel`) e filtros por namespace sem necessidade de recompilação.
- Sinks configurados:
  - **Console:** Formatado de forma amigável para desenvolvimento, exibindo timestamp, nível, TenantId e mensagem.
  - **Arquivo Rotativo (File):** Gravação diária em JSON estruturado com retenção de 14 dias para arquivamento e ingestão por coletores de telemetria.

### 4. Proteção de Dados e Tratamento de Exceções
- Exceções inesperadas do sistema (HTTP 500) são registradas com a pilha completa no Serilog em nível `Error`. O cliente da API recebe resposta padronizada sem vazamento de detalhes internos.
- Dados sensíveis (senhas, segredos de webhook, tokens de autenticação) nunca são interpolados em mensagens de log de aplicação.

## Consequências

### Positivas
- Rastreabilidade ponta a ponta: qualquer entrada de log possui o `TenantId` anexado automaticamente.
- Conformidade total com a Arquitetura Hexagonal: módulos internos não possuem acoplamento com o Serilog.
- Diagnóstico simplificado de incidentes e análise de performance de endpoints e workers.
- Facilidade de integração com qualquer ferramenta moderna de observabilidade via sink ou ingestão de arquivos JSON.

### Mitigações
- Risco de overhead de I/O em arquivos; mitigado por buffers assíncronos e rotação diária com limite de retenção.
