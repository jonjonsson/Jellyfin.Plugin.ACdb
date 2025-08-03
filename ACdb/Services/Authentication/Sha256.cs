using System.Security.Cryptography;
using System.Text;

namespace ACdb.Services.Authentication;

internal class Sha256
{
    public static string Hash(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        using SHA256 sha256 = SHA256.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(s);
        byte[] hashBytes = sha256.ComputeHash(inputBytes);

        StringBuilder sb = new();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

}
