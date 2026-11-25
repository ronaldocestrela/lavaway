# ADR-0034: Gerador de Links de Campanhas com Rastreamento UTM e QR Code no Backoffice

## Status
Aceito

## Contexto
Para acelerar a aquisição de novos estabelecimentos e mensurar a eficácia de canais de marketing (feiras, anúncios no Meta/Google, panfletos, parcerias locais e WhatsApp), o time executivo e de suporte da plataforma Lavaway necessita de uma ferramenta centralizada no Backoffice (`/platform/campaigns`) para:
1. Gerar links com parâmetros padrão da indústria (`utm_source`, `utm_medium`, `utm_campaign`, `utm_term`, `utm_content`).
2. Disponibilizar URLs curtas e amigáveis (`/c/{slug}`) com contabilização agregada de acessos/cliques.
3. Emitir QR Codes em alta definição (SVG vetorial para gráfica/impressão e PNG para canais digitais).
4. Assegurar conformidade total com a LGPD e o manual arquitetural `agents.md` (Monolito Modular, Arquitetura Hexagonal, padrão `Result<T>`, isolamento de domínio, `BrazilTime`, auditoria administrativa e ausência de coleta invasiva de dados pessoais no clique).

## Decisões de Arquitetura

### 1. Alocação Modular no Módulo `Tenants`
- O gerenciamento de campanhas de aquisição da plataforma é um recurso corporativo (Backoffice Global) gerido pelos operadores da plataforma.
- Para manter a coesão sem introduzir o custo de um novo módulo separado (novos projetos `.csproj`, novo pipeline de migrations e infraestrutura duplicada), o agregado `CampaignLink` é incorporado ao módulo `Tenants`.
- Como `CampaignLink` não pertence a um estabelecimento específico, ele **não** implementa `IMustHaveTenant` e opera sob as políticas de autorização de plataforma (`PlatformAuthorizationPolicyNames`).

### 2. Geração de QR Code com Biblioteca Puramente Gerenciada (`QRCoder`)
- Para gerar QR Codes reais compatíveis com leitores de smartphones em ambientes Docker Linux sem dependência do subsistema gráfico legado `libgdiplus`/`System.Drawing`:
  - Adota-se o pacote NuGet `QRCoder`.
  - A geração é encapsulada pela porta `IQrCodeGenerator` no Application layer e implementada via `SvgQRCode` e `PngByteQRCode` na camada de Infrastructure.

### 3. Redirecionamento Curto (`/c/{slug}`) e Segurança Anti-Open Redirect
- O endpoint de redirecionamento resolve o slug, valida se a campanha está ativa e dentro do período de vigência (`ExpiresAtUtc`), incrementa atomicamente o contador `ClickCount` e executa um redirecionamento HTTP 302 temporário para a rota de destino com os parâmetros UTM anexados.
- **Proteção Anti-Open Redirect:** O caminho de destino (`DestinationPath`) deve obrigatoriamente iniciar com barra (`/`), sendo restrito ao domínio oficial da plataforma, prevenindo ataques de phishing que utilizem o encurtador oficial como vetor.

### 4. Rastreamento e Conformidade LGPD
- O clique registra apenas a métrica agregada quantitativa (`ClickCount`) e o timestamp da última interação.
- Não são armazenados endereços IP, User-Agent ou fingerprints do usuário no banco relacional da campanha, delegando a telemetria comportamental ao Google Analytics 4 sob o Google Consent Mode v2 já padronizado na `ADR-0029`.

### 5. Auditoria Administrativa
- Todas as operações de criação, alteração cadastral ou pausa/reativação de campanhas disparam eventos de auditoria gravados na tabela `AdministrativeAuditEvents` via `AuditTrailApplicationService`.

## Consequências
- **Positivas:**
  - Capacidade nativa de geração e impressão de materiais de marketing e QR Codes diretamente do painel corporativo.
  - Métricas limpas de conversão integradas com GA4 e contadores de cliques em tempo real.
  - Arquitetura limpa, testável em TDD com BUnit e xUnit.
- **Mitigações:**
  - Roteamento reverso no Nginx configurado com `location /c/` para direcionar requisições curtas ao backend sem passar pelo SPA Blazor.
