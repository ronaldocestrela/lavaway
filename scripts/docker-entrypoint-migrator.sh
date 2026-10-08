#!/usr/bin/env bash
set -euo pipefail

echo "=========================================================="
echo "  Lavaway - Executando Migrations em Produção"
echo "=========================================================="

CONNECTION_STRING="${ConnectionStrings__CarWashSaaS:-${CONNECTION_STRING:-}}"

if [ -z "$CONNECTION_STRING" ]; then
  echo "ERRO: ConnectionStrings__CarWashSaaS não definida."
  exit 1
fi

echo "==> [1/5] Aplicando migrations de Tenants..."
./bundle-tenants --connection "$CONNECTION_STRING"

echo "==> [2/5] Aplicando migrations de Identity..."
./bundle-identity --connection "$CONNECTION_STRING"

echo "==> [3/5] Aplicando migrations de YardOperations..."
./bundle-yard --connection "$CONNECTION_STRING"

echo "==> [4/5] Aplicando migrations de WhatsApp..."
./bundle-whatsapp --connection "$CONNECTION_STRING"

echo "==> [5/5] Aplicando migrations de Billing..."
./bundle-billing --connection "$CONNECTION_STRING"

echo "==> Todas as migrations foram aplicadas com sucesso!"
