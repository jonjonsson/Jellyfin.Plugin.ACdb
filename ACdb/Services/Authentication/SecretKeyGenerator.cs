using System;
using System.Security.Cryptography;
using System.Text;

namespace ACdb.Services.Authentication;

public class SecretKeyGenerator
{
    public static string GenerateSecretKey(string clientId, string secret)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            throw new ArgumentNullException(nameof(clientId), "Client ID cannot be null or empty.");
        }

        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentNullException(nameof(secret), "Secret cannot be null or empty.");
        }

        byte[] key = Encoding.UTF8.GetBytes(secret);

        using var aes = Aes.Create();
        if (aes == null)
        {
            throw new InvalidOperationException("Failed to create AES instance.");
        }

        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        aes.GenerateIV();
        byte[] iv = aes.IV;

        using var encryptor = aes.CreateEncryptor(aes.Key, iv);
        byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);

        int paddingLength = 16 - clientIdBytes.Length % 16;
        byte[] paddedClientIdBytes = new byte[clientIdBytes.Length + paddingLength];
        Array.Copy(clientIdBytes, paddedClientIdBytes, clientIdBytes.Length);

        byte[] encrypted = encryptor.TransformFinalBlock(paddedClientIdBytes, 0, paddedClientIdBytes.Length);

        byte[] combined = new byte[iv.Length + encrypted.Length];
        Array.Copy(iv, 0, combined, 0, iv.Length);
        Array.Copy(encrypted, 0, combined, iv.Length, encrypted.Length);

        return Convert.ToBase64String(combined);
    }
}
