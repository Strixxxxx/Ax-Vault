using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Backend.Services;

public class PasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32; // 256 bit
    // Argon2id parameters
    private const int DegreeOfParallelism = 4;
    private const int MemorySize = 65536; // 64 MB
    private const int Iterations = 4;

    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            var hash = argon2.GetBytes(KeySize);
            
            var saltString = Convert.ToBase64String(salt);
            var hashString = Convert.ToBase64String(hash);
            
            // Format: $argon2id$v=19$m=65536,t=4,p=4$salt$hash
            return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltString}${hashString}";
        }
    }
    
    public static bool VerifyPassword(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password)) return false;

        try
        {
            // Check if it's an Argon2id hash
            if (hash.StartsWith("$argon2id$"))
            {
                var parts = hash.Split('$');
                // parts[0] is empty, parts[1] is argon2id, parts[2] is v=19, parts[3] is param string (m=...,t=...,p=...), parts[4] is salt, parts[5] is hash
                // Example: $argon2id$v=19$m=65536,t=4,p=4$SALTBASE64$HASHBASE64
                if (parts.Length != 6) return false;

                var paramParts = parts[3].Split(',');
                var m = int.Parse(paramParts[0].Split('=')[1]);
                var t = int.Parse(paramParts[1].Split('=')[1]);
                var p = int.Parse(paramParts[2].Split('=')[1]);
                
                var salt = Convert.FromBase64String(parts[4]);
                var storedHash = Convert.FromBase64String(parts[5]);

                using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
                {
                    argon2.Salt = salt;
                    argon2.DegreeOfParallelism = p;
                    argon2.MemorySize = m;
                    argon2.Iterations = t;

                    var computedHash = argon2.GetBytes(storedHash.Length);
                    return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Derives a 32-byte (256-bit) encryption key from the Vault Password using Argon2id.
    /// This key is used for AES-GCM encryption/decryption.
    /// Returns the key as a byte array.
    /// </summary>
    public static byte[] DeriveKeyFromVaultPassword(string vaultPassword, byte[] salt)
    {
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(vaultPassword)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            var key = argon2.GetBytes(32); // 32 bytes for AES-256
            Console.WriteLine($"[DeriveKey] Salt: {BitConverter.ToString(salt)}");
            Console.WriteLine($"[DeriveKey] Key: {BitConverter.ToString(key)}");
            return key;
        }
    }

    /// <summary>
    /// Generates a deterministic Argon2id hash using a provided fixed salt.
    /// Used for searchable columns like UsernameHashed and EmailHashed.
    /// </summary>
    public static string HashDeterministic(string input, byte[] salt)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Use a smaller configuration for lookups to ensure speed? 
        // Or keep same security? Prompt says "hashed using argon2id". 
        // We will match the standard config for consistency/security.
        
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(input)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.MemorySize = MemorySize;
            argon2.Iterations = Iterations;

            var hash = argon2.GetBytes(KeySize);
            return Convert.ToBase64String(hash);
        }
    }

    /// <summary>
    /// Returns a fixed salt derived from the BACKEND_SECRET_KEY.
    /// Used for deterministic Argon2id operations.
    /// </summary>
    public static byte[] GetDeterministicSalt()
    {
        var secret = Environment.GetEnvironmentVariable("BACKEND_SECRET_KEY") ?? "fallback_secret_for_dev_only";
        using (var sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(secret)).AsSpan(0, 16).ToArray();
        }
    }
}
