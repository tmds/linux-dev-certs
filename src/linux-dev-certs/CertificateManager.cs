// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LinuxDevCerts
{
    using static ProcessHelper;

    internal enum CertificateKeyExportFormat
    {
        Pfx,
        Pem,
    }

    class CertificateManager
    {
        internal const int CurrentAspNetCoreCertificateVersion = 2;
        internal const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
        internal const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";

        private const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
        private const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";

        private const string LocalhostHttpsDnsName = "localhost";
        private const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;

        public const int RSAMinimumKeySizeInBits = 2048;

        private int AspNetHttpsCertificateVersion => CurrentAspNetCoreCertificateVersion;
        public string Subject => LocalhostHttpsDistinguishedName;

        internal X509Certificate2 SaveCertificate(X509Certificate2 certificate)
        {
            // var name = StoreName.My;
            // var location = StoreLocation.CurrentUser;

//             if (Log.IsEnabled())
            {
//                 Log.SaveCertificateInStoreStart(GetDescription(certificate), name, location);
            }

            certificate = SaveCertificateCore(certificate);

//             Log.SaveCertificateInStoreEnd();
            return certificate;
        }

        protected X509Certificate2 SaveCertificateCore(X509Certificate2 certificate)
        {
            var export = certificate.Export(X509ContentType.Pkcs12, "");
            certificate.Dispose();
            certificate = new X509Certificate2(export, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            Array.Clear(export, 0, export.Length);

            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
            };

            return certificate;
        }

        internal void ExportCertificate(X509Certificate2 certificate, string path, bool includePrivateKey, string password, CertificateKeyExportFormat format)
        {
//             if (Log.IsEnabled())
            {
//                 Log.ExportCertificateStart(GetDescription(certificate), path, includePrivateKey);
            }

            if (includePrivateKey && password == null)
            {
//                 Log.NoPasswordForCertificate();
            }

            var targetDirectoryPath = Path.GetDirectoryName(path);
            if (targetDirectoryPath != "")
            {
//                 Log.CreateExportCertificateDirectory(targetDirectoryPath);
                Directory.CreateDirectory(targetDirectoryPath);
            }

            byte[] bytes;
            byte[] keyBytes;
            byte[] pemEnvelope = null;
            RSA key = null;

            try
            {
                if (includePrivateKey)
                {
                    switch (format)
                    {
                        case CertificateKeyExportFormat.Pfx:
                            bytes = certificate.Export(X509ContentType.Pkcs12, password);
                            break;
                        case CertificateKeyExportFormat.Pem:
                            key = certificate.GetRSAPrivateKey();

                            char[] pem;
                            if (password != null)
                            {
                                keyBytes = key.ExportEncryptedPkcs8PrivateKey(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100000));
                                pem = PemEncoding.Write("ENCRYPTED PRIVATE KEY", keyBytes);
                                pemEnvelope = Encoding.ASCII.GetBytes(pem);
                            }
                            else
                            {
                                // Export the key first to an encrypted PEM to avoid issues with System.Security.Cryptography.Cng indicating that the operation is not supported.
                                // This is likely by design to avoid exporting the key by mistake.
                                // To bypass it, we export the certificate to pem temporarily and then we import it and export it as unprotected PEM.
                                keyBytes = key.ExportEncryptedPkcs8PrivateKey("", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1));
                                pem = PemEncoding.Write("ENCRYPTED PRIVATE KEY", keyBytes);
                                key.Dispose();
                                key = RSA.Create();
                                key.ImportFromEncryptedPem(pem, "");
                                Array.Clear(keyBytes, 0, keyBytes.Length);
                                Array.Clear(pem, 0, pem.Length);
                                keyBytes = key.ExportPkcs8PrivateKey();
                                pem = PemEncoding.Write("PRIVATE KEY", keyBytes);
                                pemEnvelope = Encoding.ASCII.GetBytes(pem);
                            }

                            Array.Clear(keyBytes, 0, keyBytes.Length);
                            Array.Clear(pem, 0, pem.Length);

                            bytes = Encoding.ASCII.GetBytes(PemEncoding.Write("CERTIFICATE", certificate.Export(X509ContentType.Cert)));
                            break;
                        default:
                            throw new InvalidOperationException("Unknown format.");
                    }
                }
                else
                {
                    if (format == CertificateKeyExportFormat.Pem)
                    {
                        bytes = Encoding.ASCII.GetBytes(PemEncoding.Write("CERTIFICATE", certificate.Export(X509ContentType.Cert)));
                    }
                    else
                    {
                        bytes = certificate.Export(X509ContentType.Cert);
                    }
                }
            }
// //             catch (Exception e) when (Log.IsEnabled())
//             {
// //                 Log.ExportCertificateError(e.ToString());
//                 throw;
//             }
            finally
            {
                key?.Dispose();
            }

            try
            {
//                 Log.WriteCertificateToDisk(path);
                File.WriteAllBytes(path, bytes);
            }
// //             catch (Exception ex) when (Log.IsEnabled())
//             {
// //                 Log.WriteCertificateToDiskError(ex.ToString());
//                 throw;
            // }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }

            if (includePrivateKey && format == CertificateKeyExportFormat.Pem)
            {
                try
                {
                    var keyPath = Path.ChangeExtension(path, ".key");
//                     Log.WritePemKeyToDisk(keyPath);
                    File.WriteAllBytes(keyPath, pemEnvelope);
                }
// //                 catch (Exception ex) when (Log.IsEnabled())
//                 {
// //                     Log.WritePemKeyToDiskError(ex.ToString());
//                     throw;
//                 }
                finally
                {
                    Array.Clear(pemEnvelope, 0, pemEnvelope.Length);
                }
            }
        }

        internal X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter, X509Certificate2 issuerCert)
        {
            var subject = new X500DistinguishedName(Subject);
            var extensions = new List<X509Extension>();
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(LocalhostHttpsDnsName);

            var keyUsage = new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: true);
            var enhancedKeyUsage = new X509EnhancedKeyUsageExtension(
                new OidCollection() {
                    new Oid(
                        ServerAuthenticationEnhancedKeyUsageOid,
                        ServerAuthenticationEnhancedKeyUsageOidFriendlyName)
                },
                critical: true);

            var basicConstraints = new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true);

            byte[] bytePayload;

            if (AspNetHttpsCertificateVersion != 0)
            {
                bytePayload = new byte[1];
                bytePayload[0] = (byte)AspNetHttpsCertificateVersion;
            }
            else
            {
                bytePayload = Encoding.ASCII.GetBytes(AspNetHttpsOidFriendlyName);
            }

            var aspNetHttpsExtension = new X509Extension(
                new AsnEncodedData(
                    new Oid(AspNetHttpsOid, AspNetHttpsOidFriendlyName),
                    bytePayload),
                critical: false);

            extensions.Add(basicConstraints);
            extensions.Add(keyUsage);
            extensions.Add(enhancedKeyUsage);
            extensions.Add(sanBuilder.Build(critical: true));
            extensions.Add(aspNetHttpsExtension);

            var certificate = CreateCertificate(subject, extensions, notBefore, notAfter, RSAMinimumKeySizeInBits, issuerCert);
            return certificate;
        }

        static internal X509Certificate2 CreateCertificate(
            X500DistinguishedName subject,
            IEnumerable<X509Extension> extensions,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            int minimumKeySize,
            X509Certificate2 issuerCert)
        {
            using var key = CreateKeyMaterial(minimumKeySize);

            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            foreach (var extension in extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            var result = issuerCert == null ? request.CreateSelfSigned(notBefore, notAfter) :
                                    request.Create(issuerCert, notBefore, notAfter, Guid.NewGuid().ToByteArray());

            return result.HasPrivateKey ? result : result.CopyWithPrivateKey(key);

            RSA CreateKeyMaterial(int minimumKeySize)
            {
                var rsa = RSA.Create(minimumKeySize);
                if (rsa.KeySize < minimumKeySize)
                {
                    throw new InvalidOperationException($"Failed to create a key with a size of {minimumKeySize} bits");
                }

                return rsa;
            }
        }

        // -- code above was taken from aspnetcore and slightly modified.

        internal void InstallAndTrust()
        {
            Console.WriteLine("Removing all existing certificates.");
            Execute("dotnet", "dev-certs", "https", "--clean");

            Console.WriteLine("Creating CA certificate.");
            var caCert = CreateAspNetDevelopmentCACertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(10));

            Console.WriteLine("Installing CA certificate.");
            char[] caCertPem = PemEncoding.Write("CERTIFICATE", caCert.Export(X509ContentType.Cert));
            string certFolder = "/etc/pki/ca-trust/source/anchors/";
            string username = Environment.UserName;
            string certFileName = $"aspnet-{username}.pem";
            SudoExecute(new[] { "tee", Path.Combine(certFolder, certFileName)}, caCertPem);
            SudoExecute("update-ca-trust", "extract");

            Console.WriteLine("Creating development certificate.");
            var devCert = CreateAspNetCoreHttpsDevelopmentCertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), caCert);
            Console.WriteLine("Installing development certificate.");
            SaveCertificateCore(devCert);
        }

        private static bool IsInstalled(string program)
        {
            string pathEnvVar = Environment.GetEnvironmentVariable("PATH");
            if (pathEnvVar != null)
            {
                foreach (var subPath in pathEnvVar.Split(':', StringSplitOptions.RemoveEmptyEntries))
                {
                    return true;
                }
            }
            return false;
        }

        private const string LocalhostCASubject = "O=ASP.NET Core dev CA";
        public const int RsaCACertMinimumKeySizeInBits = 2048;

        private X509Certificate2 CreateAspNetDevelopmentCACertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
        {
            string certOwner = $"{Environment.UserName}@{Environment.MachineName}";
            string distinguishedName = $"{LocalhostCASubject},OU={certOwner}";
            var subject = new X500DistinguishedName(distinguishedName);

            var keyUsage = new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true);

            var basicConstraints = new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 1,
                critical: true);

            var extensions = new List<X509Extension>();
            extensions.Add(basicConstraints);
            extensions.Add(keyUsage);

            return CreateCertificate(subject, extensions, notBefore, notAfter, RsaCACertMinimumKeySizeInBits, issuerCert: null /* self-signed */);
        }
    }
}