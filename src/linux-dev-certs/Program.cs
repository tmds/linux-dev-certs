if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This tool is for installing ASP.NET Core developer certificates on Linux.");
    return 1;
}

if (Environment.IsPrivilegedProcess)
{
    Console.Error.WriteLine("The tool is running with elevated privileges. You should run under the user account of the developer.");
    return 1;
}

Console.WriteLine("Some operations require root. You may be prompted for your 'sudo' password.");
Console.WriteLine();

var certManager = new LinuxDevCerts.CertificateManager();
certManager.InstallAndTrust();
return 0;