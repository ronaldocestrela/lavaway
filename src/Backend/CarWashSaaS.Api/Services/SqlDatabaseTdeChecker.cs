using System.Data;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarWashSaaS.Api.Services;

public sealed class SqlDatabaseTdeChecker : ITdeStatusChecker
{
    private readonly string _connectionString;
    private readonly ILogger<SqlDatabaseTdeChecker> _logger;

    public SqlDatabaseTdeChecker(IConfiguration configuration, ILogger<SqlDatabaseTdeChecker> logger)
    {
        _connectionString = configuration.GetConnectionString("CarWashSaaS")
            ?? DotEnvConfiguration.GetRequiredConnectionString();
        _logger = logger;
    }

    public async Task<Result<DatabaseTdeStatusDto>> CheckTdeStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "CarWashSaaS" : builder.InitialCatalog;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                SELECT 
                    db.name AS DatabaseName,
                    ISNULL(dek.encryption_state, 0) AS EncryptionState,
                    dek.key_algorithm AS KeyAlgorithm,
                    dek.key_length AS KeyLength,
                    c.name AS CertificateName,
                    ISNULL(dek.percent_complete, 0.0) AS PercentComplete
                FROM sys.databases db
                LEFT JOIN sys.dm_database_encryption_keys dek ON db.database_id = dek.database_id
                LEFT JOIN sys.certificates c ON dek.encryptor_thumbprint = c.thumbprint
                WHERE db.name = @DatabaseName;
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@DatabaseName", SqlDbType.NVarChar, 128) { Value = databaseName });

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var encState = reader.GetInt32(reader.GetOrdinal("EncryptionState"));
                var keyAlgo = reader.IsDBNull(reader.GetOrdinal("KeyAlgorithm")) ? null : reader.GetString(reader.GetOrdinal("KeyAlgorithm"));
                var keyLength = reader.IsDBNull(reader.GetOrdinal("KeyLength")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("KeyLength"));
                var certName = reader.IsDBNull(reader.GetOrdinal("CertificateName")) ? null : reader.GetString(reader.GetOrdinal("CertificateName"));
                var percent = Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PercentComplete")));

                var desc = encState switch
                {
                    0 => "0: No Database Encryption Key",
                    1 => "1: Unencrypted",
                    2 => "2: Encryption In Progress",
                    3 => "3: Encrypted (TDE Ativo com AES-256)",
                    4 => "4: Key Change In Progress",
                    5 => "5: Decryption In Progress",
                    6 => "6: Protection Change In Progress",
                    _ => $"Status desconhecido ({encState})"
                };

                var dto = new DatabaseTdeStatusDto(
                    DatabaseName: databaseName,
                    EncryptionState: encState,
                    EncryptionStateDescription: desc,
                    KeyAlgorithm: keyAlgo,
                    KeyLength: keyLength,
                    CertificateName: certName,
                    PercentComplete: percent,
                    IsTdeActive: encState == 3,
                    CheckedAtUtc: DateTimeOffset.UtcNow);

                return Result<DatabaseTdeStatusDto>.Success(dto);
            }

            return Result<DatabaseTdeStatusDto>.Failure(new Error(
                "tde.database_not_found",
                $"O banco de dados '{databaseName}' não foi localizado no servidor SQL.",
                ErrorType.NotFound));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar estado de TDE no banco SQL Server.");
            return Result<DatabaseTdeStatusDto>.Failure(new Error(
                "tde.query_failed",
                $"Erro ao consultar estado de TDE: {ex.Message}",
                ErrorType.Unavailable));
        }
    }
}
