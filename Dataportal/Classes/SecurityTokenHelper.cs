using System.Security.Cryptography;
using System.Text;

namespace Dataportal.Classes;

public static class SecurityTokenHelper
{
    public static string GenerateNumericCode(int length = 6)
    {
        var randomNumber = RandomNumberGenerator.GetInt32((int)Math.Pow(10, length - 1), (int)Math.Pow(10, length));
        return randomNumber.ToString($"D{length}");
    }

    public static string GenerateSecureToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}