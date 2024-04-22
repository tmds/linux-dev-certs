using System.CommandLine;
using System.CommandLine.Invocation;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This tool is for installing ASP.NET Core development certificates on Linux.");
    return 1;
}

var installCommand = new Command("install", "Installs (or updates) the ASP.NET Core development certificate.");
installCommand.SetHandler((InvocationContext ctx) =>
{
    var certManager = new LinuxDevCerts.CertificateManager();
    bool isSuccess = certManager.InstallAndTrust();
    if (isSuccess)
    {
        Console.WriteLine("The development certificate was successfully installed.");
    }
    ctx.ExitCode = isSuccess ? 0 : 1;
});

var rootCommand = new RootCommand()
{
    installCommand
};

return await rootCommand.InvokeAsync(args);