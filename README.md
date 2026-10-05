# Lavaway

SaaS para gestão de lava-jatos e estética automotiva, construído como um monólito modular em **.NET 10**, com módulos independentes, **SQL Server** (schemas isolados por módulo), **MinIO** (Object Storage S3 com isolamento por tenant), **RabbitMQ** (mensageria e filas assíncronas) e frontend em **Blazor WebAssembly**.

---

## Sumário

- [Visão Geral e Arquitetura](#visão-geral-e-arquitetura)
- [Stack Tecnológica](#stack-tecnológica)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [Pré-requisitos](#pré-requisitos)
- [Inicialização Completa do Projeto](#inicialização-completa-do-projeto)
  - [1. Configurar o arquivo de ambiente (.env)](#1-configurar-o-arquivo-de-ambiente-env)
  - [2. Subir os serviços de infraestrutura (Docker)](#2-subir-os-serviços-de-infraestrutura-docker)
  - [3. Aplicar as migrações no banco de dados](#3-aplicar-as-migrações-no-banco-de-dados)
  - [4. Executar a API Backend](#4-executar-a-api-backend)
  - [5. Executar o Frontend Blazor](#5-executar-o-frontend-blazor)
- [Painéis e URLs dos Serviços](#painéis-e-urls-dos-serviços)
- [Testes e Garantia de Qualidade](#testes-e-garantia-de-qualidade)
- [Comandos Úteis de Desenvolvimento](#comandos-úteis-de-desenvolvimento)
- [Documentação Adicional](#documentação-adicional)

---

## Visão Geral e Arquitetura

O Lavaway adota uma arquitetura de **Monólito Modular** orientada a Domain-Driven Design (DDD):
- Cada módulo possui suas próprias camadas (`Domain`, `Application`, `Infrastructure`) e gerencia seu próprio schema no SQL Server (`tenants`, `identity`, `whatsapp`, `yard`).
- Isolamento multi-tenant garantido em tempo de execução via `CurrentTenantAccessor`, query filters no EF Core e validação de `TenantId` em gravações ([ADR-0001](docs/architecture/ADR-0001-isolamento-tenant-ef-core.md)).
- Endpoints HTTP expostos como **Minimal APIs modulares** em `src/Backend/CarWashSaaS.Api/Endpoints/` ([ADR-0006](docs/architecture/ADR-0006-organizacao-minimal-apis-modulares.md)).
- Armazenamento privado via MinIO com separação lógica por tenant e mensageria distribuída com RabbitMQ ([ADR-0004](docs/architecture/ADR-0004-storage-fila-self-hosted.md)).

---

## Stack Tecnológica

- **Backend:** .NET 10, C# 13, ASP.NET Core Minimal APIs, Entity Framework Core 10, ASP.NET Core Identity.
- **Frontend:** Blazor WebAssembly (.NET 10), Razor Components, autenticação com Bearer Tokens JWT.
- **Banco de Dados:** Microsoft SQL Server 2022 (Developer Edition).
- **Object Storage:** MinIO (compatível com AWS S3).
- **Mensageria:** RabbitMQ 4 com interface de gerenciamento.
- **Testes:** xUnit, FluentAssertions, Testcontainers (SQL Server e RabbitMQ), NetArchTest, bUnit.

---

## Estrutura do Repositório

```text
src/
  Shared/
    CarWashSaaS.Shared.Contracts/        Contratos compartilhados, DTOs e Result pattern
    CarWashSaaS.Shared.Configuration/    Configurações comuns e resolução de .env
  Backend/
    CarWashSaaS.Api/                    Host principal, Minimal APIs, middlewares e workers
      Endpoints/                         Minimal APIs agrupadas por módulo (Identity, Tenants, Yard, WhatsApp)
      Middleware/                        TenantResolverMiddleware e pipelines HTTP
      Services/                          Serviços de aplicação (JWT, Worker de background)
    Modules/
      Tenants/                           Módulo de Tenants, perfil da loja e branding
      Identity/                          Módulo de Identity, autenticação, usuários e papéis
      YardOperations/                    Módulo de Operações: clientes, veículos, pátio e serviços
      WhatsApp/                          Módulo de conexão, pareamento e webhooks WhatsApp
  Frontend/
    CarWashSaaS.Client.Core/             Clients de API HTTP e modelos do cliente
    CarWashSaaS.Client.Components/       Componentes Razor reutilizáveis de interface
    CarWashSaaS.Client.Web/              Aplicação Blazor WebAssembly (SPA)
tests/
  Backend/
    UnitTests/                          Testes unitários e invariantes de domínio
    IntegrationTests/                   Testes de integração com Testcontainers (SQL, RabbitMQ)
    ArchitectureTests/                  Testes de arquitetura e fronteiras entre camadas (NetArchTest)
  Frontend/
    ComponentTests/                     Testes de componentes Blazor (bUnit)
scripts/
  apply-migrations.sh                   Script para aplicar migrations em todos os módulos
  verify-quality.sh                     Script de verificação de formatação, build e testes
docs/
  architecture/                         ADRs (Architectural Decision Records)
  living-docs/                          Documentação viva de fluxos de negócio
```

---

## Pré-requisitos

Antes de iniciar, certifique-se de ter instalado no seu ambiente:

1. **.NET 10 SDK** (versão 10.0 ou superior):
   ```sh
   dotnet --version
   ```
2. **Docker e Docker Compose** (para serviços locais de infraestrutura e Testcontainers):
   ```sh
   docker --version
   docker compose version
   ```
3. **Ferramenta de Linha de Comando do EF Core (`dotnet-ef`)**:
   ```sh
   dotnet tool install --global dotnet-ef --version 10.0.9
   # Se já estiver instalado, garanta que esteja atualizado:
   dotnet tool update --global dotnet-ef
   ```

---

## Inicialização Completa do Projeto

Siga os 5 passos abaixo para colocar toda a solução em funcionamento a partir de um checkout limpo.

### 1. Configurar o arquivo de ambiente (`.env`)

Na raiz do repositório, copie o modelo `.env.example` para `.env`:

```sh
cp .env.example .env
```

O arquivo `.env` já vem pré-configurado com as credenciais padrões de desenvolvimento local:
- String de conexão para o SQL Server local.
- Credenciais e portas para MinIO e RabbitMQ.
- Chave de assinatura e emissores para JWT (`Authentication__*`).
- **Integração WhatsApp (Evolution API)**:
  - `EVOLUTION_API_KEY`: Chave mestre de autenticação da Evolution API (`CHANGE_ME`).
  - `EVOLUTION_API_PORT`: Porta HTTP exposta do container (`8080`).
  - `WhatsApp__EvolutionApi__BaseUrl`: URL consumida pela API backend (`http://localhost:8080/`).
  - `WhatsApp__EvolutionApi__ApiKey`: Chave enviada no cabeçalho `apikey` (`CHANGE_ME`).
  - `WhatsApp__EvolutionApi__InstanceNamePrefix`: Prefixo identificador das instâncias por tenant (`lavaway`).
  - `WhatsApp__EvolutionApi__WebhookSecret`: Segredo para validação de callbacks via `X-Webhook-Secret` (`LavawayEvolutionWebhookSecretDev2026!`).

> [!NOTE]
> Mantenha a senha do SQL Server consistente entre `MSSQL_SA_PASSWORD` e o parâmetro `Password` da variável `ConnectionStrings__CarWashSaaS`. Garanta também que `WhatsApp__EvolutionApi__ApiKey` coincida com `EVOLUTION_API_KEY`.

---

### 2. Subir os serviços de infraestrutura (Docker)

Inicie os containers do SQL Server 2022, MinIO, RabbitMQ e Evolution API:

```sh
docker compose up -d
```

Acompanhe o status e garanta que todos os serviços fiquem saudáveis (`healthy` ou `running`):

```sh
docker compose ps
```

---

### 3. Aplicar as migrações no banco de dados

Com o container do SQL Server em execução, aplique as migrações de todos os módulos.

#### Opção A: Via script automatizado (recomendado)

```sh
bash scripts/apply-migrations.sh
```

#### Opção B: Manualmente módulo a módulo via `dotnet ef`

Caso prefira executar cada contexto individualmente:

```sh
# 1. Tenants (schema 'tenants')
dotnet ef database update \
  --project src/Backend/Modules/Tenants/CarWashSaaS.Tenants.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context TenantsDbContext

# 2. Identity (schema 'identity')
dotnet ef database update \
  --project src/Backend/Modules/Identity/CarWashSaaS.Identity.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context IdentityModuleDbContext

# 3. Yard Operations (schema 'yard')
dotnet ef database update \
  --project src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context YardOperationsDbContext

# 4. WhatsApp (schema 'whatsapp')
dotnet ef database update \
  --project src/Backend/Modules/WhatsApp/CarWashSaaS.WhatsApp.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context WhatsAppDbContext
```

---

### 4. Executar a API Backend

Abra um terminal na raiz do projeto e execute o host da API:

```sh
dotnet run --project src/Backend/CarWashSaaS.Api
```

- A API estará disponível em: **`http://localhost:5225`** (e HTTPS em `https://localhost:7035`).
- Verifique a saúde do serviço:
  ```sh
  curl http://localhost:5225/health
  # Retorno esperado: {"status":"ok"}
  ```
- O documento OpenAPI pode ser consultado em: `http://localhost:5225/openapi/v1.json`.

---

### 5. Executar o Frontend Blazor

Em outro terminal na raiz do projeto, execute o frontend WebAssembly:

```sh
dotnet run --project src/Frontend/CarWashSaaS.Client.Web
```

- O aplicativo Web abrirá automaticamente no navegador ou estará acessível em: **`http://localhost:5199`** (e HTTPS em `https://localhost:7288`).
- O frontend já está configurado por padrão em `wwwroot/appsettings.json` para consumir a API em `http://localhost:5225/`.

---

## Painéis e URLs dos Serviços

| Serviço | Descrição | Endereço / URL | Credenciais Padrão (dev) |
|---|---|---|---|
| **Frontend Web** | Blazor WebAssembly SPA | `http://localhost:5199` | *(acesso web)* |
| **Backend API** | ASP.NET Core Minimal APIs | `http://localhost:5225` | Bearer JWT / Basic |
| **API Health Check** | Endpoint de verificação | `http://localhost:5225/health` | Anônimo |
| **MinIO Console** | Painel Web de Storage S3 | `http://localhost:9001` | Usuário: `lavaway`<br>Senha: `LavawayMinioDev2026` |
| **MinIO S3 API** | Endpoint de API S3 | `http://localhost:9000` | Idem |
| **RabbitMQ Dashboard** | Painel de filas e mensageria | `http://localhost:15672` | Usuário: `lavaway`<br>Senha: `LavawayRabbitDev2026` |
| **RabbitMQ AMQP** | Porta do broker de mensagens | `localhost:5672` | Idem |
| **SQL Server 2022** | Banco de dados relacional | `localhost:1433` | Usuário: `sa`<br>Senha: `LavawaySqlDev2026!` |
| **Evolution API** | API WhatsApp / Provedor | `http://localhost:8080` | Header `apikey`: `CHANGE_ME` |
| **Evolution Manager** | Painel Web de Instâncias | `http://localhost:8080/manager` | Token / Chave: `CHANGE_ME` |

---

## Testes e Garantia de Qualidade

### Executar todos os testes da solução

```sh
dotnet test CarWashSaaS.sln
```

> [!NOTE]
> Os testes unitários e de arquitetura executam diretamente em memória. Os testes de integração utilizam **Testcontainers** para provisionar instâncias efêmeras de SQL Server e RabbitMQ, exigindo o Docker em execução.

### Executar a verificação completa de qualidade

O script `scripts/verify-quality.sh` executa todas as etapas do pipeline de CI:
1. Validação de formatação (`dotnet format --verify-no-changes`).
2. Build da solução em modo `Release`.
3. Execução dos testes de arquitetura com NetArchTest.
4. Execução dos testes unitários e de domínio.

```sh
bash scripts/verify-quality.sh
```

### Executar testes por categoria específica

- **Testes Unitários:**
  ```sh
  dotnet test tests/Backend/UnitTests/CarWashSaaS.UnitTests/
  ```
- **Testes de Arquitetura:**
  ```sh
  dotnet test tests/Backend/ArchitectureTests/CarWashSaaS.ArchitectureTests/
  ```
- **Testes de Integração:**
  ```sh
  dotnet test tests/Backend/IntegrationTests/CarWashSaaS.IntegrationTests/
  ```
- **Testes de Componentes Frontend (bUnit):**
  ```sh
  dotnet test tests/Frontend/ComponentTests/CarWashSaaS.ComponentTests/
  ```

---

## Comandos Úteis de Desenvolvimento

### Gerenciar os containers locais

- **Pausar containers mantendo os dados:**
  ```sh
  docker compose stop
  ```
- **Retomar containers pausados:**
  ```sh
  docker compose start
  ```
- **Derrubar containers mantendo os volumes persistidos:**
  ```sh
  docker compose down
  ```
- **Limpar completamente os containers e volumes (reset total do banco e storage):**
  ```sh
  docker compose down -v
  ```

### Criar uma nova migração do EF Core

Para adicionar uma migration em um módulo específico, informe o projeto de infraestrutura do módulo e seu respectivo `DbContext`:

```sh
# Exemplo para o módulo YardOperations:
dotnet ef migrations add <NomeDaMigration> \
  --project src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context YardOperationsDbContext
```

---

## Documentação Adicional

- [Roadmap de Implementação](roadmap.md)
- [Diretrizes de Arquitetura e Regras de Desenvolvimento](agents.md)
- [ADR-0001: Isolamento de Tenant com EF Core](docs/architecture/ADR-0001-isolamento-tenant-ef-core.md)
- [ADR-0004: Storage Privado e Fila Self-Hosted](docs/architecture/ADR-0004-storage-fila-self-hosted.md)
- [ADR-0006: Organização das Minimal APIs Modulares](docs/architecture/ADR-0006-organizacao-minimal-apis-modulares.md)
- [Modelo de Domínio e Dados da Fase 1.1](docs/living-docs/modelo-dominio-dados-1.1.md)
- [Cadastro e Perfil do Estabelecimento — Fase 2.1](docs/living-docs/cadastro-perfil-estabelecimento-2.1.md)
