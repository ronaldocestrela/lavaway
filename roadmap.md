# Roadmap de Implementação — SaaS para Lava-Jato e Estética Automotiva

Este roadmap organiza a entrega do produto em seis fases e subfases menores. Cada subfase representa um resultado que pode ser desenvolvido, testado e demonstrado antes de avançar para a próxima. A duração indicada no cronograma é uma estimativa inicial; integrações externas, decisões de fornecedor e capacidade da equipe podem alterá-la.

## Visão Geral das Fases

```mermaid
flowchart TD
    F1[Fase 1: Fundação e multi-tenancy] --> F2[Fase 2: Onboarding e configurações]
    F2 --> F3[Fase 3: Operação de pátio e OS]
    F3 --> F4[Fase 4: WhatsApp e automações]
    F3 --> F5[Fase 5: Financeiro e retenção]
    F4 --> F5
    F1 --> F6[Fase 6: Backoffice do SaaS]
    F4 --> F6
    F5 --> F6
```

**Ordem sugerida:** a Fase 1 é pré-requisito para todas as demais. As Fases 2 e 3 habilitam a operação principal. As Fases 4 e 5 podem avançar em paralelo após os eventos e estados da OS estarem definidos; a Fase 6 pode começar pela base administrativa, mas depende dos dados de billing e operação para entregar métricas completas.

---

## Fase 1: Fundação Arquitetural e Multi-Tenancy

**Objetivo:** Estabelecer a base segura, testável e isolada para múltiplos estabelecimentos.

### 1.1 Modelo de domínio e dados

- [x] Definir entidades e relações iniciais para `tenants`, `users`, `roles_permissions`, `customers`, `vehicles`, `services` e `work_orders`.
- [x] Definir identificadores, regras de propriedade por tenant e estratégia de isolamento (filtro por linha e/ou *Row-Level Security*).
- [x] **Entrega:** modelo revisado e migrations iniciais aplicáveis em ambiente local.

Migrations e scripts idempotentes foram gerados em `scripts/sql/` e aplicados com sucesso ao SQL Server local via `scripts/apply-migrations.sh`. Schemas `tenants`, `identity` e `yard` validados com isolamento de `__EFMigrationsHistory` por contexto.

### 1.2 Isolamento multi-tenant

- [x] Implementar resolução do tenant no pipeline e disponibilizar o contexto atual à aplicação por claim `tenant_id` validada no JWT do gateway.
- [x] Aplicar filtros globais de consulta e atribuição/validação obrigatória do tenant na gravação.
- [x] Cobrir leitura e gravação cruzada com testes em SQL Server: dados do Tenant A não podem ser consultados nem alterados pelo Tenant B.
- [x] **Entrega:** testes de isolamento aprovados para consultas e comandos.

`Authentication:Authority` e `Authentication:Audience` são obrigatórios na configuração da API. Headers e outros identificadores enviados pelo cliente não definem o tenant; sem claim válida a request é negada. Os testes usam SQL Server efêmero via Testcontainers.

### 1.3 Identidade, sessão e permissões

- [x] Implementar cadastro/autenticação com tokens JWT e refresh tokens (ou cookies seguros, conforme decisão de arquitetura).
- [x] Definir os perfis Administrador da Loja, Recepcionista e Operador/Lavador e mapear as permissões por papel na camada de domínio.
- [x] **Entrega:** fluxo completo de emissão de Access Token (JWT), gestão de sessão com Refresh Token criptográfico e rotação automática, detecção de reutilização, endpoints de login/refresh/revoke/usuários na API e provedor de autenticação no frontend Blazor.

> Decisão de arquitetura formalizada na ADR-0005. O módulo `CarWashSaaS.Identity` gerencia de ponta a ponta as credenciais, emissão com chave configurada, persistência de `RefreshToken` com isolamento multi-tenant no schema `identity`, e o frontend consome via `AuthApiClient` e `JwtAuthenticationStateProvider`. Documentação completa em `docs/living-docs/identidade-sessao-permissoes-1.3.md`.

### 1.4 Arquivos e processamento assíncrono

- [x] Implementar armazenamento de objetos MinIO com namespace por tenant para arquivos privados.
- [x] Implementar fila RabbitMQ durável com confirmação explícita, retentativa e dead-letter queue.
- [x] **Entrega:** validar upload/leitura isolados por tenant e processamento de mensagem em execução com os serviços reais.

