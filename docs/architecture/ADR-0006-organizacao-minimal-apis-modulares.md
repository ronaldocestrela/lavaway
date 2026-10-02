# ADR-0006: Organização e Agrupamento Modular de Minimal APIs

- **Status:** Aceita
- **Data:** 2026-10-02

## Contexto

O projeto adota a arquitetura de **Monólito Modular** em .NET 10 e utiliza **ASP.NET Core Minimal APIs** para expor seus endpoints HTTP.

Com a evolução dos módulos de domínio (`Identity`, `Tenants`, `WhatsApp` e `YardOperations`), o arquivo `Program.cs` atingiu mais de 800 linhas, acumulando rotas, injeções de dependência, validações de payloads e regras auxiliares de serialização. Esse crescimento centralizado prejudicava a legibilidade, a navegabilidade do código e a manutenibilidade do sistema.

Avaliou-se o retorno aos Controllers tradicionais do ASP.NET Core MVC versus a estruturação modular das Minimal APIs por meio de métodos de extensão (`IEndpointRouteBuilder`).

## Decisão

1. **Manutenção do modelo Minimal APIs:**
   * **Desempenho e leveza:** Ausência do pipeline pesado de descoberta por reflexão do MVC, filtros de ação rígidos e model binders complexos.
   * **Injeção de dependências granular:** Dependências (`ICurrentTenantAccessor`, serviços de aplicação, etc.) são injetadas exclusivamente nos parâmetros dos handlers que as utilizam, evitando sobrecarga em construtores de classes.
   * **Compatibilidade com Native AOT (.NET 8/9/10):** Permite compilação e inicialização ultrarrápidas em containers.

2. **Agrupamento Modular por Domínio (`src/Backend/CarWashSaaS.Api/Endpoints/`):**
   Os endpoints foram desacoplados de `Program.cs` e organizados em classes estáticas de extensão que implementam `IEndpointRouteBuilder`, correspondendo diretamente aos 4 módulos de domínio da aplicação:

   | Classe de Endpoints | Módulo de Domínio | Prefixos de Rota / Recursos |
   | :--- | :--- | :--- |
   | `IdentityEndpoints` | `CarWashSaaS.Identity` | `/auth/login`, `/auth/refresh`, `/auth/revoke`, `/auth/me`, `/identity/users` |
   | `TenantEndpoints` | `CarWashSaaS.Tenants` | `/tenants/profile`, `/tenants/profile/logo`, `/tenants/profile/logo/{fileName}` |
   | `WhatsAppEndpoints` | `CarWashSaaS.WhatsApp` | `/whatsapp/status`, `/whatsapp/pairing/start`, `/whatsapp/pairing/refresh`, `/whatsapp/webhooks/evolution` |
   | `YardOperationsEndpoints` | `CarWashSaaS.YardOperations` | `/services/*`, `/customers/*`, `/yard/capacity`, `/team-members/*`, `/commission-rules/*` |

3. **Papel Estrito de `Program.cs` (Composition Root):**
   * O `Program.cs` passa a atuar estritamente como a raiz de composição do host:
     * Carregamento de configuração e `.env`.
     * Registro de serviços no container de DI.
     * Configuração do pipeline de middlewares (`HttpsRedirection`, `Cors`, `Authentication`, `TenantResolverMiddleware`, `Authorization`).
     * Mapeamento de rotas de infraestrutura (`/health`) e chamada declarativa dos módulos de endpoints:
       ```csharp
       app.MapIdentityEndpoints();
       app.MapWhatsAppEndpoints();
       app.MapTenantEndpoints();
       app.MapYardOperationsEndpoints();
       ```

## Consequências e Controles

* **Manutenibilidade:** `Program.cs` foi reduzido de ~800 linhas para menos de 190 linhas.
* **Coesão:** Cada arquivo em `Endpoints/` lida unicamente com os contratos e endpoints do seu domínio.
* **Governança:** Novos endpoints devem ser adicionados na classe de extensão do domínio correspondente (ou em sub-classes dedicadas caso o módulo cresça muito), sendo expressamente proibido adicionar handlers de domínio diretamente no `Program.cs`.
* **Zero quebra de contrato:** Todos os contratos de rotas, políticas de autorização (`RequireAuthorization`) e status codes HTTP (`Result<T>`) foram preservados identicamente.
