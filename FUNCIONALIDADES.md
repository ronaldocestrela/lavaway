# Funcionalidades do Lavaway (Lava Aê)

Catálogo do que o sistema faz hoje para a gestão de lava-jatos e estética automotiva. O Lavaway é um SaaS multiestabelecimento: cada loja opera isolada, com painel próprio, e a equipe da plataforma administra os assinantes em um backoffice separado.

Data de referência: 23 de outubro de 2026.

---

## 1. O que o sistema cobre

O fluxo vai do cadastro da loja até a retirada do veículo e o retorno do cliente:

1. O gestor configura a loja, os preços, a equipe, o WhatsApp e o recebimento.
2. A recepção localiza o cliente, abre a ordem de serviço e faz a vistoria.
3. O pátio acompanha o veículo em um quadro Kanban até ficar pronto.
4. O cliente recebe comprovante, aviso de retirada, Pix e pesquisa pelo WhatsApp.
5. O caixa fecha o dia, apura comissões e acompanha fidelidade e clubes.
6. A plataforma cobra a assinatura do SaaS, monitora as lojas e audita o suporte.

---

## 2. Quem usa o sistema

### Equipe da loja

| Perfil | O que pode fazer |
|---|---|
| Administrador da loja | Tudo da unidade: perfil, preços, equipe, comissões, WhatsApp, plano, pagamentos, caixa, relatórios e usuários. |
| Recepcionista | Abrir ordem de serviço, cadastrar clientes e veículos, vistoriar, agendar, receber no caixa e enviar comprovantes. |
| Operador / lavador | Ver o pátio, mover cards, assumir serviços, registrar refação e fotos de antes e depois. |

Permissões da loja: configurar a loja, gerir usuários, gerir ordens, criar ordens, atualizar status, ver clientes e ver relatórios.

### Equipe da plataforma

| Perfil | O que pode fazer |
|---|---|
| SuperAdmin | Tudo do backoffice: lojas, suporte assistido, faturamento, auditoria, usuários da plataforma e métricas. |
| Suporte da plataforma | Listar lojas, entrar em sessão de diagnóstico, ver auditoria e métricas. |
| Administrador de faturamento | Gerir planos e assinaturas do SaaS, ver auditoria e métricas. |
| Auditor | Consultar a trilha de auditoria e as métricas. |

O acesso da plataforma é separado do acesso das lojas. Um operador da plataforma só entra nos dados de uma loja por sessão de diagnóstico, com justificativa e registro na auditoria.

### Cliente final

O cliente não entra no painel. Ele conversa com o WhatsApp da loja para agendar, confirmar, remarcar, cancelar, pedir o Pix, consultar selos e responder a pesquisa de satisfação.

---

## 3. Telas do sistema

### Site e acesso

| Tela | Rota | Função |
|---|---|---|
| Landing page | `/` | Apresentação do produto, demonstração, comparação com operação em papel, calculadora de retorno, planos e contratação com período de teste. |
| Login | `/login` | Entrada da equipe da loja e da plataforma. |

### Operação da loja

| Tela | Rota | Função |
|---|---|---|
| Recepção | `/reception` | Busca de cliente e veículo por placa ou telefone e início do atendimento. |
| Nova ordem de serviço | `/work-orders/new` | Check-in com serviços, porte, valor e previsão de entrega. |
| Vistoria | `/work-orders/{id}/inspection` | Checklist, avarias no diagrama e fotos de perímetro. |
| Pátio e Kanban | `/yard` | Quadro operacional em tempo real. |
| Clientes | `/customers` | Cadastro e consulta de clientes e veículos. |
| Agenda e reservas | `/scheduling` | Agenda do dia, vagas, confirmação e remarcação. |
| Pós-venda e retenção | `/after-sales` | Pesquisas, campanhas e consentimento de mensagens. |
| Caixa e comissões | `/cashier` | Recebimentos, sangria, aporte, fechamento e comissões. |
| Fidelidade e selos | `/loyalty` | Cartão de selos, resgate e extrato. Disponível a partir do plano Pro. |
| Assinaturas e clubes | `/subscriptions` | Planos mensais do cliente final e créditos por placa. Disponível a partir do plano Pro. |

