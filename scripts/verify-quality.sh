#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

echo "========================================================"
echo "  CarWashSaaS — Verificação de Qualidade e Arquitetura   "
echo "========================================================"

echo ""
echo "==> [1/4] Verificando formatação de código..."
dotnet format --verify-no-changes --verbosity diagnostic || {
    echo "AVISO: Código com inconsistências de formatação detectadas."
    echo "Execute 'dotnet format' para ajustar automaticamente."
}

echo ""
echo "==> [2/4] Compilando a solução (Release)..."
dotnet build CarWashSaaS.sln -c Release --no-incremental

echo ""
echo "==> [3/4] Executando testes de arquitetura (NetArchTest)..."
dotnet test tests/Backend/ArchitectureTests/CarWashSaaS.ArchitectureTests/CarWashSaaS.ArchitectureTests.csproj \
    -c Release --no-build --logger "console;verbosity=normal"

echo ""
echo "==> [4/4] Executando testes unitários (TDD / Domínio / Aplicação)..."
dotnet test tests/Backend/UnitTests/CarWashSaaS.UnitTests/CarWashSaaS.UnitTests.csproj \
    -c Release --no-build --logger "console;verbosity=normal"

echo ""
echo "========================================================"
echo "  SUCESSO: Todos os gates de qualidade foram aprovados! "
echo "========================================================"
