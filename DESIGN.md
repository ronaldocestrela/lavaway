# Lavaway — Design Visual

Este documento registra as regras visuais da identidade do produto Lavaway, tomando como referência a landing page inicial. Ele orienta novas páginas, componentes e materiais digitais a manterem uma experiência reconhecível, consistente e acessível.

## 1. Essência da marca

**Posicionamento visual:** tecnologia operacional para lava-jatos e estética automotiva, com aparência profissional, precisa e contemporânea.

**Sensações a transmitir:**

- **Controle:** informação organizada, métricas legíveis e estados claros.
- **Agilidade:** hierarquia direta, chamadas para ação visíveis e pouca fricção.
- **Confiança:** contraste alto, superfícies sólidas e sinais de sucesso evidentes.
- **Premium automotivo:** base escura, verdes profundos e acentos luminosos sem excesso de efeitos.

A interface combina um ambiente escuro de alto contraste com o verde-lima da marca. O brilho e os gradientes são acentos, não decoração dominante.

## 2. Logotipo e assinatura

- Escrever o nome da marca em caixa baixa: **lavaway**.
- Quando houver espaço, combinar o wordmark com o emblema quadrado contendo a inicial **L**.
- O emblema usa fundo verde-lima, letra escura e cantos suavemente arredondados.
- A assinatura “SaaS”, quando necessária, é secundária e pode usar tipografia monoespaçada em tamanho reduzido.
- Em fundos escuros, usar o wordmark claro; preservar o contraste do emblema.
- Não redesenhar, inclinar ou aplicar gradientes ao logotipo fora dos tratamentos já definidos para a landing page.

## 3. Paleta de cores do produto

Os valores abaixo são os tons usados ou diretamente derivados da landing page. Manter os valores hexadecimais em maiúsculas ao reutilizá-los em documentação e tokens.

| Token sugerido | Cor | Uso |
|---|---|---|
| `brand-primary` | `#C6F277` | Cor principal de ação e destaque: CTA primário, links de ação, indicadores ativos, números-chave e pequenos acentos. |
| `brand-primary-hover` | `#D4F88E` | Hover de controles primários; usar apenas como estado interativo. |
| `brand-primary-soft` | `#DDF99C` | Destaque claro pontual sobre fundo escuro. |
| `brand-ink` | `#122421` | Texto e ícones sobre superfícies verde-lima; também usado em superfícies profundas. |
| `brand-green` | `#256B57` | Verde secundário para halos, ilustrações e apoio ao gradiente da marca. |
| `brand-mint` | `#90E0B3` | Tom de apoio para gradientes e elementos visuais secundários. |
| `background` | `#0B1514` | Fundo base da landing page e do shell institucional. |
| `background-section` | `#0D1918` | Alternância discreta entre seções escuras. |
| `background-section-alt` | `#0D1A19` | Segunda variação escura para separar áreas extensas. |
| `surface` | `#122421` | Cartões, controles e painéis elevados. |
| `surface-raised` | `#142A27` | Cartão em destaque, superfície selecionada ou estado visualmente elevado. |
| `surface-deep` | `#0F1F1D` | Painel mais profundo, por exemplo, resultados e áreas técnicas. |
| `text-primary` | `#FFFFFF` | Títulos e informação de maior prioridade. |
| `text-body` | `#C9DBD3` | Texto padrão sobre superfícies escuras. |
| `text-secondary` | `#9CB2A7` | Descrições, legendas e conteúdo secundário. |
| `text-muted` | `#728C81` | Metadados de baixa prioridade; não usar para texto essencial ou pequeno sem verificar contraste. |

### Regras de uso da cor

