-- ============================================================================
-- LAVAWAY SAAS: Script Idempotente de Habilitação de TDE (Transparent Data Encryption)
-- Microsoft SQL Server 2022 (Standard / Enterprise)
-- Conformidade LGPD: Artigo 46 (Segurança da Informação e Criptografia em Repouso)
-- ============================================================================

SET NOCOUNT ON;

DECLARE @DatabaseName NVARCHAR(128) = N'CarWashSaaS';
DECLARE @CertificateName NVARCHAR(128) = N'LavawayTdeCert';
DECLARE @MasterKeyPassword NVARCHAR(128) = COALESCE(NULLIF(RTRIM(LTRIM('$(MSSQL_TDE_PASSWORD)')), ''), N'LavawayMasterTdePass2026!SecureKey#');

PRINT '>>> [TDE] Iniciando verificação e configuração de Transparent Data Encryption para [' + @DatabaseName + ']...';

-- 1. Criação da Database Master Key (DMK) no banco master se ainda não existir
USE [master];
IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE [name] = '##MS_DatabaseMasterKey##')
BEGIN
    PRINT '>>> [TDE] Criando Database Master Key no banco master...';
    DECLARE @CreateMasterKeySql NVARCHAR(MAX) = N'CREATE MASTER KEY ENCRYPTION BY PASSWORD = ''' + REPLACE(@MasterKeyPassword, '''', '''''') + N''';';
    EXEC sp_executesql @CreateMasterKeySql;
    PRINT '>>> [TDE] Master Key criada com sucesso.';
END
ELSE
BEGIN
    PRINT '>>> [TDE] Master Key já existente no banco master.';
END

-- 2. Criação do Certificado do Servidor para proteger a DEK se ainda não existir
IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE [name] = @CertificateName)
BEGIN
    PRINT '>>> [TDE] Criando certificado [' + @CertificateName + '] no banco master...';
    DECLARE @CreateCertSql NVARCHAR(MAX) = N'CREATE CERTIFICATE [' + @CertificateName + N'] WITH SUBJECT = ''Lavaway SaaS TDE Server Certificate - LGPD Art. 46'';';
    EXEC sp_executesql @CreateCertSql;
    PRINT '>>> [TDE] Certificado [' + @CertificateName + '] criado com sucesso.';
END
ELSE
BEGIN
    PRINT '>>> [TDE] Certificado [' + @CertificateName + '] já existente no banco master.';
END

-- 3. Verificação e Criação da Database Encryption Key (DEK) com AES_256 no banco da aplicação
DECLARE @DbId INT = DB_ID(@DatabaseName);
IF @DbId IS NULL
BEGIN
    PRINT '>>> [TDE ALERTA] Banco [' + @DatabaseName + '] ainda não foi criado. Abortando criação da DEK.';
END
ELSE
BEGIN
    DECLARE @DekExists INT = 0;
    SELECT @DekExists = COUNT(*) FROM sys.dm_database_encryption_keys WHERE database_id = @DbId;

    IF @DekExists = 0
    BEGIN
        PRINT '>>> [TDE] Criando Database Encryption Key (AES_256) no banco [' + @DatabaseName + ']...';
        DECLARE @CreateDekSql NVARCHAR(MAX) = N'USE [' + @DatabaseName + N']; CREATE DATABASE ENCRYPTION KEY WITH ALGORITHM = AES_256 ENCRYPTION BY SERVER CERTIFICATE [' + @CertificateName + N'];';
        EXEC sp_executesql @CreateDekSql;
        PRINT '>>> [TDE] Database Encryption Key (AES_256) criada com sucesso.';
    END
    ELSE
    BEGIN
        PRINT '>>> [TDE] Database Encryption Key já existente no banco [' + @DatabaseName + '].';
    END

    -- 4. Habilitação do TDE no Banco
    DECLARE @IsEncrypted BIT = 0;
    SELECT @IsEncrypted = is_encrypted FROM sys.databases WHERE database_id = @DbId;

    IF @IsEncrypted = 0
    BEGIN
        PRINT '>>> [TDE] Ativando ENCRYPTION no banco [' + @DatabaseName + ']...';
        DECLARE @EnableTdeSql NVARCHAR(MAX) = N'ALTER DATABASE [' + @DatabaseName + N'] SET ENCRYPTION ON;';
        EXEC sp_executesql @EnableTdeSql;
        PRINT '>>> [TDE] ENCRYPTION ON ativado com sucesso.';
    END
    ELSE
    BEGIN
        PRINT '>>> [TDE] Banco [' + @DatabaseName + '] já está com ENCRYPTION ativado.';
    END
END

-- 5. Relatório de Estado da Criptografia em Repouso
PRINT '>>> [TDE STATUS REPORT]';
SELECT 
    db.name AS DatabaseName,
    CASE dek.encryption_state
        WHEN 0 THEN '0: No Database Encryption Key'
        WHEN 1 THEN '1: Unencrypted'
        WHEN 2 THEN '2: Encryption In Progress'
        WHEN 3 THEN '3: Encrypted (Protegido com TDE)'
        WHEN 4 THEN '4: Key Change In Progress'
        WHEN 5 THEN '5: Decryption In Progress'
        WHEN 6 THEN '6: Protection Change In Progress'
        ELSE 'Desconhecido'
    END AS EncryptionStateDescription,
    dek.encryption_state AS EncryptionState,
    dek.key_algorithm AS KeyAlgorithm,
    dek.key_length AS KeyLength,
    c.name AS CertificateName,
    dek.percent_complete AS PercentComplete
FROM sys.databases db
LEFT JOIN sys.dm_database_encryption_keys dek ON db.database_id = dek.database_id
LEFT JOIN sys.certificates c ON dek.encryptor_thumbprint = c.thumbprint
WHERE db.name = @DatabaseName;
