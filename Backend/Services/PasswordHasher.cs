using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Backend.Services
{
    public class PasswordHasher
    {
        private readonly IConfiguration _configuration;
        private const int SaltSize = 16; 
        private const int KeySize = 32; 
        private const int DegreeOfParallelism = 4;
        private const int MemorySize = 65536; 
        private const int Iterations = 4;

        public PasswordHasher(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string? GetSetting(string key) => _configuration[key] ?? Environment.GetEnvironmentVariable(key);

        public string HashPassword(string password)
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
                
                return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltString}${hashString}";
            }
        }
        
        public bool VerifyPassword(string hash, string password)
        {
            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password)) return false;

            try
            {
                if (hash.StartsWith("$argon2id$"))
                {
                    var parts = hash.Split('$');
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

        public byte[] DeriveKeyFromVaultPassword(string vaultPassword, byte[] salt)
        {
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(vaultPassword)))
            {
                argon2.Salt = salt;
                argon2.DegreeOfParallelism = DegreeOfParallelism;
                argon2.MemorySize = MemorySize;
                argon2.Iterations = Iterations;

                return argon2.GetBytes(32); 
            }
        }

        public string HashDeterministic(string input, byte[] salt)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            
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

        public byte[] GetDeterministicSalt()
        {
            var secret = GetSetting("BACKEND_SECRET_KEY") ?? "fallback_secret_for_dev_only";
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(secret)).AsSpan(0, 16).ToArray();
            }
        }
    }
}
