using System;

namespace LinuxDevCerts
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Some operations require root. You may be prompted for your 'sudo' password.");
            Console.WriteLine();

            var certManager = new CertificateManager();
            certManager.InstallAndTrust();
        }
    }
}
