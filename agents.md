# AGENT GUIDELINES & SPECIFICATION MANUAL: AGENT.MD

Este documento define as regras inegociáveis, convenções arquiteturais, diretrizes de design de software e processos operacionais que qualquer agente autônomo ou desenvolvedor humano **DEVE** seguir estritamente ao criar, evoluir, refatorar ou testar o código deste SaaS para Lava-Jato e Estética Automotiva.

---

## 1. VISÃO GERAL & STACK TECNOLÓGICA

A solução adota uma topologia de **Monolito Modular** distribuída em uma única Solution (`.sln`), contendo projetos estritamente separados para Backend e Frontend.

### 1.1 Backend
* **Runtime / Framework:** .NET 10 (C# moderno, nullable reference types habilitados, file-scoped namespaces).
* **Persistência de Dados:** Microsoft SQL Server com Entity Framework Core 10 (EF Core).
* **Autenticação & Autorização:** ASP.NET Core Identity Framework integrado ao EF Core com tokens/cookies e isolamento por Tenant.
* **Padrões de Comunicação:** Padrão Result (`Result<T>`) para fluxos de negócio sem propagação indevida de exceções.
* **Design Arquitetural:** Arquitetura Hexagonal (Ports & Adapters) combinada a princípios DDD (Domain-Driven Design).
* **Identificadores Únicos:** Chaves primárias e relacionais no formato **UUID (Guid v4 ou Guid v7 sequencial)**.

### 1.2 Frontend
* **Framework:** Blazor (WebAssembly ou Blazor Web App no .NET 10, componentes isolados, Tailwind CSS ou scoped styles).
* **Comunicação:** HTTP Clients fortemente tipados consumindo os endpoints do Backend via contratos compartilhados (`Contracts/DTOs`).
* **Estado:** State Containers centralizados com isolamento por sessão.

### 1.3 Engenharia de Qualidade
* **Metodologia:** Test-Driven Development (TDD) estrito (Red-Green-Refactor).
* **Documentação:** **Documentação Viva inegociável** (Living Documentation baseada em especificações executáveis, Mermaid.js e Markdown versionado junto ao código).

---

## 2. ARQUITETURA & PRINCÍPIOS FUNDAMENTAIS

### 2.1 Monolito Modular
* O sistema é estruturado em **módulos verticais autônomos** (ex.: `Identity`, `Tenants`, `YardOperations`, `Billing`, `Messaging/WhatsApp`, `Scheduling`).
* **Comunicação Inter-Módulos:**
  * Módulos **NÃO** podem referenciar diretamente DbContexts, tabelas ou repositórios de outros módulos.
  * A comunicação síncrona ocorre exclusivamente por meio de Interfaces Públicas expostas pelo módulo (Contracts).
  * A comunicação assíncrona desacoplada ocorre através de Domain Events em memória (`MediatR` ou `In-Memory Event Bus`).

### 2.2 Arquitetura Hexagonal (Ports & Adapters)
Cada módulo deve refletir a seguinte divisão em camadas concêntricas:

1. **Domain (Núcleo Puro):**
   * Contém Entidades, Objetos de Valor (Value Objects), Eventos de Domínio e Regras de Negócio invariantes.
   * **Zero dependências externas** (sem referências a EF Core, ASP.NET, HTTP ou bibliotecas de terceiros).
2. **Application (Casos de Uso):**
   * Portas de Entrada (Use Cases, Commands, Queries) e Portas de Saída (Interfaces de Repositórios, Serviços Externos, Provedores de WhatsApp).
   * Orquestração de negócio e retorno obrigatório encapsulado no padrão `Result<T>`.
3. **Adapters / Infrastructure (Mundo Externo):**
   * Implementação concreta das portas de saída: Repositórios EF Core, Configurações de Mapeamento, Provedores de SMS/WhatsApp, Integrações de Pagamento Pix.
4. **Adapters / Presentation (Entrada da Aplicação):**
   * Controllers REST / Minimal APIs expondo contratos para o Frontend.

### 2.3 Princípios SOLID & DRY
* **S (Single Responsibility):** Cada classe, use case e componente Blazor deve ter um único motivo para mudar.
* **O (Open/Closed):** Extensões de comportamento devem ocorrer por herança ou injeção de dependência/estratégia, nunca modificando núcleos estáveis.
* **L (Liskov Substitution):** Subtipos devem ser substituíveis por suas interfaces base sem quebrar precondições.
* **I (Interface Segregation):** Crie interfaces pequenas e específicas para cada porta (Repository segregado por aggregate root).
* **D (Dependency Inversion):** Camadas internas dependem exclusivamente de abstrações; a infraestrutura implementa as interfaces do domínio/aplicação.
* **DRY (Don't Repeat Yourself):** Elimine duplicações de lógica de validação e regras de negócio. Reutilize contratos compartilhados entre Blazor e Backend.

---

## 3. MULTI-TENANCY: ISOLAMENTO LÓGICO COM BANCO ÚNICO

O sistema atende múltiplos lava-jatos sob um único banco SQL Server. A violação de tenant é classificada como **vulnerabilidade crítica gravíssima**.

### 3.1 Identificador de Tenant
* Todo registro proprietário de um estabelecimento deve implementar a interface `IMustHaveTenant`:
  ```csharp
  public interface IMustHaveTenant
  {
      Guid TenantId { get; set; }
  }
  ```

### 3.2 Resolução de Tenant
* O `TenantId` é resolvido no pipeline HTTP do ASP.NET Core via token JWT (claim `tenant_id`) ou cabeçalho seguro validado pelo middleware `TenantResolverMiddleware`.
* O serviço injetável `ICurrentTenantAccessor` provê o contexto atual para o Application Layer.

### 3.3 Query Filters Globais no EF Core
* O `DbContext` de cada módulo deve aplicar automaticamente Global Query Filters para todas as entidades que implementem `IMustHaveTenant`:
  ```csharp
  modelBuilder.Entity<VehicleEntry>()
      .HasQueryFilter(e => e.TenantId == _currentTenantAccessor.TenantId);
  ```
* **Gravação Segura:** O método `SaveChangesAsync` deve interceptar e injetar compulsoriamente o `TenantId` corrente nas entidades em estado `Added`, impossibilitando a persistência acidental de dados em outro tenant.

---

## 4. CONVENÇÃO DE IDENTIFICADORES: UUID / GUID

* **IDs de Entidades:** Toda entidade raiz possui identificador único do tipo `Guid` (UUID).
* **Estratégia de Banco:** No SQL Server, utilize mapeamento para `uniqueidentifier`.
* **Geração:** IDs devem ser gerados preferencialmente no Domain Model no momento da instanciação (`Guid.NewGuid()`), garantindo rastreabilidade antes do commit no banco.

---

## 5. PADRÃO RESULT & TRATAMENTO DE ERROS

* **Proibido usar Exceptions para Controle de Fluxo:** Exceções são reservadas apenas para falhas estruturais inesperadas (banco indisponível, falha de rede fatal).
* Toda operação de caso de uso e domínio deve retornar um encapsulador `Result` ou `Result<T>`:

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
}

public record Error(string Code, string Description, ErrorType Type);
```

* **Tradução no Controller:** A camada de API mapeia o `Result<T>` para o HTTP StatusCode correspondente:
  * `ErrorType.NotFound` $\rightarrow$ 404 Not Found
  * `ErrorType.Validation` $\rightarrow$ 400 Bad Request
  * `ErrorType.Conflict` $\rightarrow$ 409 Conflict
  * `ErrorType.Unauthorized` $\rightarrow$ 403 Forbidden
  * `Success(value)` $\rightarrow$ 200 OK / 201 Created

---

## 6. PADRÃO REPOSITORY & PERSISTÊNCIA

* **Repositórios por Aggregate Root:** Repositórios operam unicamente sobre raízes de agregação do domínio (`IRepository<TAggregateRoot>`).
* **Assinaturas Orientadas a Domínio:** Repositórios não expõem `IQueryable` para camadas superiores. As consultas retornam coleções de domínio ou DTOs de leitura dedicados:
  ```csharp
  public interface IOrderRepository
  {
      Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
      Task AddAsync(Order order, CancellationToken ct = default);
      void Update(Order order);
      void Remove(Order order);
  }
  ```
* **Transações:** O controle de persistência atômica é governado pelo padrão `IUnitOfWork`.

---

## 7. ESTRUTURA DA SOLUÇÃO (FOLDER STRUCTURE)

```text
/
├── CarWashSaaS.sln
├── docs/
│   ├── living-docs/                 # Documentação viva e diagramas executáveis
│   └── architecture/                # ADRs (Architecture Decision Records)
├── src/
│   ├── Shared/
│   │   └── CarWashSaaS.Shared.Contracts/   # DTOs, Enums e Results compartilhados
│   ├── Backend/
│   │   ├── CarWashSaaS.Api/                 # Host ASP.NET Core, Middlewares, DI
│   │   └── Modules/
│   │       ├── Identity/                    # Módulo de Autenticação & Identity
│   │       ├── YardOperations/             # Recepção, Kanban, Checklist
│   │       │   ├── Domain/
│   │       │   ├── Application/
│   │       │   └── Infrastructure/
│   │       ├── Billing/                     # Financeiro, Assinaturas, Pix
│   │       └── Messaging/                   # Integração WhatsApp & Webhooks
│   └── Frontend/
│       ├── CarWashSaaS.Client.Web/          # Projeto Blazor
│       ├── CarWashSaaS.Client.Components/   # Biblioteca de componentes UI reutilizáveis
│       └── CarWashSaaS.Client.Core/         # Serviços de estado e HTTP handlers
└── tests/
    ├── Backend/
    │   ├── UnitTests/                       # Testes de Domínio e Use Cases (TDD)
    │   ├── IntegrationTests/                # Testes de Repositório e EF Core (Multi-tenant)
    │   └── ArchitectureTests/               # NetArchTest validando Hexagonal e Isolamento
    └── Frontend/
        └── ComponentTests/                  # Bunit para testes de componentes Blazor
```

---

## 8. TEST-DRIVEN DEVELOPMENT (TDD) & TESTES DE ARQUITETURA

O desenvolvimento de qualquer funcionalidade deve obedecer obrigatoriamente ao ciclo TDD:
1. **Red:** Escreva um teste de unidade/integração que falhe expressando a intenção do requisito de negócio.
2. **Green:** Escreva o código mínimo necessário para fazer o teste passar.
3. **Refactor:** Limpe o código, elimine duplicações, mantenha nomes expressivos garantindo que a suíte continue verde.

### 8.1 Regras de Testes
* **Testes de Arquitetura (NetArchTest):** Devem existir testes automatizados validando que:
  * Camadas de Domínio não referenciam Infrastructure nem Presentation.
  * Módulos não acessam DbContexts de outros módulos.
  * Todas as entidades auditáveis possuem `TenantId` e `UUID`.
* **Testes de Multi-Tenant:** Toda suíte de integração deve testar explicitamente o vazamento de dados:
  * Criar dados no Tenant A.
  * Tentar consultar/alterar dados estando autenticado como Tenant B.
  * O teste DEVE validar que o Tenant B recebe `NotFound` ou resultado vazio.

---

## 9. DOCUMENTAÇÃO VIVA (LIVING DOCUMENTATION) - INEGOVIÁVEL

A documentação viva é um artefato obrigatório, evolutivo e versionado no mesmo commit do código-fonte:
1. **ADRs (Architecture Decision Records):** Qualquer mudança de tecnologia, padrão de integração ou regra de isolamento exige a criação de uma ADR em `/docs/architecture/ADR-xxxx.md`.
2. **Diagramas Mermaid no Código:** Todo Use Case principal ou agregado deve possuir um diagrama de fluxo ou estado embutido em seu arquivo Markdown descritivo ou cabeçalho do teste executável.
3. **BDD / Specs Executáveis:** Casos de uso complexos devem possuir descrições legíveis em Gherkin (`.feature`) ou testes nomeados como sentenças executáveis que descrevem a regra de negócio:
   * Exemplo: `Should_Reject_Vehicle_Checkin_When_Yard_Capacity_Is_Exceeded()`.

---

## 10. REGRAS DE CONDUTA PARA O AGENTE DE IA

Ao receber qualquer comando de código, o agente deve seguir o seguinte checklist mental:
* [ ] **Multi-tenant Check:** A query/comando inclui ou filtra o `TenantId`? Há risco de cross-tenant data leak?
* [ ] **UUID Check:** Os identificadores são `Guid`?
* [ ] **Result Pattern:** O método retorna `Result<T>` em vez de lançar exceções para fluxos de validação?
* [ ] **Hexagonal Integrity:** Estou colocando regras de domínio dentro de controllers ou DbContext? (Se sim, pare e mova para o Domain/Application).
* [ ] **TDD First:** Foi apresentado ou planejado o teste correspondente para esta funcionalidade?
* [ ] **Single Project Responsibility:** O código do Backend reside nos módulos da API e o código de tela reside nos projetos Blazor, usando apenas `Shared.Contracts` para dados comuns?
* [ ] **Living Docs Update:** O diagrama ou especificação foi atualizado junto à alteração?