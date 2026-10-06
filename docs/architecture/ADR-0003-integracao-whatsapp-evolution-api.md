# ADR-0003: Integração do WhatsApp via Evolution API

- **Status:** Aceita para a entrega de pareamento do WhatsApp
- **Data:** 2026-10-01

## Contexto

A subfase 2.5 define o pareamento do WhatsApp para cada tenant, com geração de QR Code e criação de sessão por estabelecimento. O sistema precisa manter a separação de regras de negócio, o contrato de provider e o isolamento de tenant já estabelecidos na arquitetura.

A solução inicial usava um provedor estático para manter a API funcional. Isso validou o fluxo do domínio e da aplicação, mas não atendia ao requisito de integração real com um provedor de WhatsApp em produção.

## Decisão

- O pareamento do WhatsApp será orquestrado pela camada de aplicação através da interface `IWhatsAppPairingProvider`.
- A implementação concreta ficará em infraestrutura, isolando a dependência de um provedor externo do domínio.
- O provedor adotado será a Evolution API, com a configuração centralizada em `WhatsApp:EvolutionApi`.
- Para cada tenant, o provedor usa um identificador de sessão no formato `prefixo-tenantId` para manter a sessão única e rastreável.
- O QR Code e o estado da sessão são obtidos em tempo de execução pelo cliente HTTP do provedor, sem acoplamento às entidades de domínio.
- A API do backend continua configurando a implementação via DI, mantendo a injeção de dependência e o contrato do módulo.
- Atualizações de conexão chegam pelo endpoint anônimo `POST /whatsapp/webhooks/evolution`, protegido por segredo compartilhado em `X-Webhook-Secret` quando encaminhado por header, ou por parâmetro `secret` na URL no callback direto da Evolution API.
- O tenant do callback é resolvido do nome da instância, nunca de um campo de tenant enviado no payload. O caso de uso valida que o evento pertence à sessão atual antes de atualizar o estado.

## Consequências e controles

- O domínio permanece livre de dependências com HTTP, vendor SDK e detalhes do provedor de WhatsApp.
- O fluxo de aplicação continua retornando `Result<T>` e preserva a regra de multi-tenant.
- Eventos atrasados de uma sessão anterior são reconhecidos e confirmados sem modificar a conexão atual, evitando retentativas desnecessárias do provider.
- O segredo do webhook é uma configuração obrigatória para ativar o endpoint e deve ser fornecido pelo ambiente, nunca com valor padrão compartilhado.
- O provedor externo pode evoluir independentemente, desde que continue respeitando o contrato de `GeneratePairingAsync`.
- Qualquer falha de comunicação com a Evolution API deve produzir comportamento controlado e não quebrar a pipeline de negócio.
- Os testes automatizados devem continuar cobrindo o contrato do provedor, a lógica da aplicação e a proteção de tenant.

## Alternativas consideradas

- **Manter o provider estático em produção:** rejeitado porque não atende ao requisito real de pareamento do WhatsApp e deixa o sistema sem integração externa.
- **Acoplar a chamada Evolution API diretamente no serviço de aplicação:** rejeitado porque viola a separação de camadas e dificulta testes e troca de provedor.
- **Usar uma SDK de terceiros diretamente na API:** rejeitado por aumentar o acoplamento e impedir a troca de provedor sem mexer no domínio.

## Atualização para Evolution API v2.3.7

- **Data:** 2026-10-06
- O serviço local fixa `evoapicloud/evolution-api:v2.3.7`; tags mutáveis como `latest` não são usadas.
- A instalação utiliza PostgreSQL para persistir metadados das instâncias e Redis para cache, além do volume de arquivos das instâncias. As conexões internas dos bancos não são publicadas no host.
- A API envia os formatos v2 para texto (`number`, `text`) e mídia (`number`, `mediatype`, `mimetype`, `media`, `caption`, `fileName`).
- A criação da instância usa `integration` em minúsculas e configura callbacks pelo objeto `webhook` aninhado (`enabled`, `url`, `byEvents`, `base64` e `events`), como esperado pelo DTO v2.3.7.
- Erros HTTP, falhas de rede e respostas sem QR real são retornados como falhas do provider; nenhum QR sintético é persistido nem exibido como se viesse da Evolution API.
- A URL configurada diretamente na instância transporta o segredo em query string porque o contrato de configuração do webhook não define headers arbitrários. Em produção, usar HTTPS e impedir que a URL completa seja registrada nos logs; um proxy pode ser usado para converter o segredo em header caso necessário.
- As mensagens recebidas pela Evolution não são persistidas no PostgreSQL dela; a aplicação mantém seu armazenamento e processamento próprios.
- A alteração de armazenamento não converte automaticamente o estado antigo de instâncias. Em atualizações de instalações existentes, preservar o volume `evolution-instances`, fazer backup de todos os volumes e validar a recuperação das sessões antes do uso.
