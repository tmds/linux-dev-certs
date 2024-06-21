using System.CommandLine;
using System.CommandLine.Invocation;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This tool is for installing ASP.NET Core development certificates on Linux.");
    return 1;
}

var noDepsOption = new Option<bool>
(
    name: "--no-deps",
    description: "Don't install required system tools. Print list of packages to install instead."
);
var installCommand = new Command("install", "Installs (or updates) the ASP.NET Core development certificate.")
{
    noDepsOption
};
installCommand.SetHandler((InvocationContext ctx) =>
{
    bool installDeps = !ctx.ParseResult.GetValueForOption(noDepsOption);

    var certManager = new LinuxDevCerts.CertificateManager();
    bool isSuccess = certManager.InstallAndTrust(installDeps);
    if (isSuccess)
    {
        ConsoleColor color = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("The development certificate was successfully installed.");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("ASP.NET Core applications may still print a warning at startup that the develper certificate is not trusted.");
        Console.WriteLine("This is a false warning. The message is no longer printed with ASP.NET Core 9 preview 6+.");

        Console.ForegroundColor = color;
    }
    ctx.ExitCode = isSuccess ? 0 : 1;
});

var rootCommand = new RootCommand()
{
    installCommand
};

return await rootCommand.InvokeAsync(args);