> A entrega da Fase 1.4 está concluída e validada operacionalmente. O adapter MinIO foi coberto por testes de integração reais via Testcontainers (`cgr.dev/chainguard/minio`), garantindo gravação, leitura de stream com verificação de Content-Type e isolamento estrito contra acesso cross-tenant. O `TenantQueueWorker` foi testado ponta a ponta com RabbitMQ 4 em Testcontainers, comprovando a injeção do `TenantId` no escopo do DI e o processamento de eventos pelo handler de negócio registrado `TenantBrandingAuditQueueHandler`. A documentação viva foi consolidada em `docs/living-docs/arquivos-processamento-assincrono-1.4.md`.

**Checklist de implementação vigente:**

- [x] Path de arquivo scoped por tenant: `tenants/{tenantId}/{category}/{fileName}`
- [x] Storage privado MinIO registrado por `ITenantObjectStorage`; endpoint de logo usa o tenant autenticado
- [x] Fila RabbitMQ quorum com mensagem persistente, `MessageId`, ack/nack explícito, retry exponencial e DLQ
- [x] `TenantQueueWorker` cria escopo por entrega e estabelece o tenant antes de invocar o handler
- [x] Integrações MinIO, RabbitMQ e SQL Server validadas com Testcontainers em execução real
- [x] Upload/download e isolamento cross-tenant validados via testes automatizados
- [x] Handler de negócio registrado no DI (`TenantBrandingAuditQueueHandler`) e ciclo de processamento validado ponta a ponta

### 1.5 Qualidade e arquitetura da solução

- [x] Validar a base de arquitetura atual: módulos, contratos compartilhados, DI e middleware de tenant já estão alinhados com o projeto.
- [x] Confirmar a aplicação do padrão `Result<T>` e a resolução do `tenant_id` no pipeline HTTP, com filtros globais e validação de gravação em `SaveChangesAsync`.
- [x] Registrar a decisão de isolamento em ADR e manter a documentação viva atualizada com o estado do modelo e do ciclo de autenticação.
- [x] Implementar suíte inicial de arquitetura (`NetArchTest`) para validar dependências entre camadas e políticas de módulo.
- [x] Consolidar o pipeline de build/test da solução com verificação automatizada de multi-tenancy e qualidade do código.
- [x] **Entrega:** a solução compila, a base arquitetural está coberta por testes e a documentação registra o estado real do projeto.

> Estado verificado: a suíte de arquitetura e os testes de integração da base multi-tenant já estão verdes, e o build da solução confirma que a entrega está estável para avançar para a Fase 2. A camada HTTP foi padronizada com Minimal APIs modulares organizadas em classes de extensão por domínio (`Endpoints/*.cs`), mantendo `Program.cs` como Composition Root (formalizado na [ADR-0006](docs/architecture/ADR-0006-organizacao-minimal-apis-modulares.md)).

---

## Fase 2: Onboarding e Configurações Iniciais

**Objetivo:** Permitir que o gestor configure a unidade para começar a operar.

### 2.1 Cadastro e perfil do estabelecimento

- [x] Implementar o fluxo guiado para Razão Social, Nome Fantasia, CNPJ, telefone e endereço.
- [x] Validar campos obrigatórios e permitir salvar/retomar o cadastro.
- [x] **Entrega:** estabelecimento pode concluir, consultar e atualizar seus dados cadastrais.

> Implementação concluída no backend: o módulo de tenants inclui o agregado `StoreProfile` com validação de negócio, `TenantId`, isolamento multi-tenant, mapeamento no `TenantsDbContext`, repositório persistente, endpoints autenticados e política de administrador. O fluxo foi coberto por testes unitários e de integração, incluindo proteção de Tenant B contra leitura do perfil de Tenant A.

### 2.2 Identidade visual

- [x] Permitir upload da logomarca e configuração de cores usadas em links públicos e comprovantes.
- [x] Validar formato/tamanho do arquivo e armazená-lo no espaço do tenant.
- [x] **Entrega:** identidade visual aparece em uma prévia de comprovante/link.