### Configuração da loja (somente administrador)

| Tela | Rota | Função |
|---|---|---|
| Perfil da loja | `/settings/profile` | Dados cadastrais, logomarca e cores. |
| Serviços e preços | `/settings/services` | Catálogo, duração e preço por porte. |
| Capacidade e equipe | `/settings/team` | Boxes, colaboradores e regras de comissão. |
| WhatsApp | `/settings/whatsapp` | Pareamento, teste de mensagem, cota e histórico. |
| Meu plano e cotas | `/settings/subscription` | Plano SaaS, consumo, faturas e portal de cobrança. |
| Pagamentos e gateways | `/settings/payments` | Conta Pagar.me ou Mercado Pago da loja. |

### Backoffice da plataforma

| Tela | Rota | Função |
|---|---|---|
| Auditoria | `/platform/audit` | Trilha de ações administrativas. |
| Estabelecimentos | `/platform/tenants` | Ciclo de vida das lojas e sessão de diagnóstico. |
| Faturamento e planos | `/platform/billing` | Planos Básico, Pro e Enterprise e assinaturas B2B. |
| Saúde do WhatsApp | `/platform/whatsapp` | Instâncias conectadas, quedas e alertas. |
| Métricas | `/platform/metrics` | MRR, ARR, churn, LTV, volume de veículos e Pix. |

---

## 4. Funcionalidades da loja

### 4.1 Cadastro e identidade da loja

- Assistente em etapas para razão social, nome fantasia, CNPJ, telefone e endereço.
- Validação e formatação de CNPJ e CEP.
- Esses dados entram no comprovante em PDF e nas mensagens automáticas.
- Upload da logomarca (PNG, JPG ou WebP, até 2 MB), guardada no espaço privado da loja.
- Cor primária e cor secundária, com pré-visualização no comprovante e no cartão de acompanhamento.
- Consulta e atualização do perfil depois do cadastro inicial.

### 4.2 Catálogo de serviços e preços

- Cadastro e edição de serviços, com categorias como ducha, lavagem completa, higienização, polimento e vitrificação.
- Duração estimada em minutos, usada na previsão de entrega e na ocupação da agenda.
- Preço diferente por porte:
  - Hatch / Sedan
  - SUV
  - Picape / Van
  - Moto
- Marcação dos serviços elegíveis para galeria comparativa de antes e depois (polimento, vitrificação e higienização de bancos).
- O preço e a duração ficam gravados na ordem no momento da abertura, então uma alteração posterior do catálogo não muda ordens já abertas.

### 4.3 Capacidade do pátio e equipe

- Quantidade de boxes simultâneos. Esse número trava a agenda quando o horário já está lotado.
- Cadastro de colaboradores com nome, telefone e papel.
- Ativação e desativação de colaboradores.
- Regras de comissão por serviço ou por cargo:
  - percentual sobre o item
  - valor fixo
- Limites de colaboradores e de boxes conforme o plano SaaS (Básico: 3 pessoas e 2 boxes; Pro: 10 e 5; Enterprise: sem limite).

### 4.4 Clientes e veículos

- Busca por placa (padrão Mercosul e padrão antigo) e por telefone.
- Reaproveitamento do cadastro existente para não duplicar cliente ou veículo.
- Cadastro rápido na recepção, com o primeiro veículo junto do cliente.
- Inclusão de veículos adicionais no mesmo cliente.
- Listagem e edição de clientes e veículos.
- A agenda pode abrir a recepção já com placa e telefone preenchidos.

### 4.5 Abertura da ordem de serviço

- Seleção do cliente, do veículo e do porte.
- Seleção de um ou mais serviços, com quantidade.
- Cálculo imediato do valor total, do tempo total e da previsão de conclusão.
- Observações de check-in.
- Status inicial: Aguardando.
- Número e dados da ordem ficam disponíveis para vistoria, Kanban, PDF, Pix e notificações.

