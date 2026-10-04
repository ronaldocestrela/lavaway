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

> Estado verificado e consolidado: a suíte de arquitetura foi formalizada com `NetArchTest.Rules` (8 guardrails automatizados cobrindo pureza de Domain, isolamento de Application/Infrastructure, ausência de IQueryable em repositórios e adesão a IMustHaveTenant/UUID/Result), a documentação viva foi registrada em [docs/living-docs/qualidade-arquitetura-1.5.md](docs/living-docs/qualidade-arquitetura-1.5.md) e [docs/living-docs/isolamento-multi-tenant-1.2.md](docs/living-docs/isolamento-multi-tenant-1.2.md), e os gates de qualidade foram consolidados via [.editorconfig](.editorconfig), script local [scripts/verify-quality.sh](scripts/verify-quality.sh) e pipeline de CI no GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)). A camada HTTP segue padronizada com Minimal APIs modulares organizadas em classes de extensão por domínio (`Endpoints/*.cs`), mantendo `Program.cs` como Composition Root (formalizado na [ADR-0006](docs/architecture/ADR-0006-organizacao-minimal-apis-modulares.md)).

---

## Fase 2: Onboarding e Configurações Iniciais

**Objetivo:** Permitir que o gestor configure a unidade para começar a operar.

### 2.1 Cadastro e perfil do estabelecimento

- [x] Implementar o fluxo guiado para Razão Social, Nome Fantasia, CNPJ, telefone e endereço.
- [x] Validar campos obrigatórios e permitir salvar/retomar o cadastro.
- [x] **Entrega:** estabelecimento pode concluir, consultar e atualizar seus dados cadastrais.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor): o módulo de tenants inclui o agregado `StoreProfile` com validação de negócio, `TenantId`, isolamento multi-tenant, mapeamento no `TenantsDbContext`, repositório persistente, endpoints autenticados e política de administrador. Contratos compartilhados (`StoreProfileDto`, `CreateStoreProfileRequest`, `UpdateStoreProfileRequest`) conectam a API ao cliente `StoreProfileApiClient`. O frontend Blazor dispõe de fluxo guiado de 3 etapas (`StoreProfileWizard`), tela de visualização e edição (`StoreProfilePage`), validação e formatação automática de CNPJ/CEP e item no menu restrito a administradores. A entrega está 100% coberta por testes unitários, testes de arquitetura, testes de integração SQL Server e testes de componentes bUnit. Detalhes em `docs/living-docs/cadastro-perfil-estabelecimento-2.1.md`.

### 2.2 Identidade visual

- [x] Permitir upload da logomarca e configuração de cores usadas em links públicos e comprovantes.
- [x] Validar formato/tamanho do arquivo e armazená-lo no espaço do tenant.
- [x] **Entrega:** identidade visual aparece em uma prévia de comprovante/link.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor): o módulo de tenants possui suporte a logomarca e paleta de cores (primária e secundária) com validação rígida de domínio (`#RRGGBB` e URLs), retorno `Result<T>`, persistência protegida por `TenantId` no SQL Server e armazenamento seguro no MinIO (`tenants/{tenantId}/branding/`) com limite de 2 MB e extensões permitidas. A API conta com upload multipart autenticado (`POST /tenants/profile/logo`), endpoint autenticado de logo e rota pública segura para comprovantes e links (`GET /tenants/{tenantId}/public/logo`), além de auditoria assíncrona no RabbitMQ. O frontend Blazor dispõe de suporte completo no cliente tipado (`StoreProfileApiClient.UploadLogoAsync`), etapa dedicada de Identidade Visual no `StoreProfileWizard` com upload drag-and-drop, seletores de cor e presets rápidos automotivos, e o componente `BrandReceiptPreview` que exibe a identidade visual em tempo real em dois formatos (comprovante térmico/digital de OS e link público de acompanhamento no WhatsApp). A tela `StoreProfilePage` exibe a logomarca e a paleta ativa da loja com gaveta interativa de visualização. Coberto por testes unitários, testes de componentes bUnit e testes de integração SQL Server/MinIO/RabbitMQ. Detalhes em `docs/living-docs/identidade-visual-2.2.md`.

