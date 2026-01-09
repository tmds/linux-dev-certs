using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static LinuxDevCerts.ProcessHelper;

namespace LinuxDevCerts;

partial class CertificateManager
{
    private const string LocalhostCASubject = "O=ASP.NET Core dev CA";
    public const int RsaCACertMinimumKeySizeInBits = RSAMinimumKeySizeInBits;

    private X509Certificate2? _caCertificate;

    public bool InstallAndTrust(bool installDeps)
    {
        if (Environment.IsPrivilegedProcess)
        {
            Console.Error.WriteLine("The tool is running with elevated privileges. You should run under the user account of the developer.");
            return false;
        }
        string username = Environment.UserName;
        string certificateId = $"aspnet-dev-{username}";

        SystemCertificateStore systemCertStore = new();
        if (!systemCertStore.IsSupported)
        {
            Console.Error.WriteLine($"Can not determine location to install CA certificate on {RuntimeInformation.OSDescription}.");
            return false;
        }
        var additionalStores = FindAdditionalCertificateStores();

        ConsoleColor color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Some operations require root. You may be prompted for your 'sudo' password.");
        Console.ForegroundColor = color;

        HashSet<Dependency> dependencies = new();
        systemCertStore.AddDependencies(dependencies);
        foreach (ICertificateStore store in additionalStores)
        {
            store.AddDependencies(dependencies);
        }
        if (!CheckDependencies(dependencies, installMissing: installDeps))
        {
            return false;
        }

        Console.WriteLine("Creating CA certificate.");
        _caCertificate = CreateAspNetDevelopmentCACertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(10));

        Console.WriteLine("Installing CA certificate.");
        if (!systemCertStore.TryInstallCertificate(certificateId, _caCertificate))
        {
            Console.Error.WriteLine("Failed to install certificate.");
            return false;
        }

        Console.WriteLine("Removing existing development certificates.");
        Execute("dotnet", "dev-certs", "https", "--clean");
        Console.WriteLine("Creating development certificate.");
        var devCert = CreateAspNetCoreHttpsDevelopmentCertificate(DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        Console.WriteLine("Installing development certificate.");
        devCert = SaveCertificateCore(devCert, StoreName.My, StoreLocation.CurrentUser);

        bool isSuccess = true;
        foreach (ICertificateStore store in additionalStores)
        {
            Console.WriteLine($"Installing development certificate to {store.Name}.");
            if (!store.TryInstallCertificate(certificateId, devCert))
            {
                isSuccess = false;
                Console.Error.WriteLine("Failed to install certificate.");
            }
        }
        return isSuccess;
    }

    private string[]? _searchPaths;
    private string[] SearchPaths => _searchPaths ??= (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':');

    private bool IsProgramFound(string program)
    {
        bool found = false;
        foreach (var path in SearchPaths)
        {
            string filename = Path.Combine(path, program);
            if (File.Exists(filename))
            {
                found = true;
            }
        }
        return found;
    }

    private bool CheckDependencies(HashSet<Dependency> dependencies, bool installMissing = true)
    {
        // Remove dependencies that are met.
        HashSet<Dependency> unmetDependencies = new();
        string pathEnvVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        string[] paths = pathEnvVar.Split(':');
        foreach (var dependency in dependencies)
        {
            if (!IsProgramFound(dependency.ProgramName))
            {
                unmetDependencies.Add(dependency);
            }
        }

        if (unmetDependencies.Count == 0)
        {
            return true;
        }

        // Find the package names.
        HashSet<string> packagesToInstall = unmetDependencies.Select(dep => dep.PackageName).ToHashSet();

        bool hasSudo = IsProgramFound("sudo");
        if (installMissing && hasSudo)
        {
            Console.WriteLine($"The following packages are missing: {string.Join(", ", packagesToInstall)}.");
            Console.WriteLine($"Installing missing packages.");
            string[] command;
            if (OSFlavor.IsFedoraLike)
            {
                command = ["dnf", "install", "-y", ..packagesToInstall];
            }
            else if (OSFlavor.IsDebianLike)
            {
                command = ["apt-get", "install", "-y", ..packagesToInstall];
            }
            else if (OSFlavor.IsGentooLike)
            {
                command = ["emerge", ..packagesToInstall];
            }
            else if (OSFlavor.IsArchLike)
            {
                command = ["pacman", "-S", "-y", ..packagesToInstall];
            }
            else if (OSFlavor.IsSlackLike)
            {
                command = ["slackpkg", "install", ..packagesToInstall];
            }
            else
            {
                command = [];
                OSFlavor.ThrowNotSupported();
            }
            ProcessHelper.SudoExecute(command);

            return true;
        }
        else
        {
            ConsoleColor color = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"There are missing dependencies.");
            Console.Error.WriteLine("You can install them by executing the following command:");

            Console.ForegroundColor = ConsoleColor.Yellow;
            if (OSFlavor.IsFedoraLike)
            {
                Console.Error.WriteLine($"    dnf install {string.Join(", ", packagesToInstall)}");
            }
            else if (OSFlavor.IsDebianLike)
            {
                Console.Error.WriteLine($"    apt-get install {string.Join(", ", packagesToInstall)}");
            }
            else if (OSFlavor.IsGentooLike)
            {
                Console.Error.WriteLine($"    emerge {string.Join(", ", packagesToInstall)}");
            }
            else if (OSFlavor.IsArchLike)
            {
                Console.Error.WriteLine($"    pacman -S {string.Join(", ", packagesToInstall)}");
            }
            else if (OSFlavor.IsSlackLike)
            {
                Console.Error.WriteLine($"    slackpkg install {string.Join(", ", packagesToInstall)}");
            }
            else
            {
                Console.ForegroundColor = color;
                OSFlavor.ThrowNotSupported();
            }
            Console.ForegroundColor = color;

            return false;
        }
    }

    private List<ICertificateStore> FindAdditionalCertificateStores()
    {
        List<ICertificateStore> stores = new();

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);

        // Snap applications don't use '/etc/ssl' certificates. Add the certificate explicitly.
        // https://bugs.launchpad.net/ubuntu/+source/chromium-browser/+bug/1901586
        string firefoxSnapUserDirectory = Path.Combine(home, "snap/firefox/common/.mozilla/firefox");
        if (Directory.Exists(firefoxSnapUserDirectory))
        {
            FindFirefoxCertificateStores(firefoxSnapUserDirectory, stores);
        }

        string firefoxUserDirectory = Path.Combine(home, ".mozilla/firefox");
        if (OSFlavor.IsGentooLike && Directory.Exists(firefoxUserDirectory))
        {
            FindFirefoxCertificateStores(firefoxUserDirectory, stores, "LibreWolf");
        }

        string librewolfUserDirectory = Path.Combine(home, ".librewolf");
        if (OSFlavor.IsGentooLike && Directory.Exists(librewolfUserDirectory))
        {
            FindFirefoxCertificateStores(librewolfUserDirectory, stores, "LibreWolf");
        }

        return stores;
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