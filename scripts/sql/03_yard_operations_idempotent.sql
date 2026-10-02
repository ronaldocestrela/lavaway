IF OBJECT_ID(N'[yard].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'yard') IS NULL EXEC(N'CREATE SCHEMA [yard];');
    CREATE TABLE [yard].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    IF SCHEMA_ID(N'yard') IS NULL EXEC(N'CREATE SCHEMA [yard];');
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[Customers] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Phone] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Customers_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[Services] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Services_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[Vehicles] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Plate] varchar(7) NOT NULL,
        [Size] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Vehicles_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_Vehicles_Customers_TenantId_CustomerId] FOREIGN KEY ([TenantId], [CustomerId]) REFERENCES [yard].[Customers] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[ServicePrices] (
        [VehicleSize] nvarchar(32) NOT NULL,
        [ServiceId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [EstimatedDurationMinutes] int NOT NULL,
        CONSTRAINT [PK_ServicePrices] PRIMARY KEY ([ServiceId], [VehicleSize]),
        CONSTRAINT [FK_ServicePrices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [yard].[Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[WorkOrders] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_WorkOrders_TenantId_Id] UNIQUE ([TenantId], [Id]),
        CONSTRAINT [FK_WorkOrders_Customers_TenantId_CustomerId] FOREIGN KEY ([TenantId], [CustomerId]) REFERENCES [yard].[Customers] ([TenantId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Vehicles_TenantId_VehicleId] FOREIGN KEY ([TenantId], [VehicleId]) REFERENCES [yard].[Vehicles] ([TenantId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE TABLE [yard].[WorkOrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ServiceId] uniqueidentifier NOT NULL,
        [ServiceName] nvarchar(200) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [EstimatedDurationMinutes] int NOT NULL,
        [Quantity] int NOT NULL,
        [WorkOrderId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WorkOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkOrderItems_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [yard].[WorkOrders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_Customers_TenantId_Phone] ON [yard].[Customers] ([TenantId], [Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_ServicePrices_TenantId_VehicleSize] ON [yard].[ServicePrices] ([TenantId], [VehicleSize]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_Vehicles_TenantId_CustomerId] ON [yard].[Vehicles] ([TenantId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_TenantId_Plate] ON [yard].[Vehicles] ([TenantId], [Plate]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_WorkOrderItems_TenantId_ServiceId] ON [yard].[WorkOrderItems] ([TenantId], [ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_WorkOrderItems_WorkOrderId] ON [yard].[WorkOrderItems] ([WorkOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_TenantId_CreatedAtUtc] ON [yard].[WorkOrders] ([TenantId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_TenantId_CustomerId] ON [yard].[WorkOrders] ([TenantId], [CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_TenantId_VehicleId] ON [yard].[WorkOrders] ([TenantId], [VehicleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001011754_InitialYardOperations'
)
BEGIN
    INSERT INTO [yard].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001011754_InitialYardOperations', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    ALTER TABLE [yard].[Customers] ADD [NormalizedPhone] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    UPDATE [yard].[Customers] SET [NormalizedPhone] = [Phone];
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    WHILE EXISTS (
        SELECT 1
        FROM [yard].[Customers]
        WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%')
    BEGIN
        UPDATE [yard].[Customers]
        SET [NormalizedPhone] = STUFF(
            [NormalizedPhone],
            PATINDEX('%[^0-9]%', [NormalizedPhone] COLLATE Latin1_General_100_BIN2),
            1,
            '')
        WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%';
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    UPDATE [yard].[Customers]
    SET [NormalizedPhone] = SUBSTRING([NormalizedPhone], 3, LEN([NormalizedPhone]) - 2)
    WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '55%'
        AND LEN([NormalizedPhone]) IN (12, 13);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[yard].[Customers]') AND [c].[name] = N'NormalizedPhone');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [yard].[Customers] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [yard].[Customers] ALTER COLUMN [NormalizedPhone] nvarchar(32) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    DROP INDEX [IX_Customers_TenantId_Phone] ON [yard].[Customers];
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    CREATE INDEX [IX_Customers_TenantId_NormalizedPhone] ON [yard].[Customers] ([TenantId], [NormalizedPhone]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001130000_AddCustomerNormalizedPhone'
)
BEGIN
    INSERT INTO [yard].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001130000_AddCustomerNormalizedPhone', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    CREATE TABLE [yard].[CommissionRules] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ServiceName] nvarchar(200) NOT NULL,
        [RoleName] nvarchar(80) NOT NULL,
        [Percentage] decimal(5,2) NOT NULL,
        CONSTRAINT [PK_CommissionRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    CREATE TABLE [yard].[TeamMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Role] nvarchar(80) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TeamMembers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    CREATE TABLE [yard].[YardCapacities] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TotalBoxes] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_YardCapacities] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_YardCapacities_TenantId_Id] UNIQUE ([TenantId], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CommissionRules_TenantId_ServiceName_RoleName] ON [yard].[CommissionRules] ([TenantId], [ServiceName], [RoleName]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeamMembers_TenantId_Email] ON [yard].[TeamMembers] ([TenantId], [Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [yard].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001221015_AddYardSetupEntities'
)
BEGIN
    INSERT INTO [yard].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001221015_AddYardSetupEntities', N'10.0.9');
END;

COMMIT;
GO

