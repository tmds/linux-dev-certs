using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LinuxDevCerts;

sealed class SystemCertificateStore : ICertificateStore
{
    private const string FedoraFamilyCaSourceDirectory = "/etc/pki/ca-trust/source/anchors";
    private const string DebianFamilyCaSourceDirectory = "/usr/local/share/ca-certificates";

    public string Name => "System Certificates";

    public bool TryInstallCertificate(string name, X509Certificate2 certificate)
    {
        // Only the public key is stored.
        // The private key will only exist in the memory of this program
        // and no other certificates can be signed with it after the program terminates.
        char[] caCertPem = PemEncoding.Write("CERTIFICATE", certificate.Export(X509ContentType.Cert));
        string certFilePath;
        string[] trustCommand;
        if (OSFlavor.IsFedoraLike)
        {
            certFilePath = $"{FedoraFamilyCaSourceDirectory}/{name}.pem";
            trustCommand = ["update-ca-trust", "extract"];
        }
        else if (OSFlavor.IsDebianLike)
        {
            certFilePath = $"{DebianFamilyCaSourceDirectory}/{name}.crt";
            trustCommand = ["update-ca-certificates"];
        }
        else
        {
            OSFlavor.ThrowNotSupported();
            certFilePath = "";
            trustCommand = Array.Empty<string>();
        }

        ProcessHelper.SudoExecute(new[] { "tee", certFilePath }, caCertPem);
        ProcessHelper.SudoExecute(trustCommand);

        return true;
    }

    public void AddDependencies(HashSet<Dependency> dependencies)
    {
        if (OSFlavor.IsFedoraLike)
        {
            dependencies.Add(new Dependency("update-ca-trust", "ca-certificates"));
        }
        else if (OSFlavor.IsDebianLike)
        {
            dependencies.Add(new Dependency("update-ca-certificates", "ca-certificates"));
        }
        else
        {
            OSFlavor.ThrowNotSupported();
        }
    }

    public bool IsSupported
        => OSFlavor.IsFedoraLike || OSFlavor.IsDebianLike;
}