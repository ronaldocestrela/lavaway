IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'whatsapp')
BEGIN
    EXEC('CREATE SCHEMA whatsapp');
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WhatsAppConnections' AND schema_id = SCHEMA_ID('whatsapp'))
BEGIN
    CREATE TABLE [whatsapp].[WhatsAppConnections] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProviderSessionId] nvarchar(200) NOT NULL,
        [QrCodeValue] nvarchar(2000) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WhatsAppConnections] PRIMARY KEY ([Id])
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WhatsAppMessages' AND schema_id = SCHEMA_ID('whatsapp'))
BEGIN
    CREATE TABLE [whatsapp].[WhatsAppMessages] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RecipientPhone] nvarchar(32) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ProviderMessageId] nvarchar(256) NULL,
        [FailureReason] nvarchar(1024) NULL,
        [AttemptCount] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [SentAtUtc] datetimeoffset NULL,
        [DeliveredAtUtc] datetimeoffset NULL,
        [ReadAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_WhatsAppMessages] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_WhatsAppMessages_TenantId_IdempotencyKey]
        ON [whatsapp].[WhatsAppMessages] ([TenantId], [IdempotencyKey]);

    CREATE INDEX [IX_WhatsAppMessages_TenantId_CreatedAt]
        ON [whatsapp].[WhatsAppMessages] ([TenantId], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WhatsAppDeliveryAttempts' AND schema_id = SCHEMA_ID('whatsapp'))
BEGIN
    CREATE TABLE [whatsapp].[WhatsAppDeliveryAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [OutboundWhatsAppMessageId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [AttemptedAtUtc] datetimeoffset NOT NULL,
        [IsSuccess] bit NOT NULL,
        [ErrorCode] nvarchar(64) NULL,
        [ErrorMessage] nvarchar(1024) NULL,
        [HttpStatusCode] int NULL,
        CONSTRAINT [PK_WhatsAppDeliveryAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WhatsAppDeliveryAttempts_WhatsAppMessages_OutboundWhatsAppMessageId]
            FOREIGN KEY ([OutboundWhatsAppMessageId]) REFERENCES [whatsapp].[WhatsAppMessages] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_WhatsAppDeliveryAttempts_OutboundWhatsAppMessageId]
        ON [whatsapp].[WhatsAppDeliveryAttempts] ([OutboundWhatsAppMessageId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantWhatsAppQuotas' AND schema_id = SCHEMA_ID('whatsapp'))
BEGIN
    CREATE TABLE [whatsapp].[TenantWhatsAppQuotas] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [MaxMessagesPerMinute] int NOT NULL,
        [MaxMessagesPerDay] int NOT NULL,
        [SentInCurrentMinute] int NOT NULL,
        [SentToday] int NOT NULL,
        [CurrentMinuteWindowUtc] datetimeoffset NOT NULL,
        [CurrentDayWindowUtc] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TenantWhatsAppQuotas] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_TenantWhatsAppQuotas_TenantId]
        ON [whatsapp].[TenantWhatsAppQuotas] ([TenantId]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CustomerCommunicationPreferences' AND schema_id = SCHEMA_ID('whatsapp'))
BEGIN
    CREATE TABLE [whatsapp].[CustomerCommunicationPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [NormalizedPhone] nvarchar(32) NOT NULL,
        [IsOptedIn] bit NOT NULL,
        [OptedOutAtUtc] datetimeoffset NULL,
        [Reason] nvarchar(512) NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CustomerCommunicationPreferences] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_CustomerCommunicationPreferences_TenantId_NormalizedPhone]
        ON [whatsapp].[CustomerCommunicationPreferences] ([TenantId], [NormalizedPhone]);
END
GO
