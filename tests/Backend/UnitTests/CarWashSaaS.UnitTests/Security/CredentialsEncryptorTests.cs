using CarWashSaaS.Billing.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CarWashSaaS.UnitTests.Security;

public sealed class CredentialsEncryptorTests
{
    private readonly IConfiguration _configuration;

    public CredentialsEncryptorTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Billing:EncryptionKey", "TestSuperSecureMasterKey2026WithHighEntropy!" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void Encrypt_And_Decrypt_ShouldWorkCorrectly()
    {
        var encryptor = new PaymentCredentialsEncryptor(_configuration);
        var originalSecret = "sample_plain_secret_to_encrypt_12345";

        var encrypted = encryptor.Encrypt(originalSecret);
        var decrypted = encryptor.Decrypt(encrypted);

        Assert.False(string.IsNullOrWhiteSpace(encrypted));
        Assert.NotEqual(originalSecret, encrypted);
        Assert.Equal(originalSecret, decrypted);
    }

    [Fact]
    public void Encrypt_SameInputTwice_ShouldProduceDifferentCiphertexts_DueToRandomNonce()
    {
        var encryptor = new PaymentCredentialsEncryptor(_configuration);
        var secret = "sample_test_reproduciblePayload123456";

        var cipher1 = encryptor.Encrypt(secret);
        var cipher2 = encryptor.Encrypt(secret);

        Assert.NotEqual(cipher1, cipher2);
        Assert.Equal(secret, encryptor.Decrypt(cipher1));
        Assert.Equal(secret, encryptor.Decrypt(cipher2));
    }

    [Fact]
    public void Decrypt_WhenCiphertextIsTampered_ShouldReturnEmptyString()
    {
        var encryptor = new PaymentCredentialsEncryptor(_configuration);
        var secret = "ev_webhookSecret12345";

        var cipher = encryptor.Encrypt(secret);
        var rawBytes = Convert.FromBase64String(cipher);

        // Adulterar um byte da tag ou do ciphertext
        rawBytes[^1] ^= 0xFF;
        var tamperedCipher = Convert.ToBase64String(rawBytes);

        var decrypted = encryptor.Decrypt(tamperedCipher);

        // AEAD AES-GCM rejeita tag inválida e o serviço deve retornar string vazia com segurança
        Assert.Equal(string.Empty, decrypted);
    }

    [Fact]
    public void Encrypt_And_Decrypt_EmptyOrNull_ShouldReturnEmpty()
    {
        var encryptor = new PaymentCredentialsEncryptor(_configuration);

        Assert.Equal(string.Empty, encryptor.Encrypt(string.Empty));
        Assert.Equal(string.Empty, encryptor.Decrypt(string.Empty));
        Assert.Equal(string.Empty, encryptor.Decrypt("invalid-base64-content"));
    }

    [Fact]
    public void WhatsAppCredentialsEncryptor_ShouldEncryptAndDecryptEvolutionApiKey()
    {
        var whatsAppEncryptor = new CarWashSaaS.WhatsApp.Infrastructure.Security.WhatsAppCredentialsEncryptor(_configuration);
        var apiKey = "ev_live_apiKeyDedicatedTenant123456789";

        var encrypted = whatsAppEncryptor.Encrypt(apiKey);
        var decrypted = whatsAppEncryptor.Decrypt(encrypted);

        Assert.False(string.IsNullOrWhiteSpace(encrypted));
        Assert.NotEqual(apiKey, encrypted);
        Assert.Equal(apiKey, decrypted);
    }
}
