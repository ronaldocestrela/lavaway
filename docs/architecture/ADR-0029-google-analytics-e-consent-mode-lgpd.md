# ADR-0029: Integração do Google Analytics (gtag.js) com Consent Mode v2 e Governança de Cookies em Conformidade com a LGPD

## Status
Aceito

## Contexto
A plataforma SaaS Lavaway necessita mensurar tráfego, audiência, tempos de resposta e uso de suas páginas públicas e fluxos de navegação através do Google Analytics 4 (**ID da Propriedade:** `G-FCCZJVXZG9`).

Contudo, sob a égide da **Lei Geral de Proteção de Dados Pessoais (LGPD — Lei nº 13.709/2018)**, das diretrizes do Guia Orientativo de Cookies da ANPD (Autoridade Nacional de Proteção de Dados) e do manual arquitetural `agents.md`:
1. **Consentimento Prévio Obrigatório (Art. 7º, I):** Cookies analíticos de terceiros não podem ser ativados de forma compulsória ou por mero silêncio/rolagem do visitante (vedação ao opt-out forçado).
2. **Revogação Facilitada e Gratuita (Art. 8º, § 5º):** O titular deve poder alterar ou revogar seu consentimento a qualquer momento com a mesma facilidade com que o concedeu.
3. **Minimização Estrita de Dados (Art. 6º, III):** Dados cadastrais e operacionais de clientes finais de lava-jatos (nomes, telefones, placas de veículos, fotos de vistoria) **nunca** podem ser expostos ou enviados para o provedor de analytics.
4. **Isolamento de Responsabilidade e Hexagonal no Frontend:** A gestão de consentimento e disparo de tags deve respeitar a separação em camadas (`Client.Core` para contratos/regras e `Client.Components`/`Client.Web` para interfaces e adaptadores de browser).

## Decisões de Arquitetura

### 1. Google Consent Mode v2 Nativo no `<head>` (`index.html`)
- O carregamento da tag oficial (`gtag.js`) é orquestrado com o **Google Consent Mode v2**.
- Antes do download do script do Tag Manager e antes de qualquer chamada a `gtag('config')`, o script verifica no armazenamento local (`localStorage.getItem('lavaway_cookie_consent_v1')`) se houve manifestação anterior do usuário.
- Se o usuário não tiver decidido ou tiver rejeitado, os parâmetros de consentimento são inicializados compulsoriamente como negados:
  ```javascript
  gtag('consent', 'default', {
      'analytics_storage': consentState, // 'denied' por padrão
      'ad_storage': 'denied',
      'ad_user_data': 'denied',
      'ad_personalization': 'denied',
      'wait_for_update': 500
  });
  ```
- O parâmetro `anonymize_ip: true` é habilitado de forma permanente no `gtag('config', 'G-FCCZJVXZG9', ...)`.
- É criado o namespace global `window.lavawayAnalytics` com métodos padronizados (`updateConsent`, `trackPageView`, `trackEvent`) para comunicação limpa via Blazor JSInterop.

### 2. Arquitetura de Portas e Adaptadores para Consentimento (`Client.Core` e `Client.Web`)
- **Porta (Contrato):** Criada a interface `ICookieConsentService` e o modelo `CookieConsentPreference` no núcleo `CarWashSaaS.Client.Core`, garantindo zero acoplamento com APIs proprietárias do navegador e facilitando injeção de dublês em testes unitários (`InMemoryCookieConsentService`).
- **Adaptador Browser:** Criado o serviço `LocalStorageCookieConsentService` em `CarWashSaaS.Client.Web`, que persiste as opções em `localStorage` sob a chave `lavaway_cookie_consent_v1` e sincroniza o estado dinamicamente com o `lavawayAnalytics.updateConsent`.

### 3. Banner Flutuante de Consentimento (`CookieConsentBanner.razor`)
- Desenvolvido o componente visual `CookieConsentBanner.razor` em `CarWashSaaS.Client.Components`, seguindo a identidade visual premium da plataforma (dark glassmorphism, paleta slate/emerald, acessibilidade ARIA e responsividade móvel).
- Oferece três vias de ação transparentes:
  - **"Aceitar Todos":** Concede consentimento para métricas analíticas e fecha o banner.
  - **"Apenas Necessários":** Nega cookies analíticos, preserva apenas os essenciais de sessão e fecha o banner.
  - **"Personalizar Escolhas":** Abre o painel granular categorizado (1. Necessários - Sempre Ativo; 2. Analíticos - Switch interativo).
- Integrado globalmente no arquivo raiz `App.razor`.

### 4. Rastreamento SPA (Single Page Application) no Blazor WebAssembly
- Como o Blazor WASM não recarrega o documento HTML a cada navegação de rota, o arquivo `App.razor` intercepta eventos de `NavigationManager.LocationChanged`.
- Caso o consentimento analítico esteja ativo (`Analytics == true`), dispara `lavawayAnalytics.trackPageView` mantendo a contagem de páginas precisa sem enviar dados pessoais.

### 5. Gestão e Revogação na Política de Privacidade (`PrivacyPolicyPage.razor`)
- A seção 4 da Política de Privacidade foi atualizada com transparência total: especificação do ID `G-FCCZJVXZG9`, base legal (Art. 7º, I da LGPD), Google Consent Mode v2 e minimização de dados.
- Adicionado o **Painel Interativo de Gestão de Cookies**, permitindo ao usuário consultar o status em tempo real e clicar no botão "Revogar Consentimento" a qualquer momento, atendendo com rigor ao Art. 8º, § 5º da LGPD.

## Consequências

- **Positivas:**
  - 100% de conformidade técnica e regulatória com a LGPD e orientações da ANPD.
  - Bloqueio preventivo automático: zero cookies e telemetria analítica sem aceite prévio.
  - O lojista e a plataforma contam com métricas reais de audiência no Google Analytics 4 com IP anonimizado.
  - Integração desacoplada e 100% testável com bUnit (`CookieConsentComponentTests.cs`).
  - Interface responsiva, com suporte total a desktops e smartphones.

- **Mitigações:**
  - Caso o usuário navegue com navegadores com bloqueadores agressivos de terceiros, o fallback gracioso em `LocalStorageCookieConsentService` e `InMemoryCookieConsentService` garante que a aplicação continue operando perfeitamente sem erros de JavaScript.