### 4.6 Vistoria digital de entrada

Feita no celular ou no tablet, vinculada à ordem:

- Checklist de pertences e de combustível, com situação ok, ausente, avariado ou não se aplica.
- Diagrama do veículo em cinco vistas (superior, frente, traseira, lateral esquerda e lateral direita).
- Marcação de avarias com posição no desenho: arranhão, mossa, trinca, lasca de pintura ou outro, com gravidade baixa, média ou alta.
- Fotos com a câmera do aparelho. Para concluir a vistoria são obrigatórias as quatro fotos de perímetro: frente, traseira, lateral esquerda e lateral direita. Também é possível registrar interior, detalhe de avaria e outras fotos.
- As fotos ficam privadas, no armazenamento da própria loja.
- A vistoria concluída alimenta o comprovante em PDF.

### 4.7 Pátio em tempo real (Kanban)

Quadro com cinco colunas, atualizado para toda a equipe da loja:

1. Aguardando
2. Em lavagem
3. Secagem e acabamento
4. Controle de qualidade
5. Pronto para retirada

- Avanço e retorno por clique ou arrastar e soltar. A ordem das transições é validada.
- Atribuição e retirada de operador em cada veículo.
- Refação com justificativa obrigatória e registro no histórico.
- Histórico imutável de cada mudança de status.
- Busca por placa ou cliente e filtro por operador.
- Medidor de ocupação dos boxes em relação à capacidade configurada.
- Ações rápidas no card: Pix, pagamento, fotos de antes e depois, aviso de veículo pronto e registro de retirada.

### 4.8 Fotos depois do serviço

- Upload de fotos de conclusão para serviços elegíveis, associadas ou não às fotos de entrada.
- Galeria comparativa com slider de antes e depois e visualização lado a lado.
- Envio da galeria para o WhatsApp do cliente.
- Limite de 10 MB por imagem (JPEG, PNG ou WebP).

### 4.9 Comprovantes e avisos da ordem

- Geração do comprovante em PDF com dados da loja, cliente, veículo, serviços, odômetro, combustível, checklist e avarias.
- Download do PDF pela equipe.
- Envio do comprovante de entrada pelo WhatsApp.
- Aviso automático ou manual de veículo pronto quando a ordem chega em Pronto para retirada.
- Consulta do que já foi enviado em cada ordem.

### 4.10 Agenda e reservas

- Painel do dia e do dia seguinte, com filtro por status.
- Medidor do pico de boxes ocupados.
- Criação manual de reserva no balcão.
- Consulta de horários vagos calculados pela duração dos serviços e pela quantidade de boxes.
- Protocolo da reserva no formato `#BK-AAMM-XXXX`.
- Status: agendada, confirmada, cliente chegou, concluída, cancelada e falta.
- Confirmação, remarcação e cancelamento pela equipe.
- Reenvio pontual do lembrete.
- Botão para iniciar o atendimento e abrir a ordem com os dados da reserva.
- Origem registrada: balcão ou WhatsApp.

O agendamento pelo cliente, os lembretes e as respostas estão na seção 5.

### 4.11 Pix da ordem de serviço

- Geração de cobrança Pix do valor devido, com QR Code e código copia e cola.
- A cobrança fica vinculada à ordem.
- Envio do Pix pelo WhatsApp, a pedido da equipe ou quando o cliente escreve PIX, PAGAR ou SEGUNDA VIA.
- Provedores: Pagar.me (conta da própria loja), Mercado Pago e um provedor simulado para desenvolvimento.
- Cada loja guarda as próprias credenciais, criptografadas. A tela mostra a chave mascarada, testa a conexão e copia a URL exclusiva de webhook.
- O webhook do gateway confirma o pagamento uma única vez. Pagamentos repetidos ou com assinatura inválida são descartados.
- Depois da confirmação, a ordem é baixada, o caixa registra a entrada, o Kanban mostra o selo de pago e o cliente recebe a confirmação no WhatsApp.
- Situações da cobrança: pendente, paga, expirada e cancelada.

