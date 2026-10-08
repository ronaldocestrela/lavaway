namespace CarWashSaaS.Billing.Application;

public interface IPaymentCredentialsEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
