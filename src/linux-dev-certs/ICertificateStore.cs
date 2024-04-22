using System.Security.Cryptography.X509Certificates;

interface ICertificateStore
{
    public string Name { get; }
    public bool TryInstallCertificate(string name, X509Certificate2 certificate);
}