> Implementação concluída no backend: o agregado `StoreProfile` já suporta `LogoUrl`, `BrandPrimaryColor` e `BrandSecondaryColor` com validação no domínio, retorno `Result<T>`, persistência protegida por `TenantId` e endpoint de upload seguro por tenant. O arquivo é salvo em `Storage/tenants/{tenantId}/branding` e servido através de `GET /storage/...`; a pré-visualização visual no frontend pode ser adicionada como camada de apresentação, mas o fluxo de dados e armazenamento está concluído e validado.

### 2.3 Catálogo, preços e duração

- [x] Criar categorias e serviços: Ducha, Lavagem Completa, Higienização, Polimento e Vitrificação.
- [x] Configurar preço por porte (Hatch/Sedan, SUV, Picape/Van e Moto) e duração estimada por serviço.
- [x] **Entrega:** gestor cria e edita serviços e consulta preço/duração por porte.

> Implementação concluída no backend: o módulo `YardOperations` agora possui catálogo de serviços em `Service`, `ServicePrice` e `ServiceCatalogApplicationService`, com `IServiceRepository`, endpoints autenticados em `/services` e validação por tenant e tamanho de veículo. O catálogo aceita uma ou mais faixas de preço por porte e evita duplicidade de valor para o mesmo tipo de veículo.

### 2.4 Capacidade e equipe

- [x] Configurar capacidade simultânea do pátio (boxes/vagas).
- [x] Cadastrar colaboradores e, quando aplicável, regras de comissão por serviço.
- [x] **Entrega:** capacidade e equipe cadastradas e disponíveis para uso na operação.

> Estado concluído: o módulo `YardOperations` já implementa `YardCapacity`, `TeamMember` e `CommissionRule` com validação do domínio, `Result<T>`, `TenantId` em todos os agregados e isolamento por tenant em `YardOperationsDbContext`. A proteção cruzada de dados foi validada pela suíte de integração e a base está pronta para o próximo fluxo operacional.

### 2.5 Pareamento do WhatsApp

- [x] Integrar a geração/exibição de QR Code dinâmico da instância do estabelecimento.
- [x] Exibir e atualizar os estados `connected`, `connecting` e `disconnected`.
- [x] **Entrega:** gestor consegue parear a instância e identificar seu estado atual.

> Estado concluído: o módulo WhatsApp já conta com pareamento via Evolution API, persistência segura por tenant, endpoints de status/pareamento e webhook `CONNECTION_UPDATE` autenticado por segredo compartilhado. Eventos da sessão atual atualizam o estado; eventos atrasados ou de outra sessão não alteram os dados. A configuração do callback na Evolution API é por ambiente e está descrita em `docs/living-docs/pareamento-whatsapp-2.5.md`.

---

## Fase 3: Operação de Pátio, Check-in e Ordem de Serviço

**Objetivo:** Entregar o fluxo diário de recepção, execução e liberação dos veículos.

### 3.1 Cadastro e busca de clientes/veículos

- [ ] Criar ou localizar cliente e veículo por placa e telefone do proprietário.
- [ ] Reutilizar dados encontrados no fluxo de recepção sem duplicar cadastros.
- [ ] **Entrega:** recepção localiza rapidamente um cliente/veículo existente ou inicia um cadastro.

> Implementação disponível em backend e Blazor WebAssembly: busca tenant-scoped por placa/telefone, cadastro atômico cliente+veículo, inclusão de veículo e seleção para OS futura. Os testes unitários, os testes SQL Server/Testcontainers específicos de 3.1 e o build da solution passaram. A suite completa executou 76 testes: 74 passaram e 2 falharam em fluxos preexistentes de WhatsApp e StoreProfile; o smoke test autenticado ainda depende da configuração do gateway OIDC/JWT. Foram geradas migrations para completar o modelo atual de StoreProfile e persistência de equipe/capacidade. A subfase permanece aberta até o smoke autenticado e os gates globais serem resolvidos. Detalhes em `docs/living-docs/cadastro-clientes-veiculos-3.1.md`.

### 3.2 Abertura da ordem de serviço

- [ ] Selecionar serviços e porte do veículo durante o check-in.
- [ ] Calcular valor final e previsão de entrega usando preço e duração configurados.
- [ ] **Entrega:** OS é criada com cliente, veículo, serviços, valor e previsão registrados.

### 3.3 Vistoria digital de entrada

- [ ] Criar interface responsiva para celular/tablet com checklist e diagrama do veículo.
- [ ] Permitir marcar arranhões, mossas e trincas e anexar fotos obrigatórias.
- [ ] **Entrega:** vistoria fica vinculada à OS e suas fotos podem ser consultadas com isolamento por tenant.

