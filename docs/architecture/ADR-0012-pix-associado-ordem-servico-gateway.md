# ADR-0012: Pix Associado à Ordem de Serviço (OS) e Gateway de Pagamento

## Status
Aceito

## Contexto
Na subfase 5.1 do Lavaway SaaS, tornou-se necessária a implementação da cobrança instantânea via Pix associada diretamente às Ordens de Serviço (OS), permitindo que a equipe gere o QR Code dinâmico e o código Copia e Cola no balcão, consulte o status e encaminhe os dados de pagamento de forma contextualizada para o cliente via WhatsApp (ou quando solicitado conversacionalmente no chatbot).

Requisitos essenciais:
1. **Módulo Vertical Autônomo `Billing`:** Isolamento completo de dados financeiros, com schema dedicado `billing` e persistência sem acoplamento a DbContexts de outros módulos.
2. **Abstração de Gateway Pix (*Ports & Adapters*):** Interface desacoplada `IPixGatewayProvider` permitindo múltiplos provedores (Mercado Pago, Efí/Gerencianet, Asaas) e um provedor determinístico simulado (`SimulatedPixGatewayProvider`) para ambientes de desenvolvimento, CI/CD e testes automatizados.
3. **Vínculo com a Ordem de Serviço:** Geração de cobrança com TxId único, QR Code Base64 (SVG/PNG) e código Copia e Cola (padrão EMV BCB) correspondente ao valor total devido na OS.
4. **Despacho e Interceptação via WhatsApp:** Possibilidade de enviar a cobrança diretamente ao telefone do cliente através do `IOutboundWhatsAppDispatcher`, além de interceptação conversacional no `ChatbotConversationEngine` quando o cliente envia palavras-chave como "PIX", "PAGAR" ou "SEGUNDA VIA".
5. **Multi-Tenancy e Segurança:** Isolamento estrito por tenant (`IMustHaveTenant`), sem risco de vazamento de cobranças entre estabelecimentos.

## Decisões

### 1. Novo Módulo Vertical `Billing`
- Criados os projetos:
  - `CarWashSaaS.Billing.Domain`: Agregado raiz `PixCharge`, status (`Pending`, `Paid`, `Expired`, `Cancelled`) e regras invariantes de negócio.
  - `CarWashSaaS.Billing.Application`: Portas `IPixChargeRepository`, `IPixGatewayProvider`, `IWorkOrderPaymentLookup` e caso de uso `PixBillingApplicationService`.
  - `CarWashSaaS.Billing.Infrastructure`: `BillingDbContext` (schema `billing`), configurações EF Core com índices compostos `(TenantId, WorkOrderId)` e `(TenantId, TxId)`, repositório sem exposição de `IQueryable` e adaptadores de gateway.

### 2. Contratos Públicos Intermodulares (`CarWashSaaS.Shared.Contracts`)
- `IWorkOrderPaymentLookup`: Implementado no módulo `YardOperations` (`WorkOrderApplicationService`), provendo resumos seguros da OS (placa, cliente, telefone, valor devido e status) sem expor entidades internas ou DbContexts.
- `IPixBillingLookup`: Implementado por `PixBillingApplicationService`, possibilitando ao módulo `WhatsApp` consultar ou gerar cobranças ativas para atendimento do cliente.

### 3. Estratégia de Gateway Pix
- Interface `IPixGatewayProvider`:
  - `CreateImmediateChargeAsync(PixGatewayChargeRequest, CancellationToken)`: Retorna `TxId`, `QrCodeBase64`, `CopyPasteKey` e data de expiração.
- Implementações:
  - `SimulatedPixGatewayProvider`: Gera payload EMV compatível com o padrão do Banco Central (GUI `br.gov.bcb.pix`, tags TLV e CRC16) e imagem vetorial SVG de alta definição codificada em Base64.
  - `MercadoPagoPixGatewayProvider`: Integração direta com a API REST de Pagamentos Pix do Mercado Pago (`POST /v1/payments`) com chave de idempotência e fallback para provedor simulado na ausência de credenciais em desenvolvimento.

### 4. Ciclo de Vida da Cobrança Pix (`PixCharge`)
- Se uma cobrança ativa e pendente já existir com o mesmo valor da OS, o sistema reaproveita o mesmo código Pix dentro da janela de validade (idempotência).
- Se o valor da OS for alterado após a emissão da cobrança, a anterior é cancelada e uma nova com o valor atualizado é emitida.
- Cobranças expiradas são invalidadas automaticamente para prevenir recebimentos com dados defasados.

### 5. Frontend Blazor WebAssembly
- Componente dedicado `WorkOrderPixModal.razor`:
  - Exibição de resumo do veículo, cliente e valor total com destaque visual.
  - Renderização responsiva do QR Code.
  - Campo de código Copia e Cola com botão de cópia acionando a Clipboard API via JSInterop com feedback visual interativo.
  - Botão de despacho via WhatsApp integrado, exibindo data/hora do último envio.
- Integração no Kanban operacional (`YardKanbanCard.razor` e `YardKanbanBoard.razor`) com botão de atalho `💠 Pix`.

### 6. Atendimento Conversacional no Chatbot
- No `ChatbotConversationEngine`, interceptação proativa de termos como "PIX", "PAGAR", "PAGAMENTO", "SEGUNDA VIA":
  - Localiza a ordem de serviço ativa do cliente pelo número de telefone remetente.
  - Obtém ou emite a cobrança Pix vinculada.
  - Responde diretamente na conversa com o valor, código Copia e Cola e instrução de validação.

## Consequências

- **Positivas:**
  - Desacoplamento arquitetural absoluto: o módulo `YardOperations` não conhece regras financeiras e `Billing` acessa dados da OS estritamente por contratos tipados.
  - Flexibilidade de gateways bancários sem impacto no domínio da aplicação.
  - Redução drástica do atrito no balcão e no recebimento via autoatendimento no WhatsApp.
  - Total conformidade com testes de arquitetura (NetArchTest), testes unitários, integração multi-tenant e componentes bUnit.

- **Mitigações de Risco:**
  - Cobranças possuem TTL (tempo de vida) controlado para evitar discrepâncias em alterações de serviços na OS.
  - Todas as queries do `BillingDbContext` aplicam Global Query Filters automáticos com `TenantId`.
