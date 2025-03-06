using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Keys;
using Azure.Identity;

namespace InventoryService.Infrastructure.Security
{
    public class DataEncryption
    {
        private readonly SecuritySettings _settings;
        private readonly CryptographyClient? _cryptographyClient;

        public DataEncryption(IOptions<SecuritySettings> settings)
        {
            _settings = settings.Value;

            if (_settings.Encryption.EnableAtRest && !string.IsNullOrEmpty(_settings.Encryption.KeyVaultUrl))
            {
                var credential = new DefaultAzureCredential();
                var keyClient = new KeyClient(new Uri(_settings.Encryption.KeyVaultUrl), credential);
                var key = keyClient.GetKey(_settings.Encryption.EncryptionKeyName);
                _cryptographyClient = new CryptographyClient(key.Value.Id, credential);
            }
        }

        public string EncryptSensitiveData(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return plaintext;

            if (_cryptographyClient != null)
            {
                // Use Azure Key Vault for encryption
                var bytes = Encoding.UTF8.GetBytes(plaintext);
                var result = _cryptographyClient.Encrypt(EncryptionAlgorithm.RsaOaep, bytes);
                return Convert.ToBase64String(result.Ciphertext);
            }
            else
            {
                // Fallback to local encryption
                using var aes = Aes.Create();
                aes.GenerateKey();
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                
                // Write the IV to the start of the encrypted data
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plaintext);
                }

                var encryptedData = msEncrypt.ToArray();
                
                // Store the key securely (in production, this would be in a secure key store)
                // For demo purposes, we're just storing it with the data
                var combined = new byte[aes.Key.Length + encryptedData.Length];
                Buffer.BlockCopy(aes.Key, 0, combined, 0, aes.Key.Length);
                Buffer.BlockCopy(encryptedData, 0, combined, aes.Key.Length, encryptedData.Length);

                return Convert.ToBase64String(combined);
            }
        }

        public string DecryptSensitiveData(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return ciphertext;

            try
            {
                if (_cryptographyClient != null)
                {
                    // Use Azure Key Vault for decryption
                    var encryptedBytes = Convert.FromBase64String(ciphertext);
                    var result = _cryptographyClient.Decrypt(EncryptionAlgorithm.RsaOaep, encryptedBytes);
                    return Encoding.UTF8.GetString(result.Plaintext);
                }
                else
                {
                    // Fallback to local decryption
                    var combined = Convert.FromBase64String(ciphertext);

                    // Extract the key and encrypted data
                    var keyLength = 32; // AES-256
                    var key = new byte[keyLength];
                    var encryptedData = new byte[combined.Length - keyLength];
                    Buffer.BlockCopy(combined, 0, key, 0, keyLength);
                    Buffer.BlockCopy(combined, keyLength, encryptedData, 0, encryptedData.Length);

                    using var aes = Aes.Create();
                    aes.Key = key;

                    // Read the IV from the start of the encrypted data
                    var iv = new byte[aes.IV.Length];
                    Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    var actualEncryptedData = new byte[encryptedData.Length - iv.Length];
                    Buffer.BlockCopy(encryptedData, iv.Length, actualEncryptedData, 0, actualEncryptedData.Length);

                    using var decryptor = aes.CreateDecryptor();
                    using var msDecrypt = new MemoryStream(actualEncryptedData);
                    using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                    using var srDecrypt = new StreamReader(csDecrypt);

                    return srDecrypt.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new SecurityException("Failed to decrypt data", ex);
            }
        }
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception inner) : base(message, inner) { }
    }
} 