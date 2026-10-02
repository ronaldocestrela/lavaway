#!/usr/bin/env bash
set -euo pipefail

echo "==> Aplicando migrations de Tenants..."
dotnet ef database update \
  --project src/Backend/Modules/Tenants/CarWashSaaS.Tenants.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context TenantsDbContext

echo "==> Aplicando migrations de Identity..."
dotnet ef database update \
  --project src/Backend/Modules/Identity/CarWashSaaS.Identity.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context IdentityModuleDbContext

echo "==> Aplicando migrations de YardOperations..."
dotnet ef database update \
  --project src/Backend/Modules/YardOperations/CarWashSaaS.YardOperations.Infrastructure \
  --startup-project src/Backend/CarWashSaaS.Api \
  --context YardOperationsDbContext

echo "==> Todas as migrations foram aplicadas com sucesso!"
