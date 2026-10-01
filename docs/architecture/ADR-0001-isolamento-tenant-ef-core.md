# ADR-0001: Isolamento de Tenant com EF Core

- **Status:** Aceita para a primeira entrega
- **Data:** 2026-09-30

## Contexto

O produto usa um único banco SQL Server para vários estabelecimentos. Uma consulta ou gravação que atravesse tenants é uma vulnerabilidade crítica. `agents.md` exige `IMustHaveTenant`, filtros globais por contexto e validação de `TenantId` ao salvar.

## Decisão

- Todos os registros operacionais pertencentes a um estabelecimento carregam `TenantId`; o agregado global `Tenant` e os papéis globais do Identity são exceções deliberadas.
- A subfase 1.1 cria as colunas, índices tenant-scoped e chaves estrangeiras compostas compatíveis com o isolamento.
- A subfase 1.2 deve aplicar Global Query Filters a todas as entidades tenant-owned em cada contexto e rejeitar/injetar TenantId incompatível em `SaveChangesAsync`.
- O contexto atual deve vir de uma resolução confiável do tenant; identificadores enviados pelo cliente não são autoridade.
- SQL Server Row-Level Security não será habilitado no primeiro incremento. Será reavaliado se o modelo de permissões, requisitos operacionais ou uma revisão de segurança exigirem defesa adicional no banco.

## Consequências e controles

- A migration por si só não representa isolamento completo; nenhum fluxo de aplicação deve consultar ou gravar essas tabelas antes da implementação dos filtros e validações da subfase 1.2.
- Testes de integração devem criar registros no Tenant A e confirmar que Tenant B não os lê nem altera.
- Como `ApplicationUser` pertence a um tenant, o fluxo de autenticação precisa estabelecer o tenant antes de executar consultas filtradas de Identity, por exemplo solicitando um identificador público da loja no login. A estratégia concreta será definida e testada na subfase 1.2; não se deve desativar filtros globalmente para contornar esse requisito.
- Relações entre agregados operacionais incluem `TenantId` nas chaves para que o próprio SQL Server recuse referências cruzadas.
- Papéis globais do Identity não levam `TenantId`; associação de usuários a papéis continua vinculada ao usuário e será protegida pelas regras de autorização.

## Alternativas consideradas

- **Row-Level Security desde o início:** oferece defesa adicional no banco, mas exige definir o contexto de sessão SQL, conexões agrupadas e comportamento de migrations. Foi adiado para não duplicar a estratégia antes de validar os fluxos de conexão e autenticação.
- **Filtrar apenas na aplicação por convenção:** rejeitado por ser frágil e depender de cada chamada lembrar do tenant; filtros globais e validação centralizada são obrigatórios.
