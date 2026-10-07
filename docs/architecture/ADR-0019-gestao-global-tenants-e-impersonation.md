# ADR-0019: Gestão Global de Tenants e Impersonation Auditável para Diagnóstico

## Status
Aceito

## Contexto
Na subfase 6.2 do Lavaway SaaS (Fase 6: Backoffice do SaaS), surgiu a necessidade de:
1. **Governança do Ciclo de Vida dos Lojistas:** Permitir que operadores da plataforma identifiquem e controlem o status comercial de cada estabelecimento cadastrado (`Ativo`, `Em Período de Testes`, `Inadimplente` e `Cancelado`), com prazos de trial e histórico de justificativas.
2. **Diagnóstico Técnico por Impersonation:** Fornecer à equipe de suporte técnico e administradores da plataforma a capacidade de assumir a perspectiva operacional de um tenant de forma controlada, viabilizando a reprodução e solução de incidentes reportados no balcão, pátio ou caixa.
3. **Conformidade e Trilha de Auditoria Imutável:** Impedir o uso indevido de privilégios de impersonation por meio de autorização restrita (`PlatformPermission.ImpersonateTenant`), exigência de justificativa técnica e número de chamado, registro automático e append-only na trilha de auditoria (`AdministrativeAuditEvent`), além de encerramento explícito da sessão com aviso visual permanente no frontend.
4. **Isolamento Multi-Tenant Inegociável:** Garantir que lojistas e colaboradores de lojas (`ShopRole`) não consigam utilizar o mecanismo de impersonation ou cabeçalhos de diagnóstico para acessar dados de terceiros.

## Decisões

### 1. Evolução do Agregado `Tenant` e Estados Comerciais
- No módulo `Tenants`, o agregado `Tenant` foi expandido com:
  - `Status` (`TenantStatus`: `Active`, `Trial`, `Delinquent`, `Canceled`).
  - `StatusChangedAtUtc` e `StatusReason` para registrar o histórico e a justificativa da última alteração de estado.
  - `TrialEndsAtUtc` para controle do período promocional/experimental.
  - Métodos de domínio puros com invariantes: `Activate()`, `StartTrial()`, `MarkDelinquent()`, `Cancel()` e `ChangeStatus()`.
- Criada a migração EF Core `AddTenantStatusAndLifecycle` no schema `tenants`.

### 2. Mecanismo de Impersonation com Trilha Auditável
- O serviço `TenantImpersonationApplicationService` no módulo `Identity` orquestra a sessão:
  - Valida a permissão `PlatformPermission.ImpersonateTenant` do operador solicitante.
  - Exige justificativa técnica obrigatória e referência de ticket/chamado opcional.
  - Verifica a existência do estabelecimento via porta pública `IGlobalTenantLookup`.
  - Registra compulsoriamente na trilha `AdministrativeAuditEvent` a ação `Tenant.Impersonated` com snapshot JSON completo.
  - Quando o diagnóstico é concluído, registra compulsoriamente `Tenant.ImpersonationEnded` com anotações de encerramento.
- Tentativas negadas geram evento de auditoria com status `Failure` e motivo da recusa.

### 3. Pipeline HTTP Seguro e Bloqueio Cross-Tenant
- No `TenantResolverMiddleware`:
  - Operadores de plataforma (`user_realm = "Platform"`) com o cabeçalho seguro `X-Impersonate-Tenant-Id` têm o `CurrentTenantAccessor.TenantId` associado ao tenant alvo, aplicando automaticamente os Global Query Filters do EF Core.
  - Usuários normais de lojas (`ShopRole`) que tentem enviar `X-Impersonate-Tenant-Id` têm o cabeçalho ignorado sumariamente, permanecendo restritos ao seu próprio `tenant_id` validado no token JWT.

### 4. Endpoints REST da Plataforma
- `GET /platform/tenants`: Listagem paginada com suporte a filtros multifatoriais (status, busca textual livre em nome, razão social, CNPJ e cidade).
- `GET /platform/tenants/{id}`: Detalhamento do estabelecimento.
- `PUT /platform/tenants/{id}/status`: Atualização manual de status comercial com registro na auditoria (`Tenant.StatusChanged`).
- `POST /platform/tenants/{id}/impersonate`: Abertura de sessão de diagnóstico auditada.
- `POST /platform/tenants/{id}/end-impersonation`: Encerramento explícito de sessão auditado.

### 5. Frontend Blazor WebAssembly
- Novo container de estado `ImpersonationSessionState` em `CarWashSaaS.Client.Core` coordenando a sessão ativa.
- `JwtAuthorizationMessageHandler` injeta o cabeçalho `X-Impersonate-Tenant-Id` apenas quando a sessão de diagnóstico está ativa.
- Banner de alta visibilidade `ImpersonationDiagnosticBanner.razor` posicionado no topo do `MainLayout.razor`, exibindo dados do estabelecimento diagnosticado, operador, chamado e botão de ação rápida "Encerrar Diagnóstico".
- Nova página `GlobalTenantsPage.razor` (`/platform/tenants`) no menu lateral (item 14), com KPIs visuais, filtros em tempo real, tabela de alta densidade e modais `StartImpersonationModal.razor` e `UpdateTenantStatusModal.razor`.

## Consequências

- **Positivas:**
  - Suporte técnico agora diagnostica problemas operacionais com fidelidade no contexto do lojista sem necessidade de credenciais compartilhadas.
  - Toda sessão de suporte tem autor, data/hora UTC, chamado e encerramento registrados de forma imutável.
  - Gestão clara dos estabelecimentos em período de testes, ativos ou inadimplentes.
  - Zero risco de vazamento de dados para lojistas comuns.
- **Mitigações:**
  - O cabeçalho `X-Impersonate-Tenant-Id` só é aceito para usuários autenticados com realm `Platform` e permissão validada.
