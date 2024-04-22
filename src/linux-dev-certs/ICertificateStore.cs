using System.Security.Cryptography.X509Certificates;

namespace LinuxDevCerts;

interface ICertificateStore
{
    public string Name { get; }
    public bool TryInstallCertificate(string name, X509Certificate2 certificate);
    public void AddDependencies(HashSet<Dependency> dependencies);
}