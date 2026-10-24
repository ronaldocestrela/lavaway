# ADR-0027: Tela de Carregamento WebAssembly (Splash Screen) e Isolamento de Descoberta SEO/LLMs

## Status
Aceito

## Contexto
Ao acessar o sistema pela primeira vez, o runtime do Blazor WebAssembly baixa os binários (`.wasm`, `.dll` e dependências). Durante esse período de inicialização, o navegador exibe o conteúdo contido dentro da tag raiz `<div id="app">` em `index.html`.

Anteriormente, um bloco de pré-renderização semântica de SEO contendo botões como "Contexto para Agentes de IA (llms.txt)" e listas de recursos foi inserido diretamente no corpo do `#app` sem encapsulamento `<noscript>` ou ocultação visual. Isso fazia com que usuários humanos visualizassem textos técnicos direcionados a crawlers e LLMs em vez de uma tela de carregamento profissional e amigável.

## Decisão

1. **Separação Rigorosa de Públicos (Humano vs. Máquina/IA):**
   - Agentes de IA e crawlers de busca (GPTBot, ClaudeBot, Googlebot, etc.) realizam descoberta via metadados padrão:
     - Header tag `<link rel="alternate" type="text/plain" href="https://lavaway.com.br/llms.txt" title="LLMs Context" />`
     - Arquivo padrão `robots.txt` com rotas `Allow: /llms.txt`
     - Estrutura Schema.org JSON-LD completa no `<head>`
     - Conteúdo semântico de fallback encapsulado estritamente em `<noscript>` para navegadores ou crawlers sem suporte a JavaScript.
   - O usuário humano nunca deve visualizar botões ou referências diretas a arquivos de IA como `llms.txt` durante o ciclo de vida interativo da aplicação.

2. **Splash Screen Premium no Design System Lavaway:**
   - Implementação de um componente de carregamento visual dentro de `#app` seguindo as diretrizes do `DESIGN.md`:
     - Fundo escuro base institucional `#0B1514` com glow ambiente suave.
     - Card em glassmorphism com cantos de 20px, borda translúcida e desfoque de fundo.
     - Identidade visual com o logotipo Lavaway, wordmark e tipografia institucional (Manrope, DM Sans e IBM Plex Mono).
     - Barra de progresso dinâmica responsiva às variáveis nativas do Blazor WebAssembly (`--blazor-load-percentage` e `--blazor-load-percentage-text`) com animação de shimmer.
     - Mensagens acolhedoras e informativas de status ("Iniciando aplicação...", "Preparando pátio, recepção e inteligência...").

## Consequências
- Primeira impressão visual de alto impacto alinhada com a identidade da marca, eliminando qualquer flash de tela branca ou texto quebrado.
- Eliminação total de ruído cognitivo para o usuário final sobre arquitetura de LLMs ou arquivos de descoberta de IA.
- Preservação integral das capacidades de indexação por motores de busca e rastreadores de inteligência artificial via cabeçalhos e `<noscript>`.
