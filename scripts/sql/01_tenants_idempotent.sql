IF OBJECT_ID(N'[tenants].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'tenants') IS NULL EXEC(N'CREATE SCHEMA [tenants];');
    CREATE TABLE [tenants].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [tenants].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011604_InitialTenants'
)
BEGIN
    IF SCHEMA_ID(N'tenants') IS NULL EXEC(N'CREATE SCHEMA [tenants];');
END;

IF NOT EXISTS (
    SELECT * FROM [tenants].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011604_InitialTenants'
)
BEGIN
    CREATE TABLE [tenants].[Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [tenants].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011604_InitialTenants'
)
BEGIN
    INSERT INTO [tenants].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001011604_InitialTenants', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [tenants].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001220225_AddStoreProfile'
)
BEGIN
    CREATE TABLE [tenants].[StoreProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [LegalName] nvarchar(200) NOT NULL,
        [TradeName] nvarchar(200) NOT NULL,
        [Cnpj] nvarchar(14) NOT NULL,
        [Phone] nvarchar(32) NOT NULL,
        [Street] nvarchar(200) NOT NULL,
        [City] nvarchar(150) NOT NULL,
        [State] nvarchar(2) NOT NULL,
        [PostalCode] nvarchar(20) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [BrandPrimaryColor] nvarchar(7) NULL,
        [BrandSecondaryColor] nvarchar(7) NULL,
        CONSTRAINT [PK_StoreProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [tenants].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001220225_AddStoreProfile'
)
BEGIN
    INSERT INTO [tenants].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001220225_AddStoreProfile', N'10.0.9');
END;

COMMIT;
GO

