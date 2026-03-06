namespace Lingarr.Server.Interfaces.Services;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    bool TryDecrypt(string value, out string decrypted);
}
