using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using System.Runtime.Versioning;

namespace Frontend.Services
{
    /// <summary>
    /// Handles all client-side backup and restore logic.
    /// 
    /// Zero-Knowledge Flow:
    /// - EXPORT: Fetch encrypted-at-rest data from server → Apply Argon2id key derivation + AES-256-GCM → Package into .zip
    /// - IMPORT: Unzip → Derive key from Vault Password + stored Salt → AES-256-GCM Decrypt → Send to server
    /// </summary>
    public class BackupService
    {
        private const int Argon2DegreeOfParallelism = 2;
        private const int Argon2MemorySize = 65536; // 64MB
        private const int Argon2Iterations = 4;
        private const int KeySizeBytes = 32;  // 256-bit
        private const int NonceSizeBytes = 12; // 96-bit (AES-GCM standard)
        private const int TagSizeBytes = 16;   // 128-bit (AES-GCM auth tag)
        private const int BackupVersion = 1;

        // ───────────────────────────────── EXPORT ─────────────────────────────────

        [SupportedOSPlatform("ios13.0")]
        [SupportedOSPlatform("tvos13.0")]
        [SupportedOSPlatform("maccatalyst13.1")]
        [SupportedOSPlatform("android21.0")]
        public async Task<byte[]> CreateBackupZipAsync(string vaultPassword)
        {
            // 1. Fetch export JSON from backend (data is still vault-key-encrypted at row level)
            var exportElement = await FetchExportAsync();

            // 2. Serialize to JSON bytes (JsonElement serializes cleanly back to its original JSON)
            string exportJson = JsonSerializer.Serialize(exportElement, new JsonSerializerOptions { WriteIndented = false });
            byte[] plaintext = Encoding.UTF8.GetBytes(exportJson);

            // 3. Generate a unique random salt for this backup
            byte[] salt = RandomNumberGenerator.GetBytes(32);

            // 4. Derive 256-bit AES key from vault password + random salt using Argon2id
            byte[] key = DeriveKey(vaultPassword, salt);

            // 5. Encrypt with AES-256-GCM
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            // 6. vault_data.bin format: [Nonce (12)] + [Tag (16)] + [Ciphertext]
            byte[] vaultData = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, vaultData, 0, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, vaultData, NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(ciphertext, 0, vaultData, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

            // 7. manifest.json — public, unencrypted metadata needed to re-derive the key
            var manifest = new
            {
                version = BackupVersion,
                created_at = DateTime.UtcNow.ToString("o"),
                argon2 = new
                {
                    salt = Convert.ToBase64String(salt),
                    iterations = Argon2Iterations,
                    memory_size = Argon2MemorySize,
                    degree_of_parallelism = Argon2DegreeOfParallelism,
                    key_size = KeySizeBytes
                }
            };
            byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // 8. Package into a ZIP in memory
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
                using (var entryStream = manifestEntry.Open())
                    await entryStream.WriteAsync(manifestBytes);

                var dataEntry = archive.CreateEntry("vault_data.bin", CompressionLevel.NoCompression);
                using (var entryStream = dataEntry.Open())
                    await entryStream.WriteAsync(vaultData);
            }

            // 9. Clear sensitive data from memory
            Array.Clear(key, 0, key.Length);
            Array.Clear(plaintext, 0, plaintext.Length);

            zipStream.Position = 0;
            return zipStream.ToArray();
        }

        // ───────────────────────────────── IMPORT ─────────────────────────────────

        [SupportedOSPlatform("ios13.0")]
        [SupportedOSPlatform("tvos13.0")]
        [SupportedOSPlatform("maccatalyst13.1")]
        [SupportedOSPlatform("android21.0")]
        public async Task<string> RestoreBackupAsync(string vaultPassword, Stream zipStream)
        {
            byte[]? manifestBytes = null;
            byte[]? vaultDataBytes = null;

            // 1. Extract files from the ZIP
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);

                    if (entry.Name == "manifest.json") manifestBytes = ms.ToArray();
                    else if (entry.Name == "vault_data.bin") vaultDataBytes = ms.ToArray();
                }
            }

            if (manifestBytes == null || vaultDataBytes == null)
                throw new Exception("Invalid backup file: missing required entries.");

            // 2. Parse manifest
            using var manifestDoc = JsonDocument.Parse(manifestBytes);
            var root = manifestDoc.RootElement;
            var argon2Node = root.GetProperty("argon2");

            byte[] salt = Convert.FromBase64String(argon2Node.GetProperty("salt").GetString()!);
            int iterations = argon2Node.GetProperty("iterations").GetInt32();
            int memorySize = argon2Node.GetProperty("memory_size").GetInt32();
            int parallelism = argon2Node.GetProperty("degree_of_parallelism").GetInt32();
            int keySize = argon2Node.GetProperty("key_size").GetInt32();

            // 3. Derive key using parameters from manifest
            byte[] key = DeriveKey(vaultPassword, salt, iterations, memorySize, parallelism, keySize);

            // 4. Unpack vault_data.bin: [Nonce (12)] + [Tag (16)] + [Ciphertext]
            if (vaultDataBytes.Length <= NonceSizeBytes + TagSizeBytes)
                throw new Exception("Backup data is corrupt or too short.");

            byte[] nonce = new byte[NonceSizeBytes];
            byte[] tag = new byte[TagSizeBytes];
            byte[] ciphertext = new byte[vaultDataBytes.Length - NonceSizeBytes - TagSizeBytes];

            Buffer.BlockCopy(vaultDataBytes, 0, nonce, 0, NonceSizeBytes);
            Buffer.BlockCopy(vaultDataBytes, NonceSizeBytes, tag, 0, TagSizeBytes);
            Buffer.BlockCopy(vaultDataBytes, NonceSizeBytes + TagSizeBytes, ciphertext, 0, ciphertext.Length);

            // 5. Decrypt
            byte[] plaintext = new byte[ciphertext.Length];
            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            // 6. Deserialize and send to backend
            string importJson = Encoding.UTF8.GetString(plaintext);

            // 7. Clear sensitive data
            Array.Clear(key, 0, key.Length);
            Array.Clear(plaintext, 0, plaintext.Length);

            // 8. POST to backend /api/backup/import
            var content = new StringContent(importJson, Encoding.UTF8, "application/json");
            var response = await ApiClient.Instance.PostAsync("api/backup/import", content);

            string responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Restore failed: {responseBody}");

            return responseBody;
        }

        // ───────────────────────────────── HELPERS ─────────────────────────────────

        private async Task<System.Text.Json.JsonElement> FetchExportAsync()
        {
            var response = await ApiClient.Instance.GetAsync("api/backup/export");
            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to fetch backup data from server: {err}");
            }
            string json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        }

        private byte[] DeriveKey(
            string password,
            byte[] salt,
            int iterations = Argon2Iterations,
            int memorySize = Argon2MemorySize,
            int parallelism = Argon2DegreeOfParallelism,
            int keySize = KeySizeBytes)
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                MemorySize = memorySize,
                Iterations = iterations
            };
            return argon2.GetBytes(keySize);
        }
    }
}
