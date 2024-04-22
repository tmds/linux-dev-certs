using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static LinuxDevCerts.ProcessHelper;

namespace LinuxDevCerts;

partial class CertificateManager
{
    private const string LocalhostCASubject = "O=ASP.NET Core dev CA";
    public const int RsaCACertMinimumKeySizeInBits = RSAMinimumKeySizeInBits;

    private const string FedoraFamilyCaSourceDirectory = "/etc/pki/ca-trust/source/anchors";
    private const string DebianFamilyCaSourceDirectory = "/usr/local/share/ca-certificates";

    private X509Certificate2? _caCertificate;

    public void InstallAndTrust()
    {
        string username = Environment.UserName;
        string certificateName = $"aspnet-dev-{username}";

        Console.WriteLine("Removing existing development certificates.");
        Execute("dotnet", "dev-certs", "https", "--clean");

        Console.WriteLine("Creating CA certificate.");
        _caCertificate = CreateAspNetDevelopmentCACertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(10));

        Console.WriteLine("Installing CA certificate.");
        InstallCaCertificate(certificateName, _caCertificate);

        Console.WriteLine("Creating development certificate.");
        var devCert = CreateAspNetCoreHttpsDevelopmentCertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        Console.WriteLine("Installing development certificate.");
        devCert = SaveCertificateCore(devCert, StoreName.My, StoreLocation.CurrentUser);

        var additionalStores = FindAdditionaCertificateStores();
        foreach (ICertificateStore store in additionalStores)
        {
            Console.WriteLine($"Installing CA certificate to {store.Name}.");
            if (!store.TryInstallCertificate(certificateName, devCert))
            {
                Console.Error.WriteLine("Failed to install certificate.");
            }
        }
    }

    private List<ICertificateStore> FindAdditionaCertificateStores()
    {
        List<ICertificateStore> stores = new();

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);

        // Snap applications don't use '/etc/ssl' certificates. Add the certificate explicitly.
        // https://bugs.launchpad.net/ubuntu/+source/chromium-browser/+bug/1901586
        string firefoxSnapUserDirectory = Path.Combine(home, "snap/firefox/common/.mozilla/firefox");
        if (Directory.Exists(firefoxSnapUserDirectory))
        {
            FindFirefoxCertificateStores(firefoxSnapUserDirectory, stores);
        }

        return stores;
    }

    private static void InstallCaCertificate(string name, X509Certificate2 caCertificate)
    {
        // Only the public key is stored.
        // The private key will only exist in the memory of this program
        // and no other certificates can be signed with it after the program terminates.
        char[] caCertPem = PemEncoding.Write("CERTIFICATE", caCertificate.Export(X509ContentType.Cert));
        string certFilePath;
        string[] trustCommand;
        if (Directory.Exists(FedoraFamilyCaSourceDirectory))
        {
            certFilePath = $"{FedoraFamilyCaSourceDirectory}/{name}.pem";
            trustCommand = ["update-ca-trust", "extract"];
        }
        else if (Directory.Exists(DebianFamilyCaSourceDirectory))
        {
            certFilePath = $"{DebianFamilyCaSourceDirectory}/{name}.crt";
            trustCommand = ["update-ca-certificates"];
        }
        else
        {
            throw new NotSupportedException($"Can not determine location to install CA certificate on {RuntimeInformation.OSDescription}.");
        }
        SudoExecute(new[] { "tee", certFilePath }, caCertPem);
        SudoExecute(trustCommand);
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