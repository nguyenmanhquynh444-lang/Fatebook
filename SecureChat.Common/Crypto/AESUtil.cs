using System;
using System.Security.Cryptography;
using System.Text;

namespace SecureChat.Common.Crypto
{
    /// <summary>
    /// Tiện ích mã hoá/giải mã AES-256-GCM.
    /// Giao thức mã hoá kết hợp: Base64( IV [12 bytes] + Ciphertext [N bytes] + AuthTag [16 bytes] ).
    /// </summary>
    public static class AESUtil
    {
        private const int KEY_SIZE = 32;     // 32 bytes = 256 bits
        private const int IV_LENGTH = 12;    // 12 bytes GCM IV
        private const int TAG_LENGTH = 16;   // 16 bytes Authentication Tag (128 bits)

        /// <summary>
        /// Sinh khoá AES-256 ngẫu nhiên.
        /// </summary>
        public static byte[] GenerateKey()
        {
            byte[] key = new byte[KEY_SIZE];
            RandomNumberGenerator.Fill(key);
            return key;
        }

        /// <summary>
        /// Mã hoá văn bản và trả về chuỗi Base64 chứa (IV + Ciphertext + Tag).
        /// </summary>
        public static string Encrypt(string plaintext, byte[] key)
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encryptedBytes = EncryptBytes(plaintextBytes, key);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Mã hoá mảng byte dữ liệu (dùng cho truyền file).
        /// </summary>
        public static byte[] EncryptBytes(byte[] data, byte[] key)
        {
            byte[] iv = new byte[IV_LENGTH];
            RandomNumberGenerator.Fill(iv);

            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[TAG_LENGTH];

            using (var aesGcm = new AesGcm(key, TAG_LENGTH))
            {
                aesGcm.Encrypt(iv, data, ciphertext, tag);
            }

            // Gộp IV + Ciphertext + Tag
            byte[] combined = new byte[IV_LENGTH + ciphertext.Length + TAG_LENGTH];
            Buffer.BlockCopy(iv, 0, combined, 0, IV_LENGTH);
            Buffer.BlockCopy(ciphertext, 0, combined, IV_LENGTH, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, IV_LENGTH + ciphertext.Length, TAG_LENGTH);

            return combined;
        }

        /// <summary>
        /// Giải mã chuỗi Base64 (IV + Ciphertext + Tag) và trả về văn bản gốc.
        /// </summary>
        public static string Decrypt(string base64Ciphertext, byte[] key)
        {
            byte[] combined = Convert.FromBase64String(base64Ciphertext);
            byte[] plainBytes = DecryptBytes(combined, key);
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Giải mã mảng byte (IV + Ciphertext + Tag) và trả về mảng byte gốc.
        /// </summary>
        public static byte[] DecryptBytes(byte[] combined, byte[] key)
        {
            if (combined.Length < IV_LENGTH + TAG_LENGTH)
            {
                throw new ArgumentException("Dữ liệu mã hoá quá ngắn, không đúng định dạng GCM.");
            }

            // Tách IV
            byte[] iv = new byte[IV_LENGTH];
            Buffer.BlockCopy(combined, 0, iv, 0, IV_LENGTH);

            // Xác định chiều dài ciphertext
            int ciphertextLength = combined.Length - IV_LENGTH - TAG_LENGTH;
            byte[] ciphertext = new byte[ciphertextLength];
            Buffer.BlockCopy(combined, IV_LENGTH, ciphertext, 0, ciphertextLength);

            // Tách Authentication Tag
            byte[] tag = new byte[TAG_LENGTH];
            Buffer.BlockCopy(combined, IV_LENGTH + ciphertextLength, tag, 0, TAG_LENGTH);

            byte[] plaintext = new byte[ciphertextLength];

            using (var aesGcm = new AesGcm(key, TAG_LENGTH))
            {
                aesGcm.Decrypt(iv, ciphertext, tag, plaintext);
            }

            return plaintext;
        }
    }
}
