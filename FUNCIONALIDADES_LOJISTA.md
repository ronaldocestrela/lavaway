# Funcionalidades do lojista — Lava Aê (Lava Aê)

O que a loja (tenant) usa no dia a dia: configuração, recepção, pátio, agenda, financeiro, fidelização e o WhatsApp do estabelecimento.

Data de referência: 23 de outubro de 2026.

---

## 1. Quem opera a loja

| Perfil | O que pode fazer |
|---|---|
| Administrador | Tudo da unidade: perfil, preços, equipe, comissões, WhatsApp, plano, pagamentos, caixa, relatórios e usuários. |
| Recepcionista | Abrir ordem de serviço, cadastrar clientes e veículos, vistoriar, agendar, receber no caixa e enviar comprovantes. |
| Operador / lavador | Ver o pátio, mover cards, assumir serviços, registrar refação e fotos de antes e depois. |

O cliente final não entra no painel. Ele fala com o WhatsApp da loja.

---

## 2. Telas da loja

### Operação

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
| Fidelidade e selos | `/loyalty` | Cartão de selos, resgate e extrato. A partir do plano Pro. |
| Assinaturas e clubes | `/subscriptions` | Planos mensais do cliente e créditos por placa. A partir do plano Pro. |

### Configuração (somente administrador)

| Tela | Rota | Função |
|---|---|---|
| Perfil da loja | `/settings/profile` | Dados cadastrais, logomarca e cores. |
| Serviços e preços | `/settings/services` | Catálogo, duração e preço por porte. |
| Capacidade e equipe | `/settings/team` | Boxes, colaboradores e regras de comissão. |
| WhatsApp | `/settings/whatsapp` | Pareamento, teste de mensagem, cota e histórico. |
| Meu plano e cotas | `/settings/subscription` | Plano do Lava Aê, consumo, faturas e portal de cobrança. |
| Pagamentos e gateways | `/settings/payments` | Conta Pagar.me ou Mercado Pago da loja. |

A entrada da equipe é em `/login`.

---

## 3. Configuração da unidade

### Perfil e identidade visual

- Razão social, nome fantasia, CNPJ, telefone e endereço, com validação de CNPJ e CEP.
- Esses dados entram no comprovante em PDF e nas mensagens automáticas.
- Logomarca (PNG, JPG ou WebP, até 2 MB).
- Cor primária e cor secundária, com prévia no comprovante.

### Serviços e preços

- Cadastro e edição de serviços (ducha, lavagem completa, higienização, polimento, vitrificação e outros).
- Duração estimada em minutos, usada na previsão de entrega e na agenda.
- Preço por porte: Hatch/Sedan, SUV, Picape/Van e Moto.
- Marcação dos serviços que aceitam galeria de antes e depois.
- O preço e a duração ficam gravados na ordem no momento da abertura.

### Pátio e equipe

- Quantidade de boxes simultâneos. A agenda não marca horário quando os boxes já estão lotados.
- Colaboradores com nome, telefone e papel, com ativação e desativação.
- Comissão por serviço ou por cargo: percentual ou valor fixo.
- Limites do plano: Básico até 3 pessoas e 2 boxes; Pro até 10 e 5; Enterprise sem limite.

### WhatsApp da loja

- Conexão por QR Code, com status conectado, conectando ou desconectado.
- Novo pareamento quando a conexão cai, com aviso na tela.
- Mensagem de teste para validar o número.
- Cota diária e limite de envios por minuto.
- Histórico de mensagens e do resultado de cada envio.
- A cota mensal segue o plano contratado.

### Recebimento da loja

- Credenciais da própria conta Pagar.me ou Mercado Pago, gravadas de forma criptografada e exibidas mascaradas.
- Teste de conexão com o gateway.
- URL exclusiva de webhook para a confirmação automática do Pix.

### Plano do Lava Aê

- Consulta do plano, do preço e do consumo de ordens e de mensagens.
- Troca de plano, faturas e quitação.
- Contratação e upgrade pelo Stripe, com portal do cliente.
- Cinco dias de carência em atraso. Depois disso a loja não abre ordem nem envia mensagem até o pagamento ser confirmado.

| Plano | Preço mensal de referência | Ordens / ciclo | Mensagens / ciclo | Fidelidade, clubes, comissões e chatbot |
|---|---:|---:|---:|---|
| Básico | R$ 149 | 150 | 300 | Não |
| Pro | R$ 299 | 600 | 1.500 | Sim |
| Enterprise | R$ 599 | Ilimitado | Ilimitado | Sim |

---

## 4. Recepção e ordem de serviço

### Clientes e veículos

- Busca por placa (Mercosul e padrão antigo) e por telefone.
- Reaproveitamento do cadastro para não duplicar cliente ou veículo.
- Cadastro rápido na recepção, com o primeiro veículo junto do cliente.
- Inclusão de outros veículos no mesmo cliente.
- A agenda pode abrir a recepção já com placa e telefone preenchidos.

### Abertura da ordem

- Seleção do cliente, do veículo e do porte.
- Um ou mais serviços, com quantidade.
- Valor total, tempo total e previsão de conclusão calculados na hora.
- Observações de check-in.
- A ordem nasce em Aguardando.

### Vistoria de entrada

No celular ou no tablet, vinculada à ordem:

- Checklist de pertences e de combustível: ok, ausente, avariado ou não se aplica.
- Diagrama em cinco vistas: superior, frente, traseira, lateral esquerda e lateral direita.
- Avarias no desenho: arranhão, mossa, trinca, lasca de pintura ou outro, com gravidade baixa, média ou alta.
- Fotos pela câmera. Para concluir são obrigatórias frente, traseira, lateral esquerda e lateral direita. Também dá para registrar interior, detalhe de avaria e outras fotos.
- As fotos ficam só no espaço da loja.
- A vistoria entra no comprovante em PDF.

### Comprovante

- PDF com dados da loja, cliente, veículo, serviços, odômetro, combustível, checklist e avarias.
- Download pela equipe e envio pelo WhatsApp.
- Consulta do que já foi enviado em cada ordem.

---

## 5. Pátio

Quadro em tempo real, com cinco etapas:

1. Aguardando
2. Em lavagem
3. Secagem e acabamento
4. Controle de qualidade
5. Pronto para retirada

- Avanço e retorno por clique ou arrastar e soltar.
- Operador atribuído a cada veículo.
- Refação com justificativa obrigatória e histórico de cada mudança.
- Busca por placa ou cliente e filtro por operador.
- Medidor de boxes ocupados.
- No card: Pix, pagamento, fotos de antes e depois, aviso de veículo pronto e registro de retirada.

### Fotos depois do serviço

- Fotos de conclusão nos serviços elegíveis, ligadas ou não às fotos de entrada.
- Comparativo com slider e lado a lado.
- Envio da galeria para o WhatsApp do cliente.

### Aviso de veículo pronto

- Disparo quando a ordem chega em Pronto para retirada, pelo quadro ou de forma automática.

---

## 6. Agenda

- Painel de hoje e de amanhã, com filtro por status.
- Medidor do pico de boxes ocupados.
- Reserva feita no balcão e reserva que chegou pelo WhatsApp.
- Horários vagos calculados pela duração dos serviços e pela quantidade de boxes.
- Protocolo no formato `#BK-AAMM-XXXX`.
- Status: agendada, confirmada, cliente chegou, concluída, cancelada e falta.
- Confirmação, remarcação, cancelamento e reenvio do lembrete pela equipe.
- Atalho para iniciar o atendimento e abrir a ordem com os dados da reserva.

### O que o cliente resolve sozinho no WhatsApp

- Agendar: nome, serviço, porte, data, horário livre e placa.
- Ver o catálogo e os preços.
- Consultar as reservas dele.
- Confirmar, remarcar ou cancelar a partir dos lembretes de 24 horas e de 2 horas.
- A remarcação só aceita horário com vaga. O cancelamento libera o box na hora.
- Escrever `menu` volta ao início.

---

## 7. Financeiro da loja

### Pix da ordem

- QR Code e copia e cola do valor da ordem.
- Envio pelo WhatsApp, pela equipe ou quando o cliente escreve PIX, PAGAR ou SEGUNDA VIA.
- Confirmação automática pelo gateway. A ordem é baixada, o caixa recebe a entrada, o Kanban mostra o selo de pago e o cliente recebe a confirmação.
- Situações: pendente, paga, expirada e cancelada.