### 2.3 Catálogo, preços e duração

- [x] Criar categorias e serviços: Ducha, Lavagem Completa, Higienização, Polimento e Vitrificação.
- [x] Configurar preço por porte (Hatch/Sedan, SUV, Picape/Van e Moto) e duração estimada por serviço.
- [x] **Entrega:** gestor cria e edita serviços e consulta preço/duração por porte.

> Implementação concluída no backend: o módulo `YardOperations` agora possui catálogo de serviços em `Service`, `ServicePrice` e `ServiceCatalogApplicationService`, com `IServiceRepository`, endpoints autenticados em `/services` e validação por tenant e tamanho de veículo. O catálogo aceita uma ou mais faixas de preço por porte e evita duplicidade de valor para o mesmo tipo de veículo.

### 2.4 Capacidade e equipe

- [x] Configurar capacidade simultânea do pátio (boxes/vagas).
- [x] Cadastrar colaboradores e, quando aplicável, regras de comissão por serviço.
- [x] **Entrega:** capacidade e equipe cadastradas e disponíveis para uso na operação.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor): o módulo `YardOperations` implementa os agregados `YardCapacity`, `TeamMember` e `CommissionRule` com validação de domínio puro, invariantes protegidas, ciclo de vida completo (criação, edição, ativação/desativação e exclusão) e retorno `Result<T>`. Contratos compartilhados em `CarWashSaaS.Shared.Contracts` (`YardCapacityDto`, `TeamMemberDto`, `CommissionRuleDto`, requisições tipadas) padronizam a comunicação eliminando o vazamento de entidades de domínio na API. Os endpoints em `/yard/capacity`, `/team-members` e `/commission-rules` contam com autorização por tenant e política `Administrator`. No frontend Blazor WebAssembly, o cliente `YardSetupApiClient` e os componentes isolados (`YardCapacityCard`, `TeamMemberModal`, `CommissionRuleModal`) integram a nova tela administrativa `CapacityAndTeamPage` (`/settings/team`), adicionada ao `NavMenu`. A entrega conta com 100% de aprovação nos testes unitários, testes de arquitetura e testes de componentes bUnit. Detalhes em `docs/living-docs/capacidade-equipe-2.4.md`.

### 2.5 Pareamento do WhatsApp

- [x] Integrar a geração/exibição de QR Code dinâmico da instância do estabelecimento.
- [x] Exibir e atualizar os estados `connected`, `connecting` e `disconnected`.
- [x] **Entrega:** gestor consegue parear a instância e identificar seu estado atual.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor): o módulo WhatsApp dispõe de pareamento via Evolution API com fallback seguro, isolamento multi-tenant estrito por `TenantId`, contratos tipados compartilhados em `CarWashSaaS.Shared.Contracts` (`WhatsAppConnectionDto`, `WhatsAppStatusConstants`), ciclo de vida com endpoints autenticados para status (`GET /whatsapp/status`), início (`POST /whatsapp/pairing/start`), atualização (`POST /whatsapp/pairing/refresh`) e desconexão (`POST /whatsapp/pairing/disconnect`), além de webhook assíncrono `CONNECTION_UPDATE` com validação de segredo compartilhado em tempo constante. No frontend Blazor WebAssembly, o cliente fortemente tipado `WhatsAppApiClient` alimenta o componente `WhatsAppPairingCard` com design premium automotivo, badges de status com indicador luminoso/pulsante, exibição do QR Code dinâmico com instruções passo a passo para o celular e a tela administrativa `WhatsAppSettingsPage` (`/settings/whatsapp`), integrada com item dedicado `05 WhatsApp` no `NavMenu` e polling reativo automático (3s) para detecção instantânea da leitura do código. A subfase está 100% coberta por testes unitários, testes de arquitetura (NetArchTest), testes de integração multi-tenant SQL Server e testes de componentes bUnit. Detalhes em `docs/living-docs/pareamento-whatsapp-2.5.md`.

