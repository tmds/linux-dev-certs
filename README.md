The .NET `dotnet dev-certs` tool doesn't support trusting the ASP.NET Core HTTPS development certificate on Linux.

This repo contains a .NET application that will create a trusted certificate on Fedora.

```
git clone https://github.com/tmds/linux-dev-certs
cd linux-dev-certs/src/linux-dev-certs
dotnet run
```