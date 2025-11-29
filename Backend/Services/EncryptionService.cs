using System;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService()
        {
            var keyString = Environment.GetEnvironmentVariable("BACKEND_AES_256_KEY");
            if (string.IsNullOrEmpty(keyString) || keyString.Length < 32)
            {
                throw new InvalidOperationException("BACKEND_AES_256_KEY environment variable must be at least 32 characters long.");
            }
            // Use the first 32 bytes (256 bits) of the key string for the AES key.
            _key = Encoding.UTF8.GetBytes(keyString.Substring(0, 32));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            // Generate a random nonce. The nonce + tag requires 28 bytes.
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherText = new byte[plainTextBytes.Length];
            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

            using (var aesGcm = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
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

        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return string.Empty;
            }

            byte[] encryptedData = Convert.FromBase64String(encryptedText);

            // Extract nonce, ciphertext, and tag.
            byte[] nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonce.Length);

            byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
            Buffer.BlockCopy(encryptedData, encryptedData.Length - tag.Length, tag, 0, tag.Length);

            byte[] cipherText = new byte[encryptedData.Length - nonce.Length - tag.Length];
            Buffer.BlockCopy(encryptedData, nonce.Length, cipherText, 0, cipherText.Length);

            byte[] decryptedBytes = new byte[cipherText.Length];

            using (var aesGcm = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
            {
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedBytes);
            }

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}
