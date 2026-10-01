# ADR-0001: Isolamento de Tenant com EF Core

- **Status:** Aceita para a primeira entrega
- **Data:** 2026-09-30

## Contexto

O produto usa um único banco SQL Server para vários estabelecimentos. Uma consulta ou gravação que atravesse tenants é uma vulnerabilidade crítica. `agents.md` exige `IMustHaveTenant`, filtros globais por contexto e validação de `TenantId` ao salvar.

## Decisão

- Todos os registros operacionais pertencentes a um estabelecimento carregam `TenantId`; o agregado global `Tenant` e os papéis globais do Identity são exceções deliberadas.
- A subfase 1.1 cria as colunas, índices tenant-scoped e chaves estrangeiras compostas compatíveis com o isolamento.
- A subfase 1.2 deve aplicar Global Query Filters a todas as entidades tenant-owned em cada contexto e rejeitar/injetar TenantId incompatível em `SaveChangesAsync`.
- A subfase 1.3 introduz um modelo explícito de autorização por perfil: `ShopRole` e `ShopPermission` definem o conjunto de acessos por loja, centralizados em `ShopRolePermissions` e consumidos por policies de autorização do ASP.NET Core. As políticas são registradas no startup da API e respeitam a regra de que todo acesso exige um usuário autenticado e, quando aplicável, um role válido para o tenant correspondente.
- A autenticação é delegada a um gateway, que emite JWT assinado com exatamente um claim `tenant_id` (UUID não vazio). A API valida assinatura via discovery/JWKS do `Authority`, emissor, `Audience` e validade do token antes de resolver o tenant.
- `Authentication:Authority` e `Authentication:Audience` são configuração obrigatória por ambiente. A API não inicia sem esses valores e não aceita tenant de payload, query string ou cabeçalho comum.
- O middleware resolve o tenant após autenticação. Claim ausente, malformada ou ambígua resulta em `403`; requests não autenticados ficam sujeitos à política global de autorização. Endpoints explicitamente anônimos, como health, não precisam de tenant.
- SQL Server Row-Level Security não será habilitado no primeiro incremento. Será reavaliado se o modelo de permissões, requisitos operacionais ou uma revisão de segurança exigirem defesa adicional no banco.

## Consequências e controles

- A migration por si só não representa isolamento completo; nenhum fluxo de aplicação deve consultar ou gravar essas tabelas antes da implementação dos filtros e validações da subfase 1.2.
- Testes de integração devem criar registros no Tenant A e confirmar que Tenant B não os lê nem altera.
- `ICurrentTenantAccessor` é scoped por request. Filtros globais são aplicados automaticamente a roots que implementam `IMustHaveTenant`; owned types são protegidos pelo filtro do agregado proprietário.
- `SaveChanges` e `SaveChangesAsync` atribuem o tenant corrente a entidades novas sem `TenantId` e rejeitam gravações sem tenant ou com tenant divergente, inclusive alterações e remoções.
- Como `ApplicationUser` pertence a um tenant, a claim precisa estar resolvida antes de qualquer consulta filtrada de Identity. Papéis globais permanecem sem filtro.
- As permissões por role foram centralizadas em `ShopRolePermissions` para evitar espalhamento de regras em controllers ou endpoints. Isso mantém a autorização previsível e alinhada ao modelo de domínio.
- Relações entre agregados operacionais incluem `TenantId` nas chaves para que o próprio SQL Server recuse referências cruzadas.
- Papéis globais do Identity não levam `TenantId`; associação de usuários a papéis continua vinculada ao usuário e será protegida pelas regras de autorização.
- Testes comportamentais usam SQL Server efêmero em container, aplicam as migrations e validam leitura e gravação entre tenants.

## Alternativas consideradas

- **Row-Level Security desde o início:** oferece defesa adicional no banco, mas exige definir o contexto de sessão SQL, conexões agrupadas e comportamento de migrations. Foi adiado para não duplicar a estratégia antes de validar os fluxos de conexão e autenticação.
- **Filtrar apenas na aplicação por convenção:** rejeitado por ser frágil e depender de cada chamada lembrar do tenant; filtros globais e validação centralizada são obrigatórios.
