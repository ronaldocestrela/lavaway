# ADR-0026: Mitigação de Vulnerabilidades Transitivas em Dependências NuGet (System.Security.Cryptography.Pkcs)

## Status
Aceito

## Contexto
Durante o processo de build contínuo e validação de segurança no .NET 10, o NuGet Audit identificou duas vulnerabilidades conhecidas no pacote `System.Security.Cryptography.Pkcs` na versão `9.0.4`:
- **NU1902 (Severidade Moderada):** [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
- **NU1903 (Severidade Alta):** [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) / CVE-2026-84008 - vulnerabilidade de Denial of Service (DoS) por consumo quadrático de CPU em `SignedCms.CheckSignature` ao processar dados ASN.1/X.509 forjados por atacante.

O pacote vulnerável não foi referenciado diretamente pelo código da solução, mas inserido de forma **transitiva** como dependência indireta do `Microsoft.Data.SqlClient` (consumido por `Microsoft.EntityFrameworkCore.SqlServer` e `Microsoft.AspNetCore.Identity.EntityFrameworkCore`).

Conforme as diretrizes de governança e segurança do `agents.md`, vulnerabilidades de dependências devem ser tratadas de forma proativa para garantir um pipeline de CI/CD estável e seguro, sem poluir camadas de domínio com acoplamentos desnecessários.

## Decisão de Arquitetura

### 1. Pinning Explícito da Versão Segura
Para sanar imediatamente as vulnerabilidades sem aguardar um ciclo de release do pacote upstream (`Microsoft.EntityFrameworkCore.SqlServer`), fixou-se a versão segura oficial do pacote `System.Security.Cryptography.Pkcs` para **`10.0.13`** (ou superior) no ecossistema .NET 10:
- Projetos atualizados com `<PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />`:
  - `src/Backend/CarWashSaaS.Api/CarWashSaaS.Api.csproj` (Host da API)
  - `tests/Backend/IntegrationTests/CarWashSaaS.IntegrationTests/CarWashSaaS.IntegrationTests.csproj` (Testes de integração)
  - `tests/Backend/ArchitectureTests/CarWashSaaS.ArchitectureTests/CarWashSaaS.ArchitectureTests.csproj` (Testes de arquitetura)

### 2. Preservação do Isolamento Hexagonal das Camadas de Domínio
- Conforme a Seção 2.2 do `agents.md`, os módulos de **Domínio** permanecem com **zero dependências externas**.
- O pacote de criptografia transitiva foi fixado exclusivamente nas camadas de Host/Infraestrutura e Projetos de Teste onde o EF Core / SqlClient é consumido.
- A integridade dos limites de camadas continua 100% resguardada e validada pelas regras do `NetArchTest`.

### 3. Eliminação de Warnings de Compilação
- Em paralelo, o warning de compilação `CS9113` identificado em `TenantPaymentGatewayApplicationService` foi sanado mediante o uso efetivo do `ILogger<TenantPaymentGatewayApplicationService>` injetado no construtor primário, garantindo registros estruturados com `TenantId` em todos os fluxos de consulta, salvamento e teste de conectividade com gateways de pagamento (sem exposição de segredos ou tokens).

## Consequências

### Positivas
- Resolução completa dos avisos de segurança `NU1902` e `NU1903`, elevando a postura de segurança da aplicação para produção.
- Mitigação do vetor de ataque DoS CVE-2026-84008.
- Manutenção de um build 100% limpo com zero warnings e zero errors no .NET 10.
- Preservação da conformidade com as regras arquiteturais do `agents.md`.

### Mitigações
- Monitoramento de futuros releases de pacotes do Entity Framework Core 10 e SqlClient para sincronizar atualizações upstream quando as novas versões referenciarem nativamente o pacote seguro.
