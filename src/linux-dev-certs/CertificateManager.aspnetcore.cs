// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LinuxDevCerts;

partial class CertificateManager
{
    // Copied from aspnetcore CertificateManager.cs.
    internal const int CurrentAspNetCoreCertificateVersion = 6;
    internal const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
    internal const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";
    private const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
    private const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";
    internal const string SubjectKeyIdentifierOid = "2.5.29.14";
    internal const string AuthorityKeyIdentifierOid = "2.5.29.35";

    // dns names of the host from a container
    private const string LocalhostDockerHttpsDnsName = "host.docker.internal";
    private const string ContainersDockerHttpsDnsName = "host.containers.internal";

    // wildcard DNS names
    private const string LocalhostWildcardHttpsDnsName = "*.dev.localhost";
    private const string InternalWildcardHttpsDnsName = "*.dev.internal";

    // main cert subject
    private const string LocalhostHttpsDnsName = "localhost";
    private const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;
    public const int RSAMinimumKeySizeInBits = 2048;
    private int AspNetHttpsCertificateVersion => CurrentAspNetCoreCertificateVersion;
    public string Subject => LocalhostHttpsDistinguishedName;

    // Copied from aspnetcore CertificateManager.cs.
    // Call to 'CreateSelfSignedCertificate' at the end replaced by 'CreateAspNetDevelopmentCertificate'.
    internal X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var subject = new X500DistinguishedName(Subject);
        var extensions = new List<X509Extension>();
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(LocalhostHttpsDnsName);
        sanBuilder.AddDnsName(LocalhostWildcardHttpsDnsName);
        sanBuilder.AddDnsName(InternalWildcardHttpsDnsName);
        sanBuilder.AddDnsName(LocalhostDockerHttpsDnsName);
        sanBuilder.AddDnsName(ContainersDockerHttpsDnsName);
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);

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

        var certificate = CreateAspNetDevelopmentCertificate(subject, extensions, notBefore, notAfter);
        return certificate;
    }

    // Copied from aspnetcore UnixCertificateManager.cs
    protected X509Certificate2 SaveCertificateCore(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation)
    {
        var export = certificate.Export(X509ContentType.Pkcs12, "");
        certificate.Dispose();
        certificate = new X509Certificate2(export, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        Array.Clear(export, 0, export.Length);

        using (var store = new X509Store(storeName, storeLocation))
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();
        };

        return certificate;
    }

    // Copied from aspnetcore CertificateManager.cs
    internal static void ExportCertificate(X509Certificate2 certificate, string path, bool includePrivateKey, string? password, CertificateKeyExportFormat format)
    {
        // if (Log.IsEnabled())
        // {
        //     Log.ExportCertificateStart(GetDescription(certificate), path, includePrivateKey);
        // }

        // if (includePrivateKey && password == null)
        // {
        //     Log.NoPasswordForCertificate();
        // }

        var targetDirectoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(targetDirectoryPath))
        {
            // Log.CreateExportCertificateDirectory(targetDirectoryPath);
            Directory.CreateDirectory(targetDirectoryPath);
        }

        byte[] bytes;
        byte[] keyBytes;
        byte[]? pemEnvelope = null;
        RSA? key = null;

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
                        key = certificate.GetRSAPrivateKey()!;

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
                            keyBytes = key.ExportEncryptedPkcs8PrivateKey(string.Empty, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1));
                            pem = PemEncoding.Write("ENCRYPTED PRIVATE KEY", keyBytes);
                            key.Dispose();
                            key = RSA.Create();
                            key.ImportFromEncryptedPem(pem, string.Empty);
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
        // catch (Exception e) when (Log.IsEnabled())
        // {
        //     Log.ExportCertificateError(e.ToString());
        //     throw;
        // }
        finally
        {
            key?.Dispose();
        }

        try
        {
            // Log.WriteCertificateToDisk(path);

            // Create a temp file with the correct Unix file mode before moving it to the expected path.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tempFilename = Path.GetTempFileName();
                File.Move(tempFilename, path, overwrite: true);
            }

            File.WriteAllBytes(path, bytes);
        }
        // catch (Exception ex) when (Log.IsEnabled())
        // {
        //     Log.WriteCertificateToDiskError(ex.ToString());
        //     throw;
        // }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
        }

        if (includePrivateKey && format == CertificateKeyExportFormat.Pem)
        {
            Debug.Assert(pemEnvelope != null);

            try
            {
                var keyPath = Path.ChangeExtension(path, ".key");
                // Log.WritePemKeyToDisk(keyPath);

                // Create a temp file with the correct Unix file mode before moving it to the expected path.
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var tempFilename = Path.GetTempFileName();
                    File.Move(tempFilename, keyPath, overwrite: true);
                }

                File.WriteAllBytes(keyPath, pemEnvelope);
            }
            // catch (Exception ex) when (Log.IsEnabled())
            // {
            //     Log.WritePemKeyToDiskError(ex.ToString());
            //     throw;
            // }
            finally
            {
                Array.Clear(pemEnvelope, 0, pemEnvelope.Length);
            }
        }
    }
}

// Copied from aspnetcore CertificateExportFormat.cs
internal enum CertificateKeyExportFormat
{
    Pfx,
    Pem,
}