---

## Fase 3: Operação de Pátio, Check-in e Ordem de Serviço

**Objetivo:** Entregar o fluxo diário de recepção, execução e liberação dos veículos.

### 3.1 Cadastro e busca de clientes/veículos

- [x] Criar ou localizar cliente e veículo por placa e telefone do proprietário.
- [x] Reutilizar dados encontrados no fluxo de recepção sem duplicar cadastros.
- [x] **Entrega:** recepção localiza rapidamente um cliente/veículo existente ou inicia um cadastro.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo `YardOperations` implementa os agregados `Customer` e `Vehicle` com normalização rigorosa de placa Mercosul/antiga e telefone, busca tenant-scoped combinada ou individual, persistência atômica cliente+primeiro veículo e adição subsequente de novos veículos sem duplicidade. A API conta com endpoints autenticados em `/customers/search`, `/customers/{id}` e inclusão de veículos com isolamento estrito via `TenantId` e retorno em `Result<T>`. No frontend Blazor WebAssembly, os componentes isolados `CustomerMatchRow`, `CustomerVehicleCreateCard` e `AddVehicleCard` em `CarWashSaaS.Client.Components` integram-se à `ReceptionPage` com design automotivo e o novo state container `ReceptionSessionState` em `CarWashSaaS.Client.Core`, viabilizando transição reativa imediata para a subfase 3.2 (Abertura de OS). A solução conta com 100% de aprovação nos 209 testes da solution (testes unitários, testes de integração SQL Server Testcontainers com validação cross-tenant, testes de limites de arquitetura NetArchTest e testes de componentes bUnit). Detalhes em `docs/living-docs/cadastro-clientes-veiculos-3.1.md`.


### 3.2 Abertura da ordem de serviço

- [x] Selecionar serviços e porte do veículo durante o check-in.
- [x] Calcular valor final e previsão de entrega usando preço e duração configurados.
- [x] **Entrega:** OS é criada com cliente, veículo, serviços, valor e previsão registrados.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo `YardOperations` implementa a abertura de ordens de serviço (`WorkOrder` e `WorkOrderItem`) com snapshot de preços e durações configurados por porte de veículo, cálculo dinâmico da previsão estimada de conclusão (`EstimatedCompletionAtUtc`), registro de observações do check-in (`Notes`) e status inicial `Waiting`. A API conta com endpoints autenticados em `/work-orders` (criação via política `CreateWorkOrders`, consulta por ID e listagem recente via `ViewCustomers`) com isolamento multi-tenant estrito e contratos tipados compartilhados em `CarWashSaaS.Shared.Contracts` (`CreateWorkOrderRequest`, `WorkOrderDto`, `WorkOrderItemDto`). No frontend Blazor WebAssembly, a tela de check-in `NewWorkOrderPage` (`/work-orders/new`) integra-se com os componentes isolados `CheckinCustomerHeader` (com placa estilizada e seletor de porte interativo), `ServiceSelectorCard` (com filtragem, precificação dinâmica por porte e controladores de quantidade), `WorkOrderSummaryCard` (com cálculo reativo em tempo real do total financeiro, tempo total e banner de previsão de entrega com relógio) e `WorkOrderCreatedModal` (modal de confirmação de abertura de OS com ações direcionadas). A solução conta com 100% de aprovação nos 226 testes da solution (testes unitários, testes de integração SQL Server Testcontainers com validação cross-tenant, testes de arquitetura NetArchTest e testes de componentes bUnit). Detalhes em `docs/living-docs/abertura-ordem-servico-3.2.md`.


### 3.3 Vistoria digital de entrada

