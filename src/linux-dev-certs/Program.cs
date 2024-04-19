using System.CommandLine;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("This tool is for installing ASP.NET Core development certificates on Linux.");
    return 1;
}

if (Environment.IsPrivilegedProcess)
{
    Console.Error.WriteLine("The tool is running with elevated privileges. You should run under the user account of the developer.");
    return 1;
}

var installCommand = new Command("install", "Installs (or updates) the ASP.NET Core development certificate.");
installCommand.SetHandler(() =>
{
    ConsoleColor color = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Some operations require root. You may be prompted for your 'sudo' password.");
    Console.ForegroundColor = color;

    var certManager = new LinuxDevCerts.CertificateManager();
    certManager.InstallAndTrust();
    Console.WriteLine("The development certificate was successfully installed.");
});

var rootCommand = new RootCommand()
{
    installCommand
};

return await rootCommand.InvokeAsync(args);