### 4.12 Caixa diário

- Recebimento da ordem em Pix, dinheiro, cartão de crédito, cartão de débito ou crédito de assinatura.
- Cálculo de troco no recebimento em dinheiro.
- Aportes e sangrias manuais.
- Entrada automática quando o Pix é confirmado pelo gateway.
- Resumo do dia por forma de pagamento.
- Fechamento do caixa com conferência da gaveta: valor contado contra valor esperado, sobra ou falta, e justificativa.
- Histórico dos fechamentos anteriores.
- A tela de caixa é da recepção e do administrador.

### 4.13 Comissões

- Cálculo sobre itens de ordens concluídas e pagas.
- Aplicação das regras por serviço e por cargo (percentual ou valor fixo).
- Relatório por colaborador e por período, com o detalhe de cada item.
- A funcionalidade de comissões entra a partir do plano Pro.

### 4.14 Fidelidade (plano Pro e Enterprise)

- Programa de selos configurável pela loja.
- Crédito automático de selo quando o serviço elegível é concluído e pago, sem duplicar o lançamento da mesma ordem.
- Conta por cliente, com saldo, progresso e extrato (selos ganhos, resgates e ajustes).
- Aviso no WhatsApp quando falta um serviço para o benefício e quando a meta é atingida.
- Resgate registrado pela equipe.
- O cliente consulta o saldo escrevendo FIDELIDADE, SELOS ou PONTOS no WhatsApp.
- Painel com clientes ativos, total de selos, recompensas entregues e quem está perto do resgate.

### 4.15 Clubes de assinatura do cliente final (plano Pro e Enterprise)

São os planos mensais que a loja vende aos seus clientes, diferentes da mensalidade que a loja paga ao Lavaway.

- Catálogo de planos com preço, créditos do ciclo e limite de placas.
- Assinatura do cliente com cobrança recorrente no cartão.
- Placas autorizadas. Só a placa cadastrada usa o crédito.
- Consumo na liquidação da ordem, sem ultrapassar o saldo do ciclo.
- Histórico de uso com data, placa, serviço e ordem.
- Proteção contra consumir o mesmo crédito duas vezes na mesma ordem.
- O Kanban e o caixa reconhecem o veículo assinante e oferecem a baixa por crédito.
- Painel com assinaturas ativas, receita mensal estimada, créditos disponíveis e usos do mês.
- Consulta por placa e cancelamento da assinatura.

### 4.16 Pós-venda, satisfação e reativação

- Registro da retirada do veículo no Kanban.
- Uma hora depois da retirada, pesquisa de 1 a 5 estrelas no WhatsApp.
- Resposta por número ou emoji. Notas 4 e 5 recebem agradecimento. Notas 1 a 3 recebem um pedido de desculpas e sinal para a gerência.
- Painel de CSAT: nota média, taxa de resposta, distribuição de estrelas e lista de avaliações.
- Campanhas para clientes sem voltar há 15, 30 e 45 dias, com liga/desliga, texto da mensagem, prévia e disparo manual.
- Audiência separada entre quem pode receber e quem está bloqueado.
- Frequência mínima de 7 dias entre mensagens promocionais.
- Clientes com ordem aberta ou reserva futura ficam de fora da campanha.
- Consentimento: PARAR, SAIR ou STOP desligam as mensagens; QUERO religa. A equipe também altera o consentimento no painel.

### 4.17 WhatsApp da loja

- Conexão da instância da loja por QR Code, com atualização do status (conectado, conectando, desconectado).
- Desconexão e novo pareamento.
- Aviso na tela quando a conexão cai, com atalho para gerar outro QR Code.
- Mensagem de teste para validar o número.
- Fila de envio com nova tentativa em caso de falha e registro de cada tentativa.
- Cota diária e limite de mensagens por minuto, para reduzir risco de bloqueio do número.
- Histórico de mensagens com status de entrega.
- A cota mensal de mensagens segue o plano SaaS.

