# ADR-0028: Criptografia em Repouso (TDE), Criptografia de Credenciais de Terceiros e Mascaramento de PII em Observabilidade

## Status
Aceito

## Contexto
Em conformidade com a LGPD (Lei nº 13.709/2018), especificamente o **Artigo 46** (Medidas técnicas e administrativas de segurança para proteção de dados pessoais) e o **Artigo 6º, Inciso III** (Princípio da Necessidade e Minimização de Dados), a plataforma SaaS Lavaway exige garantias técnicas formais para:
1. **Proteção de Dados em Repouso no Banco de Dados:** Impedir que vazamentos de volumes físicos, snapshots de disco ou arquivos brutos (`.mdf`, `.ldf`, backups `.bak`) exponham informações pessoais de clientes e dados operacionais de lava-jatos.
2. **Proteção Criptográfica de Credenciais Sensíveis de Terceiros:** Assegurar que chaves de API secretas de adquirentes e integrações (Pagar.me, Mercado Pago, Stripe e Evolution API do WhatsApp) persistam criptografadas na base (`TenantPaymentGatewayConfigs`, `TenantWhatsAppConfigs`) sem possibilidade de leitura em texto plano.
3. **Prevenção de Vazamento de PII em Logs e Telemetria:** Evitar que dados cadastrais (CPF, telefone, e-mail) ou tokens de autorização sejam gravados de forma legível em arquivos JSON rotativos (`logs/lavaway-.json`), no console do host ou em dashboards de observabilidade.

## Decisões de Arquitetura

### 1. Transparent Data Encryption (TDE) no SQL Server 2022
- Adotado o **TDE nativo com AES-256 bits** para o banco de dados `CarWashSaaS` em produção.
- Criado o script SQL idempotente de provisionamento `scripts/sql/enable-tde.sql` que orquestra a criação da Database Master Key (DMK) no banco `master`, do Certificado do Servidor (`LavawayTdeCert`), da Database Encryption Key (DEK) com `AES_256` e ativação via `ALTER DATABASE [CarWashSaaS] SET ENCRYPTION ON`.
- Criado o script de salvaguarda de certificados e chaves privadas `scripts/sql/backup-tde-keys.sql` para garantir prontidão de Disaster Recovery.
- Implementado o serviço `SqlDatabaseTdeChecker` e endpoint administrativo `GET /platform/observability/database/encryption-status` para auditoria contínua do estado do TDE via DMVs `sys.dm_database_encryption_keys`.

### 2. Criptografia Autenticada de Credenciais (AEAD AES-GCM 256 bits)
- As chaves secretas de provedores de WhatsApp (`EvolutionApiKeyEncrypted`, `WebhookSecretEncrypted`) e de faturamento (`PagarMeSecretKeyEncrypted`, `MercadoPagoAccessTokenEncrypted`) são criptografadas em nível de coluna/aplicação utilizando **AES-GCM (Galois/Counter Mode)** com nonce criptográfico aleatório de 12 bytes (`RandomNumberGenerator`) e tag de autenticação de 16 bytes.
- A chave mestra de derivação é obtida via variáveis de ambiente com hash SHA-256.
- A decriptação valida a tag de autenticação em tempo constante, retornando vazio caso ocorra qualquer tentativa de adulteração de dados (tampering prevention).

### 3. Mascaramento Centralizado e Sanitização no Serilog (PII Masking)
- Criada a classe `PiiRegexMasker` no projeto `CarWashSaaS.Shared.Contracts` com expressões regulares compiladas (`[GeneratedRegex]`) para mascaramento de:
  - **CPF:** `***.***.123-**`
  - **Telefone:** `(11) *****-1234` ou `+55 (11) *****-1234`
  - **E-mail:** `u***@dominio.com`
  - **Chaves de API / Segredos:** `sk_live_••••••••1234`, `ev_••••••••5678`
- Desenvolvido o enricher do Serilog `PiiDataMaskingEnricher` no host `CarWashSaaS.Api`, inspecionando e sanitizando automaticamente mensagens e propriedades estruturadas.
- O pipeline de logs de requisição HTTP (`UseSerilogRequestLogging`) higieniza o `RequestPath` e parâmetros de consulta antes da gravação em disco.

## Consequências

- **Positivas:**
  - Plena conformidade técnica com o Art. 46 e Art. 6º, III da LGPD.
  - O banco de dados físico, backups e volumes Docker tornam-se ilegíveis fora da instância protegida pelo certificado TDE.
  - Chaves de terceiros de cada tenant possuem proteção de ponta a ponta e nunca trafegam em texto plano para dashboards ou logs.
  - Zero acoplamento das camadas de Domínio com o Serilog, validado pelos testes de arquitetura `ModuleBoundaryTests` (NetArchTest).
  - 100% de sucesso nos testes automatizados da solução.

- **Mitigações Operacionais:**
  - O backup do certificado do servidor (`LavawayTdeCert.cer`) e da chave privada (`LavawayTdeCert.pvk`) deve ser armazenado fora do container do banco de dados (cold-storage protegido) para possibilitar a restauração em novos ambientes.
