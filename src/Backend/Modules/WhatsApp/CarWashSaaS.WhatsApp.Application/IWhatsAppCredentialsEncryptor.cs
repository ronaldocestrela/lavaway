namespace CarWashSaaS.WhatsApp.Application;

public interface IWhatsAppCredentialsEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
