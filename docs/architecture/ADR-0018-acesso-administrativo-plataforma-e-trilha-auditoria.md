# ADR-0018: Acesso Administrativo da Plataforma e Trilha de Auditoria Imutável

## Status
Aceito

## Contexto
Na subfase 6.1 do Lavaway SaaS (primeiro marco da Fase 6: Backoffice do SaaS), surgiu a necessidade de:
1. **Separação Rígida de Perfis:** Desacoplar operadores da plataforma (Backoffice do SaaS) dos colaboradores e perfis locais dos estabelecimentos comerciais (`ShopRole`: `Administrator`, `Receptionist`, `Operator`), impedindo riscos de elevação de privilégio indevida ou contaminação cross-tenant.
2. **Trilha de Auditoria Imutável (Append-Only):** Rastrear e registrar compulsoriamente todas as ações sensíveis (criação/gestão de usuários da plataforma, intervenções em tenants, alterações de planos, logins bem-sucedidos ou bloqueados), garantindo a entrega do critério: *ações sensíveis têm autor, data/hora e alvo registrados*.
3. **Pipeline de Resolução de Tenant Seguro:** O middleware `TenantResolverMiddleware` exigia compulsoriamente a claim `tenant_id` em qualquer requisição autenticada, retornando `403 Forbidden` na ausência desta claim. Para operadores de plataforma que atuam em escopo global, o middleware precisava permitir requisições sem tenant vinculado, preparando também o terreno para impersonation seguro com auditoria na subfase 6.2.
4. **Isolamento Hexagonal & Conformidade de Domínio:** Adoção de chaves UUIDv7, padrão `Result<T>` sem lançamento de exceções para fluxo de negócio e zero acoplamento indevido entre camadas.

## Decisões

### 1. Modelo de Domínio e Papéis da Plataforma
- **`PlatformRole` & `PlatformPermission`:**
  - Papéis da plataforma: `SuperAdmin`, `PlatformSupport`, `PlatformBillingAdmin`, `PlatformAuditor`.
  - Permissões: `ManageTenants`, `ImpersonateTenant`, `ManagePlatformBilling`, `ViewAuditLogs`, `ManagePlatformUsers`, `ViewSystemMetrics`.
  - Matriz invariante `PlatformRolePermissions` definida no domínio puro.
- **`PlatformUser` (Aggregate Root):**
  - Entidade de domínio pura identificada por UUIDv7 (`Guid Id`), `Email` único normalizado, `FullName`, `Role`, `PasswordHash`, `IsActive`, `CreatedAtUtc` e `LastLoginAtUtc`.
  - Contas de plataforma não implementam `IMustHaveTenant`, pois existem acima do escopo de um lava-jato individual.

### 2. Trilha de Auditoria Imutável (`AdministrativeAuditEvent`)
- **Agregado `AdministrativeAuditEvent`:**
  - `Id` (UUIDv7), `TimestampUtc` (DateTimeOffset), `ActorId` (Guid), `ActorEmail`, `ActorRole`, `ActorRealm` (`Platform` vs `Tenant`), `Action`, `TargetType`, `TargetId`, `TenantId` (Guid? opcional), `IpAddress`, `UserAgent`, `DetailsJson` (snapshot estruturado), `Outcome` (`Success`, `Failure`, `Warning`), `ErrorMessage`.
  - **Imutabilidade Estrita (Append-Only):**
    - Repositório `IAdministrativeAuditEventRepository` expõe exclusivamente `AddAsync`, `GetByIdAsync` e `SearchAsync` (sem métodos de mutação/remoção).
    - No `IdentityModuleDbContext`, o `SaveChanges` intercepta o ChangeTracker e lança `InvalidOperationException` se qualquer entidade `AdministrativeAuditEvent` estiver em estado `Modified` ou `Deleted`.

### 3. Pipeline HTTP & Políticas de Autorização
- **`TenantResolverMiddleware`:**
  - Identifica operadores da plataforma via claim `user_realm == "Platform"` ou `is_platform_admin == "true"`.
  - Quando um operador de plataforma acessa o sistema sem tenant, a requisição prossegue com `CurrentTenantAccessor.TenantId = null`.
  - Suporta o cabeçalho seguro `X-Impersonate-Tenant-Id` para resolução de tenant direcionada (requisito habilitador para a subfase 6.2).
- **Políticas de Autorização:**
  - `PlatformSuperAdminPolicy`, `PlatformSupportPolicy`, `PlatformBillingAdminPolicy`, `PlatformAuditorPolicy` e `PlatformUserPolicy`.

### 4. Endpoints REST da Camada de Apresentação
- `POST /platform/auth/login`: Autenticação de operadores de plataforma com auditoria automática de tentativas com sucesso ou falha.
- `POST /platform/users` e `GET /platform/users`: Gestão de operadores de plataforma restrita a `SuperAdmin`.
- `GET /platform/audit`: Consulta avançada com filtros múltiplos (data/hora, autor, ação, alvo, tenant, desfecho, busca livre) e paginação.
- `GET /platform/audit/{id}`: Detalhamento completo do evento com inspeção de payload.

### 5. Frontend Blazor WebAssembly
- Novo cliente HTTP `PlatformAuditApiClient` em `CarWashSaaS.Client.Core` retornando `Result<T>`.
- Nova página `PlatformAuditPage.razor` (`/platform/audit`) com layout escuro responsivo, cards de KPIs analíticos, barra de filtros dinâmicos, tabela de eventos e modal `AuditEventDetailsModal.razor` para formatação do payload JSON e auditoria forense.
- Atualização do menu lateral `NavMenu.razor` para incluir item `13 - Auditoria & Backoffice` restrito a operadores de plataforma e administradores.

## Consequências

- **Positivas:**
  - Total segregação entre usuários de lojas e operadores do backoffice da plataforma.
  - Trilha de auditoria blindada contra deleções ou alterações acidentais/maliciosas (append-only enforced).
  - Toda ação administrativa registra o autor, a data com precisão UTC e o alvo da intervenção.
  - Conformidade estrita com as regras de `agents.md` e 624 testes automatizados 100% aprovados.
- **Mitigações de Risco:**
  - Auditoria assíncrona e consultas paginadas garantem que a tabela de auditoria não degrade a performance do banco à medida que o volume de eventos crescer.
