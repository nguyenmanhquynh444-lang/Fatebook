using System;
using System.Security.Cryptography;

namespace SecureChat.Common.Crypto
{
    /// <summary>
    /// Tiện ích mã hoá/giải mã RSA phục vụ trao đổi khoá phiên AES.
    /// Sử dụng RSA-2048 / OAEP-SHA256.
    /// </summary>
    public static class RSAUtil
    {
        private const int KEY_SIZE = 2048;

        public class RsaKeyPair
        {
            public string PublicKey { get; set; } = string.Empty;  // Base64 DER X.509
            public string PrivateKey { get; set; } = string.Empty; // Base64 DER PKCS#8
        }

        /// <summary>
        /// Sinh cặp khoá RSA-2048.
        /// </summary>
        public static RsaKeyPair GenerateKeyPair()
        {
            using (var rsa = RSA.Create(KEY_SIZE))
            {
                byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();

                return new RsaKeyPair
                {
                    PublicKey = Convert.ToBase64String(publicKeyBytes),
                    PrivateKey = Convert.ToBase64String(privateKeyBytes)
                };
            }
        }

        /// <summary>
        /// Mã hoá dữ liệu (ví dụ khoá AES) bằng RSA Public Key.
        /// </summary>
        public static byte[] Encrypt(byte[] data, string publicKeyBase64)
        {
            byte[] keyBytes = Convert.FromBase64String(publicKeyBase64);
            using (var rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            }
        }

        /// <summary>
        /// Giải mã dữ liệu bằng RSA Private Key.
        /// </summary>
        public static byte[] Decrypt(byte[] encryptedData, string privateKeyBase64)
        {
            byte[] keyBytes = Convert.FromBase64String(privateKeyBase64);
            using (var rsa = RSA.Create())
            {
                rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                return rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
            }
        }

        /// <summary>
        /// Tạo chữ ký điện tử cho dữ liệu bằng Private Key.
        /// </summary>
        public static byte[] Sign(byte[] data, string privateKeyBase64)
        {
            byte[] keyBytes = Convert.FromBase64String(privateKeyBase64);
            using (var rsa = RSA.Create())
            {
                rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        /// <summary>
        /// Xác minh chữ ký điện tử bằng Public Key.
        /// </summary>
        public static bool Verify(byte[] data, byte[] signature, string publicKeyBase64)
        {
            byte[] keyBytes = Convert.FromBase64String(publicKeyBase64);
            using (var rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}