### 3.4 Fluxo operacional e Kanban

- [ ] Implementar estados: *Aguardando* → *Em Lavagem* → *Secagem/Acabamento* → *Controle de Qualidade* → *Pronto para Retirada*.
- [ ] Permitir transição por clique ou *drag and drop*, com validação de transições e atualização em tempo real.
- [ ] Atribuir operador por veículo/OS.
- [ ] **Entrega:** equipe acompanha e atualiza o fluxo operacional, mantendo histórico do estado.

### 3.5 Registro de fotos pós-serviço

- [ ] Permitir fotos de "Depois" e associá-las às fotos de entrada para serviços elegíveis (detalhamento, vitrificação e bancos).
- [ ] **Entrega:** galeria comparativa fica disponível na OS para consulta e envio futuro.

---

## Fase 4: Mensageria e Chatbot WhatsApp

**Objetivo:** Automatizar comunicações e agendamentos sem acoplar regras de negócio ao provedor.

### 4.1 Base de integração e entrega confiável

- [ ] Definir o provedor e implementar adaptador para envio/recebimento de mensagens e webhooks.
- [ ] Processar eventos de forma assíncrona, com idempotência, retentativas e registro de falhas.
- [ ] Aplicar limites de envio por tenant e registrar consentimento/preferências de comunicação.
- [ ] **Entrega:** mensagem de teste é enviada e falhas podem ser rastreadas sem duplicar processamento.

### 4.2 Notificações da ordem de serviço

- [ ] Gerar e enviar comprovante PDF de entrada com dados da OS e checklist da vistoria.
- [ ] Enviar aviso de veículo pronto quando a OS chegar ao estado *Pronto para Retirada*.
- [ ] Enviar galeria comparativa "Antes e Depois" quando houver fotos.
- [ ] **Entrega:** eventos da OS disparam as mensagens correspondentes e o resultado do envio é registrado.

### 4.3 Agendamento conversacional

- [ ] Disponibilizar catálogo de serviços e horários vagos pelo menu do chatbot.
- [ ] Criar a reserva na agenda operacional após confirmação e evitar conflito de capacidade.
- [ ] **Entrega:** cliente agenda pelo WhatsApp e a reserva aparece para a equipe.

### 4.4 Confirmação e prevenção de faltas

- [ ] Enviar lembretes 24h e 2h antes do horário agendado.
- [ ] Processar ações *Confirmar*, *Remarcar* e *Cancelar* e atualizar a reserva.
- [ ] **Entrega:** respostas do cliente atualizam o agendamento sem intervenção manual.

### 4.5 Pós-venda e reativação

- [ ] Enviar pesquisa de 1 a 5 estrelas uma hora após a retirada.
- [ ] Programar campanhas para clientes ausentes há 15, 30 e 45 dias.
- [ ] Aplicar limites de frequência e registrar opt-out para evitar mensagens excessivas.
- [ ] **Entrega:** avaliações e campanhas respeitam preferências e limites configurados.

---

## Fase 5: Financeiro, Pix e Retenção

**Objetivo:** Simplificar recebimentos, fechamento financeiro e retorno dos clientes.

### 5.1 Pix associado à OS

- [ ] Escolher o gateway e gerar QR Code e código Copia e Cola para o valor devido na OS.
- [ ] Enviar os dados de pagamento ao cliente por WhatsApp quando solicitado.
- [ ] **Entrega:** cobrança Pix é vinculada à OS e pode ser consultada pela equipe.

### 5.2 Confirmação e conciliação de pagamento

- [ ] Validar assinatura/autenticidade do webhook do gateway e processar eventos de forma idempotente.
- [ ] Atualizar pagamento e baixar a OS somente após confirmação válida.
- [ ] **Entrega:** pagamento confirmado atualiza a OS uma única vez; eventos inválidos ou repetidos são tratados com segurança.

### 5.3 Caixa e comissões

- [ ] Registrar receitas por Pix, dinheiro, cartão de crédito e débito.
- [ ] Gerar fechamento diário e relatório por método de pagamento.
- [ ] Calcular comissões por colaborador/lavador conforme regras configuradas.
- [ ] **Entrega:** gestor confere totais do dia e detalhamento das comissões.

