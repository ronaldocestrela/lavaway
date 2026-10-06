# ADR-0017: Sistema visual compartilhado para a aplicação autenticada

## Status
Aceito

## Contexto

As páginas autenticadas cresceram com estilos isolados e decisões visuais distintas para cabeçalhos, botões, superfícies, tipografia e estados. Isso gerou inconsistência entre áreas operacionais e administrativas e tornou ajustes de identidade visual mais sujeitos a divergências.

## Decisão

- Definir tokens de cor, tipografia, espaçamento, bordas, sombras e foco em `wwwroot/css/app.css`.
- Fornecer estilos compartilhados em `wwwroot/css/design-system.css`, carregados após os estilos isolados do Blazor para aplicar as regras comuns às páginas dentro do workspace.
- Reutilizar o componente Blazor `PageHeading` nas páginas com cabeçalho de título, contexto, descrição e ações opcionais.
- Preservar a identidade da landing page e da autenticação, que ficam fora do layout autenticado, e preservar estruturas particulares quando necessárias ao fluxo (por exemplo, check-in e vistoria).
- Manter o breadcrumb sincronizado com a rota atual e tornar a navegação lateral horizontalmente navegável em telas estreitas.

## Consequências

- A identidade das telas internas é ajustada por tokens e primitivas compartilhadas, em vez de exigir a alteração de valores repetidos página a página.
- Páginas podem continuar adicionando estilos específicos, mas cores e componentes comuns devem usar os tokens do sistema.
- Novas páginas autenticadas devem preferir `PageHeading` e as primitivas do sistema visual, com exceções justificadas pelo fluxo de trabalho.
- A landing page e o login podem continuar evoluindo separadamente, sem herdar mudanças acidentais do tema operacional.