### 4.18 Plano SaaS da loja

- Consulta do plano atual, do preço e das cotas.
- Barras de consumo de ordens de serviço e de mensagens de WhatsApp no ciclo.
- Troca de plano.
- Contratação e upgrade pelo Stripe (cartão e portal do cliente) ou pelo fluxo simulado de Pix em desenvolvimento.
- Faturas e quitação de fatura em aberto.
- Aviso de carência de 5 dias em atraso. Depois disso a loja fica inadimplente e não abre ordem nem envia mensagem até o pagamento.
- Reativação automática quando o pagamento é confirmado.

| Plano | Preço mensal de referência | Ordens / ciclo | Mensagens / ciclo | Equipe | Boxes | Fidelidade, clubes, comissões e chatbot |
|---|---:|---:|---:|---:|---:|---|
| Básico | R$ 149 | 150 | 300 | 3 | 2 | Não |
| Pro | R$ 299 | 600 | 1.500 | 10 | 5 | Sim |
| Enterprise | R$ 599 | Ilimitado | Ilimitado | Ilimitado | Ilimitado | Sim |

O valor zero nas cotas do Enterprise significa sem limite. A landing page oferece teste de 14 dias na contratação.

---

## 5. O que o cliente faz pelo WhatsApp

O atendimento é um fluxo guiado. A qualquer momento o cliente pode escrever `menu` para voltar ao início.

Menu principal:

1. Agendar um serviço.
2. Ver catálogo e preços.
3. Consultar agendamentos.

No agendamento, o robô:

- confirma ou pede o nome;
- mostra os serviços;
- pede o porte do veículo;
- pede a data e oferece só horários com box livre;
- usa um veículo já cadastrado ou pede a placa;
- pede confirmação e grava a reserva com protocolo.

Antes do horário:

- lembrete 24 horas antes;
- lembrete 2 horas antes;
- o cliente responde 1 para confirmar, 2 para remarcar ou 3 para cancelar;
- a remarcação só aceita horário com vaga;
- o cancelamento libera o box na hora.

Fora do menu, estas palavras também funcionam:

- PIX, PAGAR, PAGAMENTO ou SEGUNDA VIA: devolve o Pix da ordem ativa daquele telefone.
- FIDELIDADE, PONTOS, SELOS ou CARTAO FIDELIDADE: informa o saldo de selos.
- Nota de 1 a 5 depois da pesquisa: registra a avaliação.
- PARAR, SAIR ou STOP: descadastra mensagens promocionais.
- QUERO: volta a autorizar.

Mensagens enviadas pela própria loja não entram de novo no robô, para não criar loop.

---

## 6. Backoffice da plataforma

### 6.1 Lojas (tenants)

- Lista de estabelecimentos com status: em teste, ativo, inadimplente e cancelado.
- Filtros e indicadores por status.
- Alteração de status com justificativa e data.
- Prazo de teste registrado na loja.

### 6.2 Sessão de diagnóstico (impersonation)

- Suporte e SuperAdmin entram no contexto de uma loja para investigar um problema.
- É obrigatório informar a justificativa e a referência do chamado.
- A abertura e o encerramento ficam na auditoria.
- Enquanto a sessão está aberta, um aviso fixo no topo mostra a loja, o chamado e o operador, com botão para encerrar.
- Um usuário comum da loja não consegue usar esse mecanismo para ver outra loja.

### 6.3 Faturamento B2B

- Catálogo dos planos Básico, Pro e Enterprise, com edição de preço, cotas e recursos.
- Assinaturas das lojas e override administrativo quando necessário.
- Cobrança recorrente pelo Stripe: checkout, portal do cliente e webhooks assinados.
- Provedor simulado para desenvolvimento, com Pix e validação da assinatura do evento.
- Cada evento de webhook é processado uma única vez.
- Indicadores de receita recorrente mensal e contagem de assinantes por status.

### 6.4 Saúde do WhatsApp