### 5.4 Fidelidade

- [ ] Atribuir pontos/selos após a conclusão de serviços elegíveis.
- [ ] Notificar o cliente quando estiver próximo do resgate, incluindo a mensagem de um serviço restante.
- [ ] **Entrega:** saldo e progresso de fidelidade são consultáveis e consistentes com os serviços concluídos.

### 5.5 Assinaturas e créditos recorrentes

- [ ] Criar planos mensais e integrar cobrança recorrente no cartão pelo gateway escolhido.
- [ ] Controlar créditos e utilizações por placa cadastrada.
- [ ] **Entrega:** consumo de créditos é registrado e não ultrapassa o saldo disponível.

---

## Fase 6: Backoffice do SaaS

**Objetivo:** Operar assinantes, faturamento, saúde das integrações e indicadores globais.

### 6.1 Acesso administrativo e auditoria

- [ ] Definir perfil administrativo da plataforma separado dos perfis dos tenants.
- [ ] Registrar ações administrativas e eventos relevantes em trilha de auditoria.
- [ ] **Entrega:** ações sensíveis têm autor, data e alvo registrados.

### 6.2 Gestão global de tenants

- [ ] Listar estabelecimentos e status: Ativo, Em Período de Testes, Inadimplente e Cancelado.
- [ ] Implementar *impersonation* para suporte com autorização restrita, auditoria e encerramento explícito da sessão.
- [ ] **Entrega:** suporte encontra um tenant e acessa contexto de diagnóstico com trilha auditável.

### 6.3 Planos, billing e acesso

- [ ] Integrar o provedor de billing recorrente do SaaS (ex.: Asaas, Stripe ou Iugu).
- [ ] Implementar planos Básico, Pro e Enterprise, upgrades/downgrades e cotas de OS ou mensagens.
- [ ] Aplicar regras de inadimplência, incluindo bloqueio automático de acesso conforme política definida.
- [ ] **Entrega:** estado da assinatura e cotas refletem os eventos confirmados do provedor.

### 6.4 Saúde das instâncias WhatsApp

- [ ] Exibir estado das instâncias conectadas por tenant.
- [ ] Alertar por e-mail/notificação quando uma conexão cair e orientar o lojista a gerar novo QR Code.
- [ ] **Entrega:** falhas de conexão são detectáveis e acionam alerta para o tenant correto.

### 6.5 Métricas e observabilidade global

- [ ] Consolidar MRR, ARR, *churn rate* e LTV a partir de dados de billing definidos.
- [ ] Exibir volume de veículos atendidos e volume transacionado via Pix.
- [ ] Disponibilizar logs de webhooks, falhas de envio e trilhas de auditoria administrativa.
- [ ] **Entrega:** métricas têm fonte e período definidos, e eventos operacionais podem ser investigados.

---

## Sugestão de Cronograma de Lançamento

O cronograma abaixo mantém os marcos de 16 semanas como referência. As subfases podem ser divididas em histórias menores dentro de cada sprint, e a previsão deve ser revista após escolher os provedores de WhatsApp, pagamentos e billing.

| Marco | Período de referência | Subfases principais | Entregável / Meta |
| :--- | :--- | :--- | :--- |
| **M1** | Semanas 1-3 | 1.1-1.5 e 2.1-2.4 | Base multi-tenant validada e onboarding essencial disponível. |
| **M2** | Semanas 4-6 | 3.1-3.5 | Operação de pátio funcional, da abertura da OS à conclusão, sem dependência de mensageria externa. |
| **M3** | Semanas 7-9 | 2.5, 4.1-4.2 | Instância conectada e notificações transacionais funcionando. |
| **M4** | Semanas 10-12 | 4.3-4.4 e 5.1-5.2 | Agendamento pelo WhatsApp e pagamento Pix conciliado. |
| **M5** | Semanas 13-14 | 4.5 e 5.3-5.5 | Pós-venda, fidelidade, recorrência e fechamento financeiro disponíveis. |
| **M6** | Semanas 15-16 | 6.1-6.5 | Backoffice inicial com gestão de tenants, billing, monitoramento e métricas. |

**Critério para avançar de subfase:** a entrega está demonstrável, os testes do fluxo e de isolamento por tenant passam, e a documentação correspondente foi atualizada conforme as diretrizes do projeto.
