# Especificação Viva: Campanhas de Aquisição, Links UTM e QR Code no Backoffice (6.7)

## Visão Geral
Esta especificação viva descreve os requisitos de negócio e os comportamentos executáveis para o módulo de campanhas corporativas no Backoffice da plataforma Lavaway.

## Fluxo de Criação e Rastreamento de Campanhas

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Administrador Backoffice
    participant UI as Blazor (/platform/campaigns)
    participant API as PlatformCampaignsEndpoints
    participant Service as CampaignLinkApplicationService
    participant Repo as ICampaignLinkRepository
    participant Audit as AuditTrailApplicationService
    actor Lead as Lead / Visitante

    Admin->>UI: Cadastra nova campanha (Nome, Slug, Destino, UTMs)
    UI->>API: POST /platform/campaigns
    API->>Service: CreateCampaignAsync(request, actorId)
    Service->>Repo: ExistsBySlugAsync(slug)
    Service->>Repo: AddAsync(campaign)
    Service->>Audit: RecordEventAsync("CreateCampaignLink", ...)
    Service-->>API: Result.Success(CampaignLinkDto)
    API-->>UI: 201 Created

    Admin->>UI: Solicita exportação do QR Code
    UI->>API: GET /platform/campaigns/{id}/qrcode?format=svg
    API->>Service: GenerateQrCodeAsync(id, format)
    Service-->>API: Svg Markup / Png Bytes
    API-->>UI: 200 OK (SVG/PNG)

    Lead->>API: GET /c/{slug}
    API->>Service: ResolveRedirectTargetAsync(slug)
    Service->>Repo: IncrementClickCountAsync(id)
    Service-->>API: Result.Success(redirectUrlWithUtm)
    API-->>Lead: 302 Found (Location: /landing?utm_source=...)
```

## Cenários BDD Executáveis

### Cenário 1: Criação com sucesso de link de campanha com slug amigável
- **Dado** que o operador possui perfil `SuperAdmin` ou `PlatformSupport`
- **Quando** enviar uma requisição para criar campanha com slug `black-friday` e destino `/#planos`
- **Então** o registro deve ser persistido com status ativo, `ClickCount = 0` e identificador `UUID`
- **E** um evento de auditoria administrativa deve ser registrado com a ação `CreateCampaignLink`.

### Cenário 2: Rejeição de slug duplicado
- **Dado** que já existe uma campanha cadastrada com o slug `expomec-2026`
- **Quando** o operador tentar criar outra campanha com o mesmo slug `expomec-2026`
- **Então** a operação deve falhar retornando `Result.Failure` do tipo `Conflict` (`campaign.slug_already_exists`).

### Cenário 3: Validação de segurança anti-open redirect
- **Dado** que o operador preenche um destino inválido como `https://site-malicioso.com`
- **Quando** tentar cadastrar a campanha
- **Então** a entidade de domínio deve rejeitar o destino com erro de validação, exigindo caminho relativo iniciado por `/`.

### Cenário 4: Resolução de link curto com parâmetros UTM
- **Dado** uma campanha ativa com slug `parceria-posto` e parâmetros UTM definidos
- **Quando** uma requisição pública acessar `/c/parceria-posto`
- **Então** a resposta deve ser HTTP 302 Found para o destino contendo a query string UTM completa
- **E** o contador de cliques deve ser incrementado atomicamente.
