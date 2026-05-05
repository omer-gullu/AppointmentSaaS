namespace Appointment_SaaS.Business.Abstract;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