- [x] Criar interface responsiva para celular/tablet com checklist e diagrama do veículo.
- [x] Permitir marcar arranhões, mossas e trincas e anexar fotos obrigatórias.
- [x] **Entrega:** vistoria fica vinculada à OS e suas fotos podem ser consultadas com isolamento por tenant.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo `YardOperations` implementa o agregado `VehicleInspection` com avarias vetoriais (`InspectionDamage` com coordenadas normatizadas X% e Y% em 5 vistas da carroceria), checklist de pertences e combustível (`InspectionChecklistItem`), galeria de fotos privadas (`InspectionPhoto`) com validação de obrigatoriedade das 4 fotos de perímetro (Frente, Traseira, Lateral Esquerda e Lateral Direita) para conclusão. A API conta com endpoints REST autenticados em `/work-orders/{workOrderId}/inspection` (início, checklist, avarias, upload multipart de fotos no MinIO sob `tenants/{tenantId}/inspections/...`, download seguro e conclusão). No frontend Blazor WebAssembly, a tela `InspectionPage` (`/work-orders/{id}/inspection`) e os componentes isolados `VehicleInspectionDiagram`, `InspectionChecklistCard` e `InspectionPhotoGallery` oferecem experiência responsiva e tátil para celular/tablet com captura de câmera integrada, conectados diretamente ao modal da OS (`WorkOrderCreatedModal`). Detalhes em `docs/living-docs/vistoria-digital-entrada-3.3.md`.

### 3.4 Fluxo operacional e Kanban

- [x] Implementar estados: *Aguardando* → *Em Lavagem* → *Secagem/Acabamento* → *Controle de Qualidade* → *Pronto para Retirada*.
- [x] Permitir transição por clique ou *drag and drop*, com validação de transições e atualização em tempo real.
- [x] Atribuir operador por veículo/OS.
- [x] **Entrega:** equipe acompanha e atualiza o fluxo operacional, mantendo histórico do estado.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo `YardOperations` implementa a máquina de estados operacional no agregado `WorkOrder` com transições estritas nos 5 estados (*Aguardando* → *Em Lavagem* → *Secagem/Acabamento* → *Controle de Qualidade* → *Pronto para Retirada*), mecanismo de refação com justificativa obrigatória e registro imutável em `WorkOrderStatusHistory`. Permite atribuição e desassociação de operadores ativos (`TeamMember`), cálculo em tempo real de ocupação de boxes versus `YardCapacity` e canais de tempo real com SignalR (`YardHub` em `/hubs/yard` com grupos por `TenantId`). A API conta com endpoints REST autenticados em `/yard/kanban`, `/work-orders/{id}/status`, `/work-orders/{id}/operator` e `/work-orders/{id}/history` sob as políticas `UpdateWorkOrderStatus` e `ViewCustomers`. No frontend Blazor WebAssembly, a tela `YardPage` (`/yard`) e os componentes isolados `YardKanbanBoard`, `YardKanbanColumn`, `YardKanbanCard` e `WorkOrderHistoryDrawer` oferecem layout em 5 colunas com drag and drop nativo, ações rápidas de avanço/retorno por clique, modais táteis de refação e atribuição de operador, busca rápida por placa/cliente, filtro por operador e medidor de capacidade do pátio. A solução conta com 100% de aprovação nos 268 testes da solution (testes unitários, testes de integração SQL Server Testcontainers com isolamento cross-tenant, testes de limites de arquitetura NetArchTest e testes de componentes bUnit). Detalhes em `docs/living-docs/fluxo-operacional-kanban-3.4.md`.

### 3.5 Registro de fotos pós-serviço

