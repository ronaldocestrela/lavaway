# Identidade, Sessão e Permissões — Subfase 1.3

Este documento descreve a arquitetura, modelos e fluxos executáveis implementados para autenticação, autorização por perfil e gestão segura de sessões com tokens JWT e refresh tokens.

---

## 1. Modelo de Domínio e Dados do Módulo Identity

O módulo `CarWashSaaS.Identity` opera no schema `identity` no SQL Server compartilhado, assegurando isolamento multi-tenant via `IMustHaveTenant` e Global Query Filters do EF Core.

```mermaid
erDiagram
    Tenant ||..o{ ApplicationUser : "TenantId lógico"
    Tenant ||..o{ RefreshToken : "TenantId lógico"
    ApplicationUser ||--o{ RefreshToken : "UserId"
    ApplicationUser }o--o{ IdentityRole : "user-role"

    ApplicationUser {
        guid Id PK
        guid TenantId
        string Email
        string UserName
    }
    RefreshToken {
        guid Id PK
        guid TenantId
        guid UserId FK
        string TokenHash
        datetimeoffset ExpiresAtUtc
        datetimeoffset CreatedAtUtc
        datetimeoffset RevokedAtUtc
        string ReplacedByTokenHash
    }
    IdentityRole {
        guid Id PK
        string Name
        string NormalizedName
    }
```

### Regras de Domínio e Invariantes

- **`ShopRole`:** `Administrator`, `Receptionist`, `Operator`.
- **`ShopPermission`:** `ConfigureStore`, `ManageUsers`, `ManageWorkOrders`, `CreateWorkOrders`, `UpdateWorkOrderStatus`, `ViewCustomers`, `ViewReports`.
- **`ShopRolePermissions`:** Matriz pura em domínio mapeando papéis a permissões.
- **`RefreshToken`:**
  - Chave primária `Guid` (UUID v7).
  - Implementa `IMustHaveTenant` para garantir isolamento por estabelecimento.
  - Armazena hash SHA-256 do token criptográfico gerado.
  - Suporta expiração, revogação e encadeamento (`ReplacedByTokenHash`).

---

## 2. Fluxo de Autenticação e Emissão de Tokens

O endpoint `/auth/login` valida credenciais sem propagar exceções (padrão `Result<T>`), gera o par Access Token (JWT) e Refresh Token criptográfico com rotação.

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente (Blazor / Mobile)
    participant API as API (/auth/login)
    participant Service as IdentityApplicationService
    participant UserRepo as IIdentityUserRepository
    participant TokenSvc as ITokenService (JWT)
    participant TokenRepo as IRefreshTokenRepository
    participant DB as SQL Server (identity)

    Cliente->>API: POST /auth/login { email, password, tenantId? }
    API->>Service: AuthenticateAsync(request)
    Service->>UserRepo: FindByEmailAsync(email)
    UserRepo->>DB: SELECT ApplicationUser (IgnoreQueryFilters)
    DB-->>UserRepo: user
    Service->>UserRepo: CheckPasswordAsync(userId, password)
    UserRepo-->>Service: true
    Service->>TokenSvc: GenerateAccessToken(userId, email, tenantId, role, permissions)
    TokenSvc-->>Service: JWT string (60 min)
    Service->>TokenSvc: GenerateRefreshToken() + HashToken()
    TokenSvc-->>Service: rawToken, tokenHash
    Service->>TokenRepo: AddAsync(RefreshToken { tenantId, userId, tokenHash, expiresAt })
    TokenRepo->>DB: INSERT INTO identity.RefreshTokens
    Service-->>API: Result.Success(AuthTokenResponse)
    API-->>Cliente: 200 OK { accessToken, refreshToken, expiresIn, role, permissions }
```

---

## 3. Fluxo de Renovação de Sessão (Token Rotation)

Para mitigar sequestro de sessão e vazamento de tokens duradouros, a cada chamada ao `/auth/refresh` o token anterior é revogado e um novo par é gerado.

```mermaid
sequenceDiagram
    autonumber
    actor Cliente as Cliente
    participant API as API (/auth/refresh)
    participant Service as IdentityApplicationService
    participant TokenRepo as IRefreshTokenRepository
    participant TokenSvc as ITokenService
    participant DB as SQL Server

    Cliente->>API: POST /auth/refresh { refreshToken }
    API->>Service: RefreshTokenAsync(request)
    Service->>TokenSvc: HashToken(refreshToken)
    TokenSvc-->>Service: tokenHash
    Service->>TokenRepo: GetByHashAsync(tokenHash)
    TokenRepo->>DB: SELECT RefreshToken (IgnoreQueryFilters)
    DB-->>TokenRepo: token

    alt Token Revogado (Tentativa de Reutilização Maliciosa)
        Service->>TokenRepo: RevokeAllForUserAsync(userId)
        TokenRepo->>DB: UPDATE identity.RefreshTokens SET RevokedAtUtc = now
        Service-->>API: 401 Unauthorized (auth.refresh_token.revoked)
    else Token Expirado
        Service-->>API: 401 Unauthorized (auth.refresh_token.expired)
    else Token Válido
        Service->>TokenSvc: GenerateRefreshToken() + HashToken()
        Service->>DB: UPDATE token (RevokedAtUtc = now, ReplacedByTokenHash = newHash)
        Service->>DB: INSERT INTO identity.RefreshTokens (newToken)
        Service->>TokenSvc: GenerateAccessToken(...)
        Service-->>API: Result.Success(AuthTokenResponse)
        API-->>Cliente: 200 OK { newAccessToken, newRefreshToken }
    end
```

---

## 4. Endpoints REST da Camada de Apresentação

| Método | Rota | Autenticação | Política / Autorização | Descrição |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/auth/login` | Anônimo | Nenhuma | Autentica usuário e emite JWT + Refresh Token |
| `POST` | `/auth/refresh` | Anônimo | Nenhuma | Rotação de sessão: consome refresh token antigo e emite novo par |
| `POST` | `/auth/revoke` | Anônimo | Nenhuma | Invalida/revoga refresh token de forma idempotente |
| `GET` | `/auth/me` | Bearer JWT | Fallback (Authenticated) | Retorna identificadores, tenant resolvido, role e permissões |
| `POST` | `/identity/users` | Bearer JWT | `AdministratorPolicy` | Cria novo colaborador associado ao tenant autenticado |
| `GET` | `/identity/users` | Bearer JWT | `AdministratorPolicy` | Lista colaboradores do tenant autenticado |

---

## 5. Integração com Frontend Blazor

No projeto [CarWashSaaS.Client.Web](file:///home/rony/LPR/lavaway/src/Frontend/CarWashSaaS.Client.Web):
1. **`ITokenStorage` / `InMemoryTokenStorage`:** abstrai o armazenamento volátil e persistente dos tokens.
2. **`JwtAuthenticationStateProvider`:** decodifica o JWT no cliente, extrai as claims (`sub`, `email`, `role`, `permission`, `tenant_id`) e alimenta o sistema de autorização nativo do Blazor (`<AuthorizeView>`, `<CascadingAuthenticationState>`).
3. **`JwtAuthorizationMessageHandler`:** injeta compulsoriamente o cabeçalho `Authorization: Bearer <token>` em todas as requisições HTTP para a API.
4. **`AuthApiClient`:** cliente fortemente tipado em [CarWashSaaS.Client.Core](file:///home/rony/LPR/lavaway/src/Frontend/CarWashSaaS.Client.Core) com métodos `LoginAsync`, `RefreshTokenAsync`, `RevokeTokenAsync`, `CreateUserAsync` e `ListUsersAsync` utilizando exclusivamente o padrão `Result<T>`.

---

## 6. Governança e Testes Automatizados

- **Testes Unitários de Domínio:** [RefreshTokenTests.cs](file:///home/rony/LPR/lavaway/tests/Backend/UnitTests/CarWashSaaS.UnitTests/Identity/RefreshTokenTests.cs) (validação, criação com UUID v7, invariantes de revogação e expiração).
- **Testes Unitários de Aplicação:** [IdentityApplicationServiceTests.cs](file:///home/rony/LPR/lavaway/tests/Backend/UnitTests/CarWashSaaS.UnitTests/Identity/IdentityApplicationServiceTests.cs) (login, validação de senha, rotação de refresh token, detecção de reutilização, revogação e criação de usuários).
- **Testes de Arquitetura:** [ModuleBoundaryTests.cs](file:///home/rony/LPR/lavaway/tests/Backend/ArchitectureTests/CarWashSaaS.ArchitectureTests/ModuleBoundaryTests.cs) (garantia de que `RefreshToken` possui `IMustHaveTenant`, identificador `Guid` e obedece às fronteiras hexagonais).
- **Testes de Isolamento Multi-Tenant:** [TenantIsolationIntegrationTests.cs](file:///home/rony/LPR/lavaway/tests/Backend/IntegrationTests/CarWashSaaS.IntegrationTests/TenantIsolationIntegrationTests.cs) e [ModuleModelTests.cs](file:///home/rony/LPR/lavaway/tests/Backend/IntegrationTests/CarWashSaaS.IntegrationTests/ModuleModelTests.cs) (filtro global por tenant ativo no EF Core).
