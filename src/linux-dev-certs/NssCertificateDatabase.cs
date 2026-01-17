using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace LinuxDevCerts;

internal class NssCertificateDatabase : ICertificateStore
{
    private string DatabasePath { get; }

    public string Name { get; }

    public NssCertificateDatabase(string name, string path)
    {
        Name = name;
        DatabasePath = path;
    }

    public bool TryInstallCertificate(string name, X509Certificate2 certificate)
    {
        string pemFile = Path.GetTempFileName();
        try
        {
            CertificateManager.ExportCertificate(certificate, pemFile, includePrivateKey: false, password: null, CertificateKeyExportFormat.Pem);
            Process process = Process.Start(new ProcessStartInfo() {
                FileName = "certutil",
                ArgumentList = { "-d", DatabasePath, "-A", "-t", "C,,", "-n", name, "-i", pemFile },
                RedirectStandardOutput = true,
                RedirectStandardError = true })!;
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            bool success = process.ExitCode == 0;
            return success;
        }
        finally
        {
            try
            {
                File.Delete(pemFile);
            }
            catch
            { }
        }
    }

    public void AddDependencies(HashSet<Dependency> dependencies)
    {
        dependencies.Add(new Dependency("certutil", GetPackageForCertUtils()));
    }

    private string GetPackageForCertUtils()
    {
        if (OSFlavor.IsFedoraLike)
        {
            return "nss-tools";
        }
        else if (OSFlavor.IsDebianLike)
        {
            return "libnss3-tools";
        }
        else if (OSFlavor.IsGentooLike)
        {
            return "dev-libs/nss";
        }
        else if(OSFlavor.IsArchLike)
        {
            return "nss";
        }
        else if(OSFlavor.IsSlackLike)
        {
            return "mozilla-nss";
        }
        else if(OSFlavor.IsSUSELike)
        {
            return "mozilla-nss-tools";
        }
        else
        {
            OSFlavor.ThrowNotSupported();
            return "";
        }
    }
}