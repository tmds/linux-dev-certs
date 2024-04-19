using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static LinuxDevCerts.ProcessHelper;

namespace LinuxDevCerts;

partial class CertificateManager
{
    private const string LocalhostCASubject = "O=ASP.NET Core dev CA";
    public const int RsaCACertMinimumKeySizeInBits = RSAMinimumKeySizeInBits;

    private X509Certificate2? _caCertificate;

    internal void InstallAndTrust()
    {
        Console.WriteLine("Removing all existing certificates.");
        Execute("dotnet", "dev-certs", "https", "--clean");

        Console.WriteLine("Creating CA certificate.");
        _caCertificate = CreateAspNetDevelopmentCACertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(10));

        Console.WriteLine("Installing CA certificate.");
        char[] caCertPem = PemEncoding.Write("CERTIFICATE", _caCertificate.Export(X509ContentType.Cert));
        string certFolder = "/etc/pki/ca-trust/source/anchors/";
        string username = Environment.UserName;
        string certFileName = $"aspnet-{username}.pem";
        SudoExecute(new[] { "tee", Path.Combine(certFolder, certFileName) }, caCertPem);
        SudoExecute("update-ca-trust", "extract");

        Console.WriteLine("Creating development certificate.");
        var devCert = CreateAspNetCoreHttpsDevelopmentCertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        Console.WriteLine("Installing development certificate.");
        SaveCertificateCore(devCert, StoreName.My, StoreLocation.CurrentUser);
    }

    // Creates a cert issued by _caCertificate.
    private X509Certificate2 CreateAspNetDevelopmentCertificate(X500DistinguishedName subject, List<X509Extension> extensions, DateTimeOffset notBefore, DateTimeOffset notAfter)
        => CreateCertificate(subject, extensions, notBefore, notAfter, RSAMinimumKeySizeInBits, _caCertificate);

    private static X509Certificate2 CreateAspNetDevelopmentCACertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
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

        var extensions = new List<X509Extension>
            {
                basicConstraints,
                keyUsage
            };

        return CreateCertificate(subject, extensions, notBefore, notAfter, RsaCACertMinimumKeySizeInBits, issuerCert: null /* self-signed */);
    }

    static internal X509Certificate2 CreateCertificate(
        X500DistinguishedName subject,
        IEnumerable<X509Extension> extensions,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        int minimumKeySize,
        X509Certificate2? issuerCert)
    {
        using var key = CreateKeyMaterial(minimumKeySize);

        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        foreach (var extension in extensions)
        {
            request.CertificateExtensions.Add(extension);
        }

        var result = issuerCert == null ? request.CreateSelfSigned(notBefore, notAfter)
                                        : request.Create(issuerCert, notBefore, notAfter, Guid.NewGuid().ToByteArray());

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
}