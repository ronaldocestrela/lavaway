# ADR-0021: Monitoramento de Saúde de Instâncias WhatsApp e Política de Alertas de Desconexão

- **Status:** Aceita
- **Data:** 2026-10-07
- **Subfase:** 6.4 Saúde das instâncias WhatsApp

## Contexto

O SaaS depende fundamentalmente do WhatsApp para emissão de ordens de serviço, envio de QR Code Pix dinâmico, agendamentos conversacionais e lembretes de pátio. No entanto, instâncias de WhatsApp conectadas a provedores como a Evolution API (Baileys) estão sujeitas a desconexões imprevistas por instabilidade de rede móvel, bateria descarregada, reinicialização de contêineres ou revogação de sessão pelo usuário no aplicativo.

Sem um mecanismo centralizado de observabilidade e alerta:
1. A equipe de suporte da plataforma não tem visibilidade consolidada de quais lojas estão offline.
2. O lojista não percebe que o canal caiu até que clientes reclamem da falta de mensagens.
3. Tentativas de alerta reativas podem causar spam de e-mails em quedas intermitentes frequentes (flapping).

## Decisão

1. **Detecção Híbrida de Falhas (Reativa + Proativa):**
   - **Reativa:** Webhooks de `connection_update` com estados `close`, `closed` ou `disconnected` atualizam imediatamente o estado da conexão para `Disconnected`, registram o motivo da queda e carimbam `LastDisconnectedAtUtc`.
   - **Proativa:** Um serviço hosted (`WhatsAppHealthMonitoringHostedService`) executa varreduras periódicas (a cada 5 minutos) via porta `IWhatsAppHealthCheckProvider` consultando `GET /instance/connectionState/{instanceName}` para identificar sessões zumbis que caíram silenciosamente sem disparo de webhook.
2. **Política Anti-Spam e Cooldown de Alertas:**
   - A entidade de domínio `WhatsAppConnection` governa as regras invariantes de disparo via `ShouldSendAlert(TimeSpan cooldown, DateTimeOffset now)`.
   - O intervalo mínimo de cooldown entre alertas automáticos para o mesmo tenant é de **60 minutos**.
   - Disparos manuais realizados pela equipe de suporte através do Backoffice ignoram o cooldown automático.
3. **Desacoplamento Intermodular via Contratos:**
   - A resolução dos dados de contato do administrador do tenant utiliza a porta pública `ITenantNotificationContactLookup` em `CarWashSaaS.Shared.Contracts`, mantendo a separação hexagonal estrita entre os módulos `WhatsApp`, `Tenants` e `Identity`.
4. **Governança no Backoffice e Isolamento Multi-Tenant:**
   - Apenas operadores de plataforma com as permissões `PlatformSupport` ou `SuperAdmin` têm acesso aos endpoints globais `/platform/whatsapp/*`.
   - O repositório `IWhatsAppConnectionRepository` implementa `ListAllConnectionsAsync` com `IgnoreQueryFilters()` exclusivamente para o serviço de governança da plataforma. Lojistas comuns permanecem rigorosamente isolados sob os Global Query Filters do EF Core.
5. **Histórico de Incidentes:**
   - Toda alteração de estado (desconexão, restabelecimento ou sondagem manual) é registrada de forma imutável na tabela `whatsapp.WhatsAppConnectionIncidents` para fins de auditoria e métricas de SLA.
6. **Experiência do Lojista (Frontend Blazor):**
   - Um banner contextual chamativo (`WhatsAppDisconnectedAlertBanner.razor`) é exibido nas telas de configuração e operação quando a instância está desconectada, orientando o lojista a gerar novo QR Code com um clique.

## Consequências

- **Positivas:**
  - Detecção precoce e confiável de falhas de comunicação com o WhatsApp.
  - O lojista é avisado imediatamente por e-mail e in-app, reduzindo o tempo médio de reparo (MTTR) da conexão.
  - Zero risco de flood ou spam de notificações em oscilações momentâneas de sinal.
  - Isolamento multi-tenant garantido tanto no banco de dados quanto na camada de API.
- **Negativas / Desafios:**
  - Pequeno overhead periódico de requisições de probe HTTP contra a Evolution API para tenants conectados.

## Conformidade

- Total conformidade com o manual arquitetural `agents.md`: Arquitetura Hexagonal, padrão `Result<T>`, identificadores UUID (Guid v7), TDD estrito e living documentation.
