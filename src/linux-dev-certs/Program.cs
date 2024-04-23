using System.CommandLine;
using System.CommandLine.Invocation;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This tool is for installing ASP.NET Core development certificates on Linux.");
    return 1;
}

var installDepsOption = new Option<bool>
(
    name: "--install-deps",
    description: "Install required system tools."
);
var installCommand = new Command("install", "Installs (or updates) the ASP.NET Core development certificate.")
{
    installDepsOption
};
installCommand.SetHandler((InvocationContext ctx) =>
{
    bool installDeps = ctx.ParseResult.GetValueForOption(installDepsOption);

    var certManager = new LinuxDevCerts.CertificateManager();
    bool isSuccess = certManager.InstallAndTrust(installDeps);
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