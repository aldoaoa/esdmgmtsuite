using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;

namespace ESDSuite.Services.Auth;

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        byte[] salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        string saltStr = Convert.ToHexString(salt).Substring(0, 16).ToLower();

        byte[] derived = SCrypt.Generate(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(saltStr), 32768, 8, 1, 64);
        string hashHex = Convert.ToHexString(derived).ToLower();

        return $"scrypt:32768:8:1${saltStr}${hashHex}";
    }

    public static bool VerifyPassword(string storedHash, string password)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(password))
            return false;

        storedHash = storedHash.Trim();
        password = password.Trim();

        try
        {
            // Format 1: scrypt:N:r:p$salt$hash
            if (storedHash.StartsWith("scrypt:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = storedHash.Split('$');
                if (parts.Length >= 3)
                {
                    string paramsPart = parts[0]; // e.g. scrypt:32768:8:1
                    string saltStr = parts[1];
                    string expectedHashHex = parts[2];

                    int n = 32768, r = 8, p = 1;
                    var paramSub = paramsPart.Split(':');
                    if (paramSub.Length >= 4)
                    {
                        int.TryParse(paramSub[1], out n);
                        int.TryParse(paramSub[2], out r);
                        int.TryParse(paramSub[3], out p);
                    }

                    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                    int dkLen = expectedHashHex.Length / 2;

                    // Try UTF-8 salt
                    byte[] saltBytesUtf8 = Encoding.UTF8.GetBytes(saltStr);
                    byte[] derivedUtf8 = SCrypt.Generate(passwordBytes, saltBytesUtf8, n, r, p, dkLen);
                    string computedHashHexUtf8 = Convert.ToHexString(derivedUtf8).ToLower();
                    if (string.Equals(computedHashHexUtf8, expectedHashHex.ToLower(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // Try Hex-decoded salt if valid hex
                    if (IsHex(saltStr) && saltStr.Length % 2 == 0)
                    {
                        try
                        {
                            byte[] saltBytesHex = Convert.FromHexString(saltStr);
                            byte[] derivedHex = SCrypt.Generate(passwordBytes, saltBytesHex, n, r, p, dkLen);
                            string computedHashHexHex = Convert.ToHexString(derivedHex).ToLower();
                            if (string.Equals(computedHashHexHex, expectedHashHex.ToLower(), StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch {}
                    }

                    return false;
                }
            }

            // Format 2: pbkdf2:sha256:iterations$salt$hash
            if (storedHash.StartsWith("pbkdf2:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = storedHash.Split('$');
                if (parts.Length >= 3)
                {
                    string methodPart = parts[0];
                    string saltStr = parts[1];
                    string expectedHashHex = parts[2];

                    int iterations = 260000;
                    var subParts = methodPart.Split(':');
                    if (subParts.Length >= 3 && int.TryParse(subParts[2], out int iters))
                    {
                        iterations = iters;
                    }

                    byte[] salt = IsHex(saltStr) ? Convert.FromHexString(saltStr) : Encoding.UTF8.GetBytes(saltStr);
                    byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
                    string computedHashHex = Convert.ToHexString(computedHash).ToLower();

                    return string.Equals(computedHashHex, expectedHashHex.ToLower(), StringComparison.OrdinalIgnoreCase);
                }
            }

            // Fallback plain string compare
            return storedHash.Trim() == password.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in VerifyPassword: {ex.Message}");
            return false;
        }
    }

    private static bool IsHex(string str)
    {
        return str.All("0123456789abcdefABCDEF".Contains);
    }
}
