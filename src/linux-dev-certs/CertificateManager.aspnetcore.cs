// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LinuxDevCerts;

partial class CertificateManager
{
    // Copied from aspnetcore CertificateManager.cs.
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

    // Copied from aspnetcore CertificateManager.cs.
    // Call to 'CreateSelfSignedCertificate' at the end replaced by 'CreateAspNetDevelopmentCertificate'.
    internal X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
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
}