IF OBJECT_ID(N'[identity].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
    CREATE TABLE [identity].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE TABLE [identity].[AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[identity].[AspNetRoles]'))
        SET IDENTITY_INSERT [identity].[AspNetRoles] ON;
    EXEC(N'INSERT INTO [identity].[AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (''b4f6a998-8bf9-4f27-9ed0-a3e82c62b501'', N''b4f6a998-8bf9-4f27-9ed0-a3e82c62b501'', N''Administrator'', N''ADMINISTRATOR''),
    (''b4f6a998-8bf9-4f27-9ed0-a3e82c62b502'', N''b4f6a998-8bf9-4f27-9ed0-a3e82c62b502'', N''Receptionist'', N''RECEPTIONIST''),
    (''b4f6a998-8bf9-4f27-9ed0-a3e82c62b503'', N''b4f6a998-8bf9-4f27-9ed0-a3e82c62b503'', N''Operator'', N''OPERATOR'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[identity].[AspNetRoles]'))
        SET IDENTITY_INSERT [identity].[AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [identity].[AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [identity].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [identity].[AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [identity].[AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [identity].[AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [identity].[AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_TenantId] ON [identity].[AspNetUsers] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [identity].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011626_InitialIdentity'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001011626_InitialIdentity', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002140041_AddRefreshTokens'
)
BEGIN
    CREATE TABLE [identity].[RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(256) NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        [ReplacedByTokenHash] nvarchar(256) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002140041_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_TenantId] ON [identity].[RefreshTokens] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002140041_AddRefreshTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [identity].[RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002140041_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [identity].[RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002140041_AddRefreshTokens'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002140041_AddRefreshTokens', N'10.0.9');
END;

COMMIT;
GO

