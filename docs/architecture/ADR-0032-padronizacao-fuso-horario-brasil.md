# ADR-0032: Padronização do Fuso Horário Oficial do Brasil (America/Sao_Paulo) em Produção e Aplicação

## Status
Aceito

## Contexto
Quando o Lavaway SaaS é implantado em servidores VPS internacionais ou em containers Docker padrão (que operam em UTC `+00:00` ou no fuso horário do datacenter estrangeiro), ocorriam desvios operacionais de 3 horas:
1. **Agendamentos e Chatbot WhatsApp:** Chamadas a `DateTime.Today` e `DateTimeOffset.Now` faziam o dia virar às 21h00 do Brasil (`00:00 UTC`) e bloqueavam horários vagos do fim da tarde (ex.: às 14h00 no Brasil o servidor em UTC considerava 17h00).
2. **Lembretes de 24h e 2h:** O cálculo de janela usando `.LocalDateTime` no servidor disparava avisos de WhatsApp com 3 horas de deslocamento.
3. **Comprovantes PDF e Mensagens WhatsApp:** O uso de `.ToLocalTime()` no Backend imprimia horários de entrada e previsão de conclusão em UTC no PDF e na legenda enviada ao cliente.
4. **Caixa Diário, Comissões e Métricas:** Consultas que convertiam `DateOnly` usando `DateTimeKind.Utc` (`00:00Z` a `23:59Z`) recortavam o dia comercial às 21h00 de Brasília, lançando pagamentos noturnos no caixa do dia seguinte.

## Decisão de Arquitetura

Adotamos uma estratégia de **Defesa em Profundidade** em duas camadas:

### 1. Utilitário Centralizado `BrazilTime` (`CarWashSaaS.Shared.Contracts`)
Criada a classe estática `BrazilTime` em `CarWashSaaS.Shared.Contracts`, compartilhada por todos os módulos do Backend e pelo Frontend Blazor WebAssembly:
* Opera sempre com offset padrão de Brasília (`UTC-03:00` / `America/Sao_Paulo`), imune ao fuso horário configurado no sistema operacional hospedeiro ou no navegador do cliente.
* **Propriedades e Métodos Principais:**
  * `BrazilTime.Now` (`DateTimeOffset`), `BrazilTime.Today` (`DateOnly`) e `BrazilTime.CurrentTime` (`TimeOnly`).
  * `ToBrazilTime(this DateTimeOffset)` e `ToBrazilDateOnly(this DateTimeOffset)`: conversão explícita de timestamps UTC para o horário de Brasília na geração de PDFs, mensagens de WhatsApp, lembretes e componentes Razor.
  * `StartOfDayUtc(this DateOnly)` e `EndOfDayUtc(this DateOnly)`: convertem um dia comercial brasileiro (`00:00:00 -03:00` até `23:59:59.9999999 -03:00`) para a janela exata em UTC (`03:00:00Z` do dia até `02:59:59.9999999Z` do dia seguinte), utilizada em `CashTransactionRepository` e `CommissionApplicationService`.

### 2. Persistência em UTC + Exibição e Recorte Comercial em `BrazilTime`
* **Armazenamento:** Todos os carimbos de tempo (`CreatedAtUtc`, `OccurredAtUtc`, `PaidAtUtc`, `PickedUpAtUtc`, etc.) continuam sendo persistidos estritamente em **UTC (`DateTimeOffset.UtcNow`)** no SQL Server.
* **Regras de Dia Comercial e Exibição:** Qualquer operação que dependa do conceito de "data de hoje", "horário comercial atual", "início/fim do dia de caixa" ou formatação visual para o usuário final utiliza obrigatoriamente `BrazilTime`.

### 3. Infraestrutura Docker (`Dockerfile.*` e `docker-compose*.yml`)
* Instalação do pacote `tzdata` e configuração de `/etc/localtime` para `America/Sao_Paulo` em `Dockerfile.api` e `Dockerfile.migrator`.
* Definição de `TZ=America/Sao_Paulo` em todos os serviços dos arquivos `docker-compose.prod.yml`, `docker-compose.yml`, `.env.prod.example` e `.env.example` (incluindo `api`, `db-migrator`, `web`, `sqlserver`, `rabbitmq`, `minio`, `evolution-api`, `evolution-postgres` e `evolution-redis`).

## Consequências
* **Positivas:**
  * Independência total da localização geográfica da VPS ou provedor de nuvem.
  * Fechamento de caixa, apuração de comissões, cotas diárias e métricas respeitam fielmente a meia-noite no horário de Brasília.
  * Comprovantes em PDF, notificações de WhatsApp, lembretes de 24h/2h e telas do Blazor exibem sempre o horário oficial do Brasil.
* **Regras de Manutenção:**
  * É proibido o uso de `DateTime.Now`, `DateTime.Today`, `DateTimeOffset.Now`, `.ToLocalTime()` ou `.LocalDateTime` no código da solução; utilize sempre `DateTimeOffset.UtcNow` para persistência de instantes e `BrazilTime` para fuso brasileiro.

