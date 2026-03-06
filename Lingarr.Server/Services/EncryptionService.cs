using Lingarr.Server.Interfaces.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Lingarr.Server.Services;

public class EncryptionService : IEncryptionService
{
    private readonly IDataProtector _protector;

    public EncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Lingarr");
    }

    public string Encrypt(string value) => string.IsNullOrEmpty(value) ? value : _protector.Protect(value);

    public string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return _protector.Unprotect(value);
    }

    /// <summary>
    /// Attempts to decrypt a value. Returns true if decryption succeeded, false if the value
    /// is not encrypted (used during migration to detect unencrypted values).
    /// </summary>
    public bool TryDecrypt(string value, out string decrypted)
    {
        decrypted = value;
        if (string.IsNullOrEmpty(value)) return true;

        try
        {
            decrypted = _protector.Unprotect(value);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
