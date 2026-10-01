# Lavaway

SaaS para gestão de lava-jatos e estética automotiva. O projeto está sendo construído como um monólito modular em .NET 10, com módulos independentes e SQL Server.

## Estado atual

A base da subfase 1.1 está implementada: solution .NET, módulos `Tenants`, `Identity` e `YardOperations`, modelo inicial de domínio, contextos EF Core, migrations e testes automatizados. A API expõe atualmente apenas `/health`; os fluxos de negócio e a interface Blazor ainda fazem parte das próximas etapas do roadmap.

**Atenção:** filtros globais de tenant e validação do `TenantId` nas gravações ainda não foram implementados. Não exponha nem use operações com dados de tenants até concluir a subfase 1.2. As migrations foram geradas e os scripts SQL foram validados, mas ainda precisam ser aplicados a uma instância SQL Server.

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
- `dotnet-ef` 10.0.9 para criar, inspecionar ou aplicar migrations. Caso ainda não esteja instalado:

```sh
dotnet tool install --global dotnet-ef --version 10.0.9
```

## Build e execução

Na raiz do repositório:

```sh
dotnet restore CarWashSaaS.sln
dotnet build CarWashSaaS.sln
dotnet test CarWashSaaS.sln
dotnet run --project src/Backend/CarWashSaaS.Api
```

O host oferece `GET /health` e o documento OpenAPI em ambiente de desenvolvimento. Use a URL exibida pelo `dotnet run` para acessar esses endpoints.

## Banco de dados e migrations

A API lê a connection string `ConnectionStrings:CarWashSaaS`. Para execução local, ela pode ser configurada pela variável de ambiente `ConnectionStrings__CarWashSaaS`. O arquivo `appsettings.Development.json` contém um exemplo com autenticação integrada; ajuste a configuração ao seu SQL Server e não versione credenciais.

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

Os testes de integração atuais validam os metadados dos modelos EF sem exigir uma conexão SQL Server. A verificação real de isolamento entre Tenant A e Tenant B depende da implementação dos filtros e da resolução do tenant na subfase 1.2.

## Documentação

- [Roadmap de implementação](roadmap.md)
- [Diretrizes de arquitetura e desenvolvimento](agents.md)
- [Modelo de domínio e dados da fase 1.1](docs/living-docs/modelo-dominio-dados-1.1.md)
- [ADR-0001: isolamento de tenant com EF Core](docs/architecture/ADR-0001-isolamento-tenant-ef-core.md)
