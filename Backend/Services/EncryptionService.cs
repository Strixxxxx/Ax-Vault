using System;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Services
{
    public class EncryptionService
    {
        // Removed static _key. Encryption now requires a key to be passed.
        // This enforces Zero-Knowledge as the server does not store the key (Vault Key).

        public EncryptionService()
        {
        }

        /// <summary>
        /// Derives a 32-byte encryption key from the user's Vault Key using SHA256.
        /// </summary>
        public string DeriveKeyFromVaultKey(string vaultKey)
        {
            if (string.IsNullOrEmpty(vaultKey))
            {
                throw new ArgumentNullException(nameof(vaultKey));
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(vaultKey));
                return Convert.ToBase64String(keyBytes);
            }
        }

        public string Encrypt(string plainText, string keyBase64)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            if (string.IsNullOrEmpty(keyBase64)) throw new ArgumentNullException(nameof(keyBase64));

            byte[] key = Convert.FromBase64String(keyBase64);
            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256 bits).");

            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            // Generate a random nonce. The nonce + tag requires 28 bytes.
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainTextBytes.Length];
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

            using (var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize))
            {
                aesGcm.Encrypt(nonce, plainTextBytes, cipherText, tag);
            }

            // Combine nonce, ciphertext, and tag for storage.
            byte[] encryptedData = new byte[nonce.Length + cipherText.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, encryptedData, 0, nonce.Length);
            Buffer.BlockCopy(cipherText, 0, encryptedData, nonce.Length, cipherText.Length);
            Buffer.BlockCopy(tag, 0, encryptedData, nonce.Length + cipherText.Length, tag.Length);

            return Convert.ToBase64String(encryptedData);
        }

        public string Decrypt(string encryptedText, string keyBase64)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            if (string.IsNullOrEmpty(keyBase64)) throw new ArgumentNullException(nameof(keyBase64));

            byte[] key = Convert.FromBase64String(keyBase64);
            if (key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256 bits).");

            byte[] encryptedData = Convert.FromBase64String(encryptedText);

            // Extract nonce, ciphertext, and tag.
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonce.Length);

            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
            Buffer.BlockCopy(encryptedData, encryptedData.Length - tag.Length, tag, 0, tag.Length);

            byte[] cipherText = new byte[encryptedData.Length - nonce.Length - tag.Length];
            Buffer.BlockCopy(encryptedData, nonce.Length, cipherText, 0, cipherText.Length);

            byte[] decryptedBytes = new byte[cipherText.Length];

            using (var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize))
            {
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedBytes);
            }

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public static string GenerateDeterministicHash(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