1. Usar `#0B1514` como base, não preto puro. Variar seções com verdes quase pretos para criar ritmo sem quebrar a identidade.
2. Reservar `#C6F277` para ações, foco visual e informação importante. Evitar grandes áreas preenchidas com essa cor.
3. Em fundo verde-lima, usar texto escuro (`#122421` ou equivalente) — nunca texto branco pequeno.
4. Usar texto branco para títulos e texto verde-claro acinzentado para leitura contínua. Não reduzir opacidade para comunicar hierarquia quando isso comprometer a legibilidade.
5. Contornos e divisórias em superfícies escuras devem ser discretos; a referência usa predominantemente branco com baixa opacidade.
6. Gradiente institucional de texto/destaque: `#C6F277` → `#E8FFD0` → `#90E0B3`. Aplicar em poucas palavras ou pequenos detalhes, mantendo conteúdo legível.
7. Verde não substitui cores semânticas de sucesso, alerta ou erro. Estados operacionais devem continuar distinguíveis por cor, rótulo e/ou ícone.

## 4. Tipografia

As fontes são carregadas em [index.html](src/Frontend/CarWashSaaS.Client.Web/wwwroot/index.html).

| Família | Papel | Aplicação |
|---|---|---|
| **Manrope** | Display e marca | Wordmark, títulos, CTAs e títulos de controles importantes. Pesos de referência: 700–800. |
| **DM Sans** | Interface e leitura | Texto corrido, navegação, formulários e conteúdo geral. Pesos de referência: 400–700. |
| **IBM Plex Mono** | Dados e linguagem técnica | Métricas, valores, etiquetas em caixa alta, códigos e números operacionais. Pesos de referência: 400–700. |

Usar fallbacks sans-serif do sistema se as fontes não estiverem disponíveis. Não usar a fonte monoespaçada para parágrafos longos.

### Hierarquia de texto

- **Título principal:** Manrope 800, fluido e responsivo; referência da LP: `clamp(36px, 5.5vw, 62px)`, line-height próximo de `1.1`.
- **Título de seção:** Manrope 800; referência: `clamp(28px, 4vw, 42px)`, line-height próximo de `1.15`.
- **Corpo de destaque:** DM Sans 16–19px, line-height entre `1.6` e `1.7`.
- **Corpo padrão:** DM Sans; priorizar leitura confortável e hierarquia clara.
- **Eyebrow/etiqueta:** IBM Plex Mono, 9–11px, peso 700, caixa alta e espaçamento entre letras moderado.
- **Métricas:** IBM Plex Mono, peso 800; os valores recebem o acento verde-lima e os rótulos ficam em tom secundário.

## 5. Layout e composição

- Usar conteúdo centralizado com largura máxima de aproximadamente **1200px** e respiro lateral de **24px** como padrão da landing page.
- Manter títulos, texto e controles agrupados por prioridade: identificação da seção, título, descrição e ação.
- Seções longas podem alternar entre fundo base e variações de superfície, sem introduzir novas cores dominantes.
- O hero pode usar alinhamento central, halos verdes desfocados e grande espaço vertical. Efeitos de brilho ficam atrás do conteúdo e não podem reduzir o contraste.
- Usar grids para métricas, recursos, comparações, depoimentos e preços. Reorganizar o grid em telas menores em vez de comprimir cartões.
- A barra de navegação institucional é fixa durante a rolagem, com fundo escuro translúcido e desfoque; conteúdo e links continuam legíveis.
- Cards agrupam conteúdos relacionados com borda sutil, superfície um pouco mais clara que o fundo e espaçamento interno generoso.
- O cartão ou plano em destaque pode usar borda verde-lima e brilho moderado, sem elevar todos os cartões simultaneamente.

## 6. Componentes e estados

### Botões e links

- **Primário:** preenchimento `#C6F277`, texto `#122421`, Manrope em peso alto e cantos arredondados. Reservar para a ação principal da área.
- **Secundário:** fundo transparente ou translúcido, contorno claro discreto e texto claro.
- **Ghost/quiet:** sem preenchimento dominante; hover recebe uma superfície clara translúcida.
- **Contornado de destaque:** borda e texto verde-lima; adequado a ações secundárias em cartões.
- Em hover, usar mudança de cor, sombra curta e/ou elevação sutil de 1–2px. A transição deve ser breve e não deslocar o layout.
- Links de texto devem continuar reconhecíveis sem depender exclusivamente de cor.

### Cartões, etiquetas e superfícies

- Usar cantos de **6–8px** em botões e controles, **10–14px** em cartões e **999px** em pílulas.
- Bordas claras ou verde-lima são finas e de baixa intensidade; usar destaque mais forte apenas para seleção ou conteúdo prioritário.
- Tags e badges devem ser compactos, com contraste explícito e texto curto.
- Usar ícones simples e consistentes. Não depender apenas de emoji para funções essenciais ou estados.

### Feedback

- Estados de sucesso, atenção, erro e informação devem combinar cor com texto e, quando útil, ícone.
- Foco por teclado deve ser sempre visível e contrastante; não remover `outline` sem substituto acessível.
- Botões desabilitados devem parecer indisponíveis e não podem ser confundidos com botões secundários ativos.
- Respeitar a preferência de movimento reduzido do sistema em animações, transições e efeitos de brilho.

## 7. Responsividade

A landing page usa como referências os breakpoints de **1024px** e **768px**:

- **Até 1024px:** reduzir colunas de grids e evitar cartões excessivamente estreitos; grades de quatro itens passam a duas colunas.
- **Até 768px:** substituir a navegação horizontal por menu móvel; empilhar conteúdo de duas colunas e manter ações fáceis de tocar.
- Permitir que grupos de botões, tabs e garantias quebrem linha sem sobreposição.
- Reduzir padding e tamanhos de título de forma fluida; preservar espaço lateral e evitar rolagem horizontal.
- Em telas pequenas, manter a ação principal evidente e posicionada depois da proposta de valor.

## 8. Personalização por estabelecimento

As cores configuráveis pelo estabelecimento pertencem à **marca do tenant**, não à identidade global do produto:

- `BrandPrimaryColor` e `BrandSecondaryColor` devem ser aplicadas somente em contextos de marca do estabelecimento, como comprovante, logomarca e acompanhamento público.
- A identidade global da aplicação Lavaway permanece baseada em fundo escuro, superfícies verde-petróleo e acento `#C6F277`.
- A cor escolhida pelo estabelecimento não deve sobrescrever globalmente navegação, botões, alertas ou componentes do produto.
- Em previews de marca, validar contraste entre as cores escolhidas e o texto; preservar uma alternativa legível quando a combinação não for acessível.

## 9. Evitar

- Não trocar a base escura e o acento verde-lima por paletas genéricas de azul/roxo como identidade padrão.
- Não usar verde-lima em todos os elementos interativos; o uso excessivo elimina hierarquia.
- Não empilhar gradientes, sombras fortes, brilhos ou efeitos de vidro na mesma superfície.
- Não usar cinzas de baixo contraste para textos importantes em fundo escuro.
- Não misturar famílias tipográficas sem função clara nem usar IBM Plex Mono para texto corrido.
- Não alterar as cores de marca do tenant como forma de personalizar a interface global.

## 10. Referências no código

- Landing page: [Home.razor](src/Frontend/CarWashSaaS.Client.Web/Pages/Home.razor)
- Estilos da landing page e valores de referência: [Home.razor.css](src/Frontend/CarWashSaaS.Client.Web/Pages/Home.razor.css)
- Shell institucional: [LandingLayout.razor.css](src/Frontend/CarWashSaaS.Client.Web/Layout/LandingLayout.razor.css)
- Carregamento de fontes: [index.html](src/Frontend/CarWashSaaS.Client.Web/wwwroot/index.html)
- Configuração de marca por estabelecimento: [identidade-visual-2.2.md](docs/living-docs/identidade-visual-2.2.md)