- [x] Permitir fotos de "Depois" e associá-las às fotos de entrada para serviços elegíveis (detalhamento, vitrificação e bancos).
- [x] **Entrega:** galeria comparativa fica disponível na OS para consulta e envio futuro.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo `YardOperations` implementa o registro de fotos de conclusão ("Depois") associadas opcionalmente às fotos de entrada ("Antes") no agregado `WorkOrder` por meio da entidade `PostServicePhoto` (`IMustHaveTenant`, Guid v7 e metadados contextuais). A regra de domínio `PostServiceEligibilityRule` identifica automaticamente serviços elegíveis (polimento/detalhamento, vitrificação e higienização de bancos/interiores) impedindo anexação fora do fluxo operacional ou em ordens inelegíveis. O armazenamento utiliza `ITenantObjectStorage` (MinIO) sob o caminho isolado `tenants/{tenantId}/post-service-photos/{file}`, validando tamanho máximo (10MB) e tipos MIME seguros (`image/jpeg`, `image/png`, `image/webp`). A API expõe endpoints autenticados sob a política `UpdateWorkOrderStatus` em `/work-orders/{id}/comparison-gallery`, `/work-orders/{id}/post-service-photos` (upload multipart, streaming seguro e remoção). No frontend Blazor WebAssembly, o componente interativo `PhotoComparisonGallery` oferece visualização em tela cheia com slider divisor "Antes/Depois" interativo (com recorte CSS preciso), modo lado a lado (*side-by-side*), cartões de fotos avulsas e modal responsivo de captura de fotos otimizado para celulares/tablets, integrado diretamente aos cards do Kanban (`YardKanbanCard` com badge visual `📸 Antes/Depois`) e na página operacional `YardPage`. A solução conta com 100% de aprovação nos 283 testes da solution (testes unitários de domínio, testes de integração SQL Server Testcontainers com validação cross-tenant, testes de limites de arquitetura NetArchTest e testes de componentes bUnit). Detalhes em `docs/living-docs/registro-fotos-pos-servico-3.5.md`.

---

## Fase 4: Mensageria e Chatbot WhatsApp

**Objetivo:** Automatizar comunicações e agendamentos sem acoplar regras de negócio ao provedor.

### 4.1 Base de integração e entrega confiável

- [x] Definir o provedor e implementar adaptador para envio/recebimento de mensagens e webhooks.
- [x] Processar eventos de forma assíncrona, com idempotência, retentativas e registro de falhas.
- [x] Aplicar limites de envio por tenant e registrar consentimento/preferências de comunicação.
- [x] **Entrega:** mensagem de teste é enviada e falhas podem ser rastreadas sem duplicar processamento.

> Implementação concluída de ponta a ponta (Backend e Frontend Blazor WebAssembly): o módulo WhatsApp dispõe de envio assíncrono durável via RabbitMQ (`IBackgroundQueue` com `OutboundWhatsAppMessageQueueHandler`), adaptador HTTP tipado para Evolution API (`EvolutionApiWhatsAppMessageSender`), garantia estrita de idempotência por `IdempotencyKey`, isolamento multi-tenant completo no schema `whatsapp` (`OutboundWhatsAppMessage`, `WhatsAppDeliveryAttempt`, `TenantWhatsAppQuota` e `CustomerCommunicationPreference`), governança anti-ban com controle de taxa de envio por minuto e cota diária, registro de consentimento LGPD (Opt-in/Opt-out) e ingestão de webhooks de status de entrega (`MESSAGES_UPDATE` e `SEND_MESSAGE`). A API expõe endpoints autenticados sob política `Administrator` em `/whatsapp/messages/test`, `/whatsapp/messages`, `/whatsapp/messages/{id}` e `/whatsapp/quota`. No frontend Blazor WebAssembly, a tela administrativa `WhatsAppSettingsPage` integra os novos componentes isolados `WhatsAppTestMessageCard` (disparo de teste com validação em tempo real de telefone), `WhatsAppQuotaMeter` (medidor de cota diária e velocidade de envio anti-ban) e `WhatsAppMessageHistoryTable` (tabela de mensagens recentes com badges de status e rastreamento de falhas). A solução conta com 100% de aprovação nos 272 testes da solution (testes unitários, testes de arquitetura NetArchTest e testes de componentes bUnit). Decisão formalizada na [ADR-0007](docs/architecture/ADR-0007-mensageria-assincrona-e-entrega-confiavel-whatsapp.md) e documentação viva em [docs/living-docs/base-integracao-entrega-confiavel-4.1.md](docs/living-docs/base-integracao-entrega-confiavel-4.1.md).


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
