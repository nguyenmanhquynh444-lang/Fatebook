using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SecureChat.Server.Crypto
{
    public static class CertGenerator
    {
        public const string PFX_PASSWORD = "server_password_2026";

        public static void GenerateCertificatesIfNotExist()
        {
            string certsDir = GetCertsDirectory();
            string caCertPath = Path.Combine(certsDir, "ca.crt");
            string serverPfxPath = Path.Combine(certsDir, "server.pfx");

            if (File.Exists(caCertPath) && File.Exists(serverPfxPath))
            {
                Console.WriteLine("[CertGenerator] Chứng chỉ SSL đã tồn tại.");
                return;
            }

            Console.WriteLine("[CertGenerator] Chưa tìm thấy chứng chỉ SSL. Đang tự động sinh CA và Server certificate...");

            try
            {
                // 1. Sinh Root CA
                using (RSA caRsa = RSA.Create(2048))
                {
                    var caRequest = new CertificateRequest(
                        "CN=SecureChat Root CA, O=VHU, C=VN",
                        caRsa,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);

                    caRequest.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(true, false, 0, true));

                    caRequest.CertificateExtensions.Add(
                        new X509KeyUsageExtension(
                            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                            true));

                    var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
                    var notAfter = notBefore.AddYears(10);

                    using (X509Certificate2 caCert = caRequest.CreateSelfSigned(notBefore, notAfter))
                    {
                        // Lưu CA certificate dưới dạng DER (để client import/verify)
                        byte[] caCertBytes = caCert.Export(X509ContentType.Cert);
                        File.WriteAllBytes(caCertPath, caCertBytes);
                        Console.WriteLine($"[CertGenerator] Đã lưu CA Certificate: {caCertPath}");

                        // 2. Sinh Server Certificate và ký bởi Root CA
                        using (RSA serverRsa = RSA.Create(2048))
                        {
                            var serverRequest = new CertificateRequest(
                                "CN=localhost, O=SecureChat, C=VN",
                                serverRsa,
                                HashAlgorithmName.SHA256,
                                RSASignaturePadding.Pkcs1);

                            // SAN (Subject Alternative Names) cho localhost và 127.0.0.1
                            var sanBuilder = new SubjectAlternativeNameBuilder();
                            sanBuilder.AddDnsName("localhost");
                            sanBuilder.AddIpAddress(IPAddress.Loopback);
                            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
                            serverRequest.CertificateExtensions.Add(sanBuilder.Build());

                            serverRequest.CertificateExtensions.Add(
                                new X509KeyUsageExtension(
                                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                                    true));

                            serverRequest.CertificateExtensions.Add(
                                new X509EnhancedKeyUsageExtension(
                                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                                    true));

                            // Tạo số Serial ngẫu nhiên
                            byte[] serialNumber = new byte[8];
                            RandomNumberGenerator.Fill(serialNumber);

                            var serverNotAfter = notBefore.AddYears(3);

                            using (X509Certificate2 signedCert = serverRequest.Create(
                                caCert,
                                notBefore,
                                serverNotAfter,
                                serialNumber))
                            {
                                // Gắn private key vào cert đã ký
                                using (X509Certificate2 certWithKey = signedCert.CopyWithPrivateKey(serverRsa))
                                {
                                    // Xuất ra định dạng PFX chứa cả key
                                    byte[] pfxBytes = certWithKey.Export(X509ContentType.Pfx, PFX_PASSWORD);
                                    File.WriteAllBytes(serverPfxPath, pfxBytes);
                                    Console.WriteLine($"[CertGenerator] Đã lưu Server PFX Certificate: {serverPfxPath}");
                                }
                            }
                        }
                    }
                }
                Console.WriteLine("[CertGenerator] Tạo chứng chỉ SSL hoàn tất!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CertGenerator] Lỗi tạo chứng chỉ SSL: {ex.Message}");
                throw;
            }
        }

        public static string GetCertsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "SecureChat.Server.slnx")))
                {
                    string certsDir = Path.Combine(dir.FullName, "certs");
                    if (!Directory.Exists(certsDir))
                    {
                        Directory.CreateDirectory(certsDir);
                    }
                    return certsDir;
                }
                dir = dir.Parent;
            }
            string fallbackDir = Path.Combine(baseDir, "certs");
            if (!Directory.Exists(fallbackDir))
            {
                Directory.CreateDirectory(fallbackDir);
            }
            return fallbackDir;
        }
    }
}
