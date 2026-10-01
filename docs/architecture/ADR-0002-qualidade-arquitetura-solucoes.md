# ADR-0002: Qualidade e arquitetura da solução

- **Status:** Aceita para a primeira entrega
- **Data:** 2026-10-01

## Contexto

A fase 1 consolidou a base funcional do produto: autenticação, tenant, arquivos por tenant, fila em memória e modelo inicial de domínio. O projeto cresce em módulos separados, mas a estabilidade da arquitetura depende de regras explícitas para manter a separação de responsabilidades, evitar vazamento de tenant e preservar testes automatizados.

A regra 1.5 exige que a solução não apenas compile, mas também prove que a arquitetura está correta, cobrindo dependências entre camadas, proteção de multi-tenancy e uso do padrão `Result<T>`.

## Decisão

- O projeto usará a estrutura monolito modular com separação clara de responsabilidades por módulo: `Domain`, `Application`, `Infrastructure` e `Presentation`.
- Regras de negócio continuam em domínio e aplicação; infraestrutura apenas implementa contratos externos e persistência.
- O padrão `Result<T>` será o retorno obrigatório para validações e regras de negócio, evitando exceções para fluxo normal.
- `TenantId` continuará obrigatório para entidades operacionais, com filtros globais em `DbContext` e validação de gravação em `SaveChangesAsync`.
- A API continuará validando `Authentication:Authority` e `Authentication:Audience` em startup; sem esses valores a aplicação não inicia.
- O middleware de tenant continuará resolvendo o contexto a partir do JWT validado e nunca a partir de header ou query string.
- A suíte de qualidade deve incluir testes de integração e testes de arquitetura para impedir regressões de acoplamento, cross-tenant e validações incompletas.

## Consequências e controles

- Qualquer mudança em domínio, contracts ou configuração precisa ser acompanhada por teste de regressão e atualização da documentação.
- O crescimento dos módulos não pode introduzir acoplamento direto entre `DbContext` e outras camadas fora da infraestrutura do módulo.
- O projeto deve manter a base pronta para integração com a próxima fase de onboarding e operação sem quebrar as regras de isolamento.
- O estado atual já está consistente com o padrão: `Program.cs`, `TenantResolverMiddleware`, `CurrentTenantAccessor`, `TenantIsolationExtensions` e os testes de integração reforçam a decisão.
- A etapa de arquitetura deve ser concluída com a criação de testes automatizados que valem como guarda de regressão para futuro desenvolvimento.

## Alternativas consideradas

- **Liberar a fase sem testes arquiteturais:** rejeitado porque a base atual é modular, mas ainda depende de mecanismos automatizados para impedir que futuras mudanças quebrem a separação de responsabilidades.
- **Centralizar regra de negócio em controllers e DbContext:** rejeitado por violar a arquitetura hexagonal e o isolamento multi-tenant.
- **Usar filtros apenas por convenção:** rejeitado por não oferecer garantia automática e por depender da disciplina manual de cada equipe.