- Painel de instâncias: total, online, offline e alertas ativos.
- Queda detectada na hora pelo webhook e também por verificação a cada 5 minutos.
- Alerta para o contato da loja com orientação para gerar um novo QR Code.
- Intervalo mínimo de 60 minutos entre alertas da mesma queda.
- Histórico de incidentes.
- Sondagem manual de uma instância e reenvio de alerta pelo suporte.

### 6.5 Métricas e observabilidade

Períodos de 7 dias, 30 dias, 90 dias e 12 meses:

- MRR, ARR, taxa de cancelamento e LTV.
- Veículos atendidos e concluídos, com série diária.
- Volume e valor transacionado via Pix, com ticket médio.
- Taxa de entrega do WhatsApp.
- Log de webhooks de pagamento, com inspeção do payload.
- Falhas de envio do WhatsApp, com telefone mascarado e orientação técnica.

### 6.6 Auditoria

- Registro somente de inclusão: o evento não é alterado nem apagado.
- Cada registro guarda autor, data e hora, ação, tipo e identificador do alvo, loja afetada, IP, navegador e um retrato dos dados.
- Tentativas bloqueadas também são registradas.
- Filtros por período, autor, ação, alvo, resultado e texto livre.
- Detalhe do evento com o conteúdo estruturado.

### 6.7 Usuários da plataforma

- Login separado do login das lojas.
- Cadastro e listagem de operadores da plataforma.

---

## 7. Segurança e isolamento

- Cada loja só lê e grava os próprios clientes, veículos, ordens, fotos, mensagens e pagamentos. O identificador da loja vem do token de acesso, não de um dado enviado pelo navegador.
- Sessão com token de acesso e refresh token, com rotação e detecção de reutilização. A sessão pode ser revogada.
- Arquivos (logo, fotos de vistoria, fotos pós-serviço e PDF) ficam em pastas separadas por loja.
- Credenciais de pagamento ficam criptografadas. Webhooks de pagamento e do WhatsApp têm a assinatura conferida antes de alterar qualquer cobrança ou mensagem.
- Cadastro de uma nova loja pelo fluxo público de registro, com usuário administrador inicial.

---

## 8. Automações que rodam sem a equipe

| Automação | Quando acontece |
|---|---|
| Lembrete de 24 h e de 2 h | Varredura a cada 5 minutos sobre reservas futuras. |
| Pesquisa de satisfação | 1 hora depois da retirada, verificação a cada 2 minutos. |
| Campanha de reativação | Clientes sem visita há 15, 30 ou 45 dias, respeitando o intervalo de 7 dias e o descadastro. |
| Confirmação de Pix | No recebimento do webhook válido do gateway. |
| Selo de fidelidade | Quando a ordem elegível fica pronta e paga. |
| Aviso de veículo pronto | Quando a ordem entra em Pronto para retirada. |
| Queda do WhatsApp | No evento de desconexão e na sondagem a cada 5 minutos. |
| Bloqueio por inadimplência | Depois de 5 dias de carência sem pagamento da mensalidade do SaaS. |
| Reativação da loja | Quando a fatura do SaaS é confirmada. |

---

## 9. Mapa rápido por objetivo

| Se a loja quer… | Onde está |
|---|---|
| Começar a operar | Perfil, serviços, equipe, WhatsApp e pagamentos |
| Atender quem chegou | Recepção, nova ordem e vistoria |
| Saber onde está cada carro | Pátio e Kanban |
| Marcar horário e reduzir falta | Agenda e o WhatsApp do cliente |
| Receber | Pix na ordem, caixa e fechamento |
| Pagar a equipe | Regras de comissão e relatório no caixa |
| Fazer o cliente voltar | Selos, clubes, pesquisa e campanhas de 15/30/45 dias |
| Proteger a vistoria | Fotos de perímetro, diagrama de avarias e PDF |
| Acompanhar a mensalidade do Lavaway | Meu plano e cotas |
| A plataforma acompanhar as lojas | Estabelecimentos, faturamento, WhatsApp, métricas e auditoria |