### Caixa

- Recebimento em Pix, dinheiro, cartão de crédito, cartão de débito ou crédito de assinatura.
- Troco no dinheiro.
- Aporte e sangria.
- Resumo do dia por forma de pagamento.
- Fechamento com valor contado, valor esperado, sobra ou falta e justificativa.
- Histórico dos fechamentos.
- Tela da recepção e do administrador.

### Comissões

A partir do plano Pro.

- Cálculo sobre itens de ordens concluídas e pagas.
- Regras por serviço e por cargo, em percentual ou valor fixo.
- Relatório por colaborador e por período, com o detalhe de cada item.

---

## 8. Fidelização

### Selos

A partir do plano Pro.

- Programa configurável pela loja.
- Selo creditado quando o serviço elegível é concluído e pago, sem repetir a mesma ordem.
- Saldo, progresso e extrato por cliente: ganhos, resgates e ajustes.
- Aviso no WhatsApp quando falta um serviço para o benefício e quando a meta é atingida.
- Resgate feito pela equipe.
- O cliente consulta o saldo escrevendo FIDELIDADE, SELOS ou PONTOS.
- Painel com clientes ativos, total de selos, recompensas entregues e quem está perto do resgate.

### Clubes por placa

A partir do plano Pro. São os planos que a loja vende aos clientes, separados da mensalidade do Lava Aê.

- Planos com preço, créditos do ciclo e limite de placas.
- Cobrança recorrente no cartão.
- Só a placa cadastrada usa o crédito, e o consumo não passa do saldo do ciclo.
- Histórico com data, placa, serviço e ordem.
- O Kanban e o caixa reconhecem o veículo assinante e oferecem a baixa por crédito.
- Painel com assinaturas ativas, receita mensal estimada, créditos disponíveis e usos do mês.
- Consulta por placa e cancelamento.

---

## 9. Pós-venda

- Registro da retirada no Kanban.
- Uma hora depois, pesquisa de 1 a 5 estrelas no WhatsApp.
- Notas 4 e 5 recebem agradecimento. Notas 1 a 3 recebem pedido de desculpas e sinal para a gerência.
- Painel com nota média, taxa de resposta, distribuição de estrelas e lista de avaliações.
- Campanhas para quem não volta há 15, 30 e 45 dias, com texto, prévia, liga/desliga e disparo manual.
- Intervalo mínimo de 7 dias entre mensagens promocionais.
- Quem tem ordem aberta ou reserva futura fica de fora.
- PARAR, SAIR ou STOP desligam as mensagens; QUERO religa. A equipe também altera o consentimento no painel.

---

## 10. O que acontece sozinho

| Automação | Quando |
|---|---|
| Lembrete de agenda | 24 horas e 2 horas antes do horário. |
| Pesquisa de satisfação | 1 hora depois da retirada. |
| Campanha de reativação | Cliente sem visita há 15, 30 ou 45 dias. |
| Baixa do Pix | Quando o gateway confirma o pagamento. |
| Selo de fidelidade | Quando a ordem elegível fica pronta e paga. |
| Aviso de veículo pronto | Quando a ordem entra em Pronto para retirada. |
| Aviso de WhatsApp caído | Quando a conexão da loja desconecta. |
| Bloqueio por atraso do plano | Depois de 5 dias sem pagar a mensalidade do Lava Aê. |
| Reativação da loja | Quando essa mensalidade é confirmada. |

---

## 11. Onde achar cada coisa

| A loja quer… | Onde está |
|---|---|
| Configurar a unidade | Perfil, serviços, equipe, WhatsApp e pagamentos |
| Atender quem chegou | Recepção, nova ordem e vistoria |
| Ver onde está cada carro | Pátio e Kanban |
| Marcar horário e reduzir falta | Agenda e o WhatsApp do cliente |
| Receber | Pix na ordem, caixa e fechamento |
| Pagar a equipe | Regras de comissão e relatório no caixa |
| Fazer o cliente voltar | Selos, clubes, pesquisa e campanhas de 15/30/45 dias |
| Guardar a vistoria | Fotos de perímetro, diagrama de avarias e PDF |
| Ver a mensalidade do Lava Aê | Meu plano e cotas |
