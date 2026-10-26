-- ============================================================================
-- LAVAWAY SAAS: Script de Salvaguarda de Certificado e Chave TDE (Disaster Recovery)
-- Conformidade LGPD: Artigo 46 (Salvaguarda de Criptografia e Continuidade do Negócio)
-- ============================================================================

SET NOCOUNT ON;

DECLARE @CertificateName NVARCHAR(128) = N'LavawayTdeCert';
DECLARE @BackupCertPath NVARCHAR(260) = N'/var/opt/mssql/data/LavawayTdeCert.cer';
DECLARE @BackupKeyPath NVARCHAR(260) = N'/var/opt/mssql/data/LavawayTdeCert.pvk';
DECLARE @PrivateKeyPassword NVARCHAR(128) = COALESCE(NULLIF(RTRIM(LTRIM('$(MSSQL_TDE_BACKUP_PASSWORD)')), ''), N'LavawayTdeKeyBackup2026!StrongPass#');

PRINT '>>> [TDE BACKUP] Iniciando exportação do certificado e private key [' + @CertificateName + ']...';

USE [master];
IF EXISTS (SELECT 1 FROM sys.certificates WHERE [name] = @CertificateName)
BEGIN
    DECLARE @BackupSql NVARCHAR(MAX) = N'
        BACKUP CERTIFICATE [' + @CertificateName + N']
        TO FILE = ''' + REPLACE(@BackupCertPath, '''', '''''') + N'''
        WITH PRIVATE KEY (
            FILE = ''' + REPLACE(@BackupKeyPath, '''', '''''') + N''',
            ENCRYPTION BY PASSWORD = ''' + REPLACE(@PrivateKeyPassword, '''', '''''') + N'''
        );';

    EXEC sp_executesql @BackupSql;
    PRINT '>>> [TDE BACKUP] Backup realizado com sucesso em: ' + @BackupCertPath + ' e ' + @BackupKeyPath;
    PRINT '>>> [TDE ATENÇÃO] Mova esses dois arquivos imediatamente para cold-storage ou cofre de segredos cifrado externo!';
END
ELSE
BEGIN
    PRINT '>>> [TDE ALERTA] Certificado [' + @CertificateName + '] não encontrado no banco master.';
END
