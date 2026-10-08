using System.Security.Cryptography;
using System.Text;
using CarWashSaaS.Billing.Application;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.Billing.Infrastructure.Security;

public sealed class PaymentCredentialsEncryptor : IPaymentCredentialsEncryptor
{
    private readonly byte[] _key;

    public PaymentCredentialsEncryptor(IConfiguration configuration)
    {
        var configuredKey = configuration["Billing:EncryptionKey"]
            ?? configuration["Jwt:Secret"]
            ?? "LavawayBillingMasterKey2026SecureDefaultSaltKey!";

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[16];
        var cipherBytes = new byte[plainBytes.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        try
        {
            var combined = Convert.FromBase64String(cipherText);
            if (combined.Length < 12 + 16)
            {
                return string.Empty;
            }

            var nonce = new byte[12];
            var tag = new byte[16];
            var cipherBytes = new byte[combined.Length - 12 - 16];

            Buffer.BlockCopy(combined, 0, nonce, 0, 12);
            Buffer.BlockCopy(combined, 12, tag, 0, 16);
            Buffer.BlockCopy(combined, 28, cipherBytes, 0, cipherBytes.Length);

            var plainBytes = new byte[cipherBytes.Length];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
