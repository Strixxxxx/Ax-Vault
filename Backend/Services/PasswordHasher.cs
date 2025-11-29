using System.Security.Cryptography;
using System.Text;

namespace Backend.Services;

public class PasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32; // 256 bit
    private const int Iterations = 10000;
    
    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA512,
            KeySize
        );
            
        var saltString = Convert.ToBase64String(salt);
        var hashString = Convert.ToBase64String(hash);
            
        return $"{Iterations}.{saltString}.{hashString}";
    }
    
    public static bool VerifyPassword(string hash, string password)
    {
        var parts = hash.Split('.');
        
        if (parts.Length != 3)
        {
            return false;
        }
        
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);
        
        var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            key.Length);
            
        return CryptographicOperations.FixedTimeEquals(hashToCompare, key);
    }
} 