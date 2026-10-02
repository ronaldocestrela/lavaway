# ADR-0005: Emissão de Tokens JWT e Refresh Tokens com Isolamento por Tenant

- **Status:** Aceita para a subfase 1.3
- **Data:** 2026-10-02

## Contexto

A subfase 1.3 do roadmap prevê a conclusão de identidade, sessão e permissões, complementando a base de `ShopRole`, `ShopPermission` e `ShopRolePermissions`.
O sistema adota a arquitetura de monolito modular com isolamento lógico multi-tenant no mesmo banco de dados SQL Server.
Até o momento, a API dependia de um validador JwtBearer apontando para `Authority` externo, mas a plataforma necessita de autonomia para que o módulo `Identity` realize o cadastro de colaboradores por loja, autenticação via credenciais, emissão segura de Access Tokens JWT e gestão de sessões com Refresh Tokens e rotação automática.

## Decisão

1. **Arquitetura Modular do Identity:**
   - O módulo `CarWashSaaS.Identity` é o proprietário dos fluxos de cadastro, autenticação e sessão.
   - A camada `Domain` define as regras invariantes de papéis (`ShopRole`), permissões (`ShopPermission`) e a entidade `RefreshToken`.
   - A camada `Application` orquestra os casos de uso de login, renovação (refresh), revogação e registro de novos colaboradores, com retorno obrigatório no padrão `Result<T>`.
   - A camada `Infrastructure` implementa o gerador de JWT (`JwtTokenGenerator`), hashing seguro de refresh tokens (SHA-256) e persistência no `IdentityModuleDbContext`.

2. **Access Token (JWT):**
   - Assinado com algoritmo HMAC-SHA256 (`HmacSha256`) utilizando chave criptográfica configurada em `Authentication:SigningKey` (mínimo 256 bits).
   - O issuer e audience padrão respeitam `Authentication:Issuer` (ou `Authority`) e `Authentication:Audience`.
   - Tempo de vida curto (ex: 60 minutos).
   - Claims obrigatórias no payload:
     - `sub`: identificador único do usuário (`Guid`).
     - `email`: e-mail do usuário.
     - `tenant_id`: identificador único do tenant (`Guid`), fundamental para o `TenantResolverMiddleware`.
     - `role`: papel do usuário (`ShopRole`).
     - `permission`: lista das permissões efetivas do papel.

3. **Refresh Token e Rotação de Sessão:**
   - A entidade `RefreshToken` implementa `IMustHaveTenant`, garantindo que uma sessão pertença exclusivamente a um tenant.
   - O token emitido para o cliente é uma string segura aleatória gerada via `RandomNumberGenerator` (256 bits em Base64Url).
   - No banco de dados, armazena-se apenas o hash SHA-256 do token, evitando exposição direta em caso de leitura indevida.
   - **Rotação:** A cada chamada ao endpoint de refresh, o token anterior é revogado e substituído por um novo par (novo Access Token + novo Refresh Token).
   - **Detecção de Reutilização Maliciosa:** Se um refresh token já revogado for apresentado para renovação, a cadeia é considerada comprometida.
   - Tempo de vida de 7 a 30 dias.

4. **Isolamento Multi-Tenant:**
   - O cadastro de usuários e a emissão de tokens exigem e validam a vinculação do usuário ao `TenantId`.
   - Global Query Filters no `IdentityModuleDbContext` impedem que o Tenant B consulte ou modifique tokens e usuários do Tenant A.
   - O login valida que o usuário pertence ao tenant informado ou identifica o tenant a partir da conta do usuário.

## Consequências e controles

- **Segurança Reforçada:** Rotação de refresh token com hash em repouso previne replay attacks e sequestro duradouro de sessão.
- **Transparência para a API:** O token JWT emitido pelo próprio módulo é compatível com o middleware de tenant existente e com as policies de autorização do ASP.NET Core (`AdministratorPolicy`, `ReceptionistPolicy`, etc.).
- **TDD:** Casos de uso e regras de domínio são validados por suítes de testes unitários e de integração antes de expor os endpoints.
- **Documentação Viva:** O fluxo de autenticação e ciclo de vida de sessão é registrado em `docs/living-docs/identidade-sessao-permissoes-1.3.md`.

## Alternativas consideradas

- **Depender exclusivamente de IdP externo (Keycloak / Duende):** Aumenta complexidade operacional e custo de implantação para clientes em fase inicial. O suporte a JWT próprio no módulo Identity atende à autonomia do monólito modular, mantendo compatibilidade caso um IdP externo seja plugado no futuro.
- **Refresh tokens em texto puro no banco:** Rejeitado por violar boas práticas de segurança OWASP.
- **Tokens sem rotação:** Rejeitado pelo risco elevado de reutilização de credenciais roubadas.
