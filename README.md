# Lavaway

SaaS para gestão de lava-jatos e estética automotiva. O projeto está sendo construído como um monólito modular em .NET 10, com módulos independentes e SQL Server.

## Estado atual

A base das subfases 1.1 e 1.2 está implementada. A subfase 1.4 já possui storage privado MinIO com namespace por tenant, fila persistente RabbitMQ e um worker genérico; ainda faltam execução dos testes com os serviços reais e handlers de negócio.

A API expõe `/health`. O build da API e os testes unitários focados em storage/fila passaram. Os testes de integração RabbitMQ e SQL Server usam Testcontainers e precisam de Docker acessível; a integração com storage MinIO ainda precisa de validação ponta a ponta.

**Observação:** as regras de tenant e a validação do `TenantId` nas gravações já estão implementadas no shared configuration e nos contextos EF Core. A integração real com SQL Server continua exigindo Docker/Testcontainers para executar os testes de isolamento em ambientes locais sem serviço SQL dedicado.

## Stack

- .NET 10 e C# com nullable reference types.
- ASP.NET Core Web API.
- Entity Framework Core 10 e SQL Server.
- ASP.NET Core Identity com chaves `Guid`.
- xUnit para testes unitários, de modelo e de arquitetura.

## Estrutura

```text
src/
  Shared/CarWashSaaS.Shared.Contracts/       Contratos compartilhados e Result
  Backend/CarWashSaaS.Api/                   Host e composição dos módulos
  Backend/Modules/Tenants/                   Agregado Tenant e persistência
  Backend/Modules/Identity/                  Identity, usuários e papéis
  Backend/Modules/YardOperations/             Clientes, veículos, serviços e OS
tests/Backend/
  UnitTests/                                  Invariantes do domínio
  IntegrationTests/                           Modelo relacional dos contextos
  ArchitectureTests/                          Fronteiras entre camadas e módulos
docs/
  architecture/                               ADRs
  living-docs/                                Modelo e documentação viva
```

Cada módulo mantém seus próprios contextos e migrations. Todos usam o mesmo banco SQL Server, em schemas separados: `tenants`, `identity` e `yard`.

## Pré-requisitos

- .NET SDK 10.0.
- SQL Server para aplicar e executar migrations localmente.
- MinIO e RabbitMQ para executar a API. Configure endpoint, bucket e credenciais do MinIO e a URI AMQP(S) do RabbitMQ por variáveis de ambiente; `.env.example` contém os nomes esperados.
- Docker acessível para executar os testes de integração baseados em Testcontainers.
- Um arquivo `.env` na raiz do repositório com a connection string local. Use `.env.example` como referência e mantenha credenciais reais fora do Git.
- `dotnet-ef` 10.0.9 para criar, inspecionar ou aplicar migrations. Caso ainda não esteja instalado:

```sh
dotnet tool install --global dotnet-ef --version 10.0.9
```

## Dependências locais

O `compose.yaml` inicia SQL Server 2022, MinIO e RabbitMQ com volumes persistentes. Copie `.env.example` para `.env` em um checkout novo e mantenha a senha do SQL Server igual na variável `MSSQL_SA_PASSWORD` e na connection string. Os valores de exemplo são apenas para desenvolvimento local.

```sh
docker compose up -d
docker compose ps
```

As portas são publicadas somente em `127.0.0.1`. O console do MinIO fica em `http://localhost:9001` e o painel do RabbitMQ em `http://localhost:15672`; use as credenciais de desenvolvimento do `.env`. O bucket privado é criado pelo adapter no primeiro acesso.

Execute a API no host com `dotnet run --project src/Backend/CarWashSaaS.Api`. O gateway OIDC não está no Compose: configure `Authentication__Authority` e `Authentication__Audience` para um issuer de desenvolvimento. Evolution API também permanece externa e só é necessária para testar o pareamento do WhatsApp. `docker compose down` preserva os dados; `docker compose down -v` remove os volumes.

## Build e execução

Na raiz do repositório:

```sh
dotnet restore CarWashSaaS.sln
dotnet build CarWashSaaS.sln
dotnet test CarWashSaaS.sln
dotnet run --project src/Backend/CarWashSaaS.Api
```

O host exige `Authentication:Authority`, `Authentication:Audience`, configuração do MinIO e URI válida do RabbitMQ. Ele oferece `GET /health` e o documento OpenAPI em ambiente de desenvolvimento. Use a URL exibida pelo `dotnet run` para acessar esses endpoints.

## Banco de dados e migrations

A API e as factories do EF carregam `.env` da raiz da solution. A variável `ConnectionStrings__CarWashSaaS` também pode ser fornecida diretamente pelo shell ou ambiente de execução; variáveis já definidas têm precedência sobre `.env`. Em produção, injete a connection string pelo ambiente/secret store e não use arquivo `.env`.

Com a connection string configurada, aplique as migrations na ordem abaixo:

```sh
dotnet ef database update --project src/Backend/Modules/Tenants/CarWashSaaS.Tenants.Infrastructure --startup-project src/Backend/CarWashSaaS.Api --context TenantsDbContext
dotnet ef database update --project src/Backend/Modules/Identity/CarWashSaaS.Identity.Infrastructure --startup-project src/Backend/CarWashSaaS.Api --context IdentityModuleDbContext
dotnet ef database update --project src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Infrastructure --startup-project src/Backend/CarWashSaaS.Api --context YardOperationsDbContext
```

Cada módulo mantém sua própria tabela de histórico de migrations. As migrations podem ser geradas por contexto com `dotnet ef migrations add <Nome>`, usando o mesmo `--project`, `--startup-project` e `--context` correspondentes.

## Testes

```sh
dotnet test CarWashSaaS.sln
```

Os testes unitários podem ser executados sem serviços externos. Os testes de integração de isolamento aplicam migrations em SQL Server via Testcontainers; os testes da fila iniciam RabbitMQ via Testcontainers. Ambos exigem Docker acessível. Para executar apenas os testes da fila:

```sh
dotnet test tests/Backend/IntegrationTests/CarWashSaaS.IntegrationTests/CarWashSaaS.IntegrationTests.csproj --filter FullyQualifiedName~RabbitMqBackgroundQueueIntegrationTests
```

## Documentação

- [Roadmap de implementação](roadmap.md)
- [Diretrizes de arquitetura e desenvolvimento](agents.md)
- [Modelo de domínio e dados da fase 1.1](docs/living-docs/modelo-dominio-dados-1.1.md)
- [ADR-0004: storage e fila self-hosted](docs/architecture/ADR-0004-storage-fila-self-hosted.md)
- [Cadastro e perfil do estabelecimento — fase 2.1](docs/living-docs/cadastro-perfil-estabelecimento-2.1.md)
- [ADR-0001: isolamento de tenant com EF Core](docs/architecture/ADR-0001-isolamento-tenant-ef-core.md)
