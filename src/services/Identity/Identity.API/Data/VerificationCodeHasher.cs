using System.Security.Cryptography;
using System.Text;

namespace Identity.API.Data;

public static class VerificationCodeHasher
{
    public static string Hash(string plaintext)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
}
