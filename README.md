The .NET `dotnet dev-certs` tool doesn't support trusting the ASP.NET Core HTTPS development certificate on Linux.

This repo contains a .NET global tool creates a developer certificate on Linux.

# Using

**note** the tool updates the system CA store and calls `sudo` to have permissions to update it.

Install the tool:
```
dotnet tool update -g linux-dev-certs --prerelease --add-source https://www.myget.org/F/tmds/api/v3/index.json
```

Run it:
```
dotnet linux-dev-certs
```

# Supported distros

- Fedora and derived (RHEL, Rocky, ...)
- Debian and derived (Ubuntu, ...)

Limitations:
- Ubuntu provides browser applications as snaps. These snaps do not use the system certificate store (https://bugs.launchpad.net/ubuntu/+source/chromium-browser/+bug/1901586). For .NET itself and CLI applications the developer certificate will be trusted. For the browsers, the user still needs manually accept the developer certificate.

# How does it work

To work accross a range of tools, the tool generates a CA certificate which is added to the system store. This CA certificate is used to create an ASP.NET Core developer certificate that will be picked up by ASP.NET Core. The private key of the CA certificate is not stored and therefore it can not be used to sign additional certificates.