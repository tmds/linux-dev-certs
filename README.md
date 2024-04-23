The .NET `dotnet dev-certs` tool doesn't support trusting the ASP.NET Core HTTPS development certificate on Linux.

This repo contains a .NET global tool that creates and installs a developer certificate on Linux.

# Using

**note** the tool updates the system CA store and calls `sudo` to obtain permissions to update it. See _How it works_ below.

Install the tool:
```
dotnet tool update -g linux-dev-certs --prerelease --add-source https://www.myget.org/F/tmds/api/v3/index.json
```

Run it:
```
dotnet linux-dev-certs install --install-deps
```

By adding the `--install-deps` argument, required system tools will be installed. When it is not specified, the tool prints a list of packages to install.

# Supported distros

- Fedora and derived (RHEL, AlmaLinux, ...)
- Debian and derived (Ubuntu, ...)

Limitations:
- Ubuntu browsers are packaged as snaps. Snaps do not use system certificates. If the user uses a snap-based Firefox, the CA certificate is added to its certificate store. Other browsers are (currently) not configured.

# How it works

To work accross a range of distro applications, the tool generates a CA certificate which is added to the system store. This CA certificate is used to create an ASP.NET Core developer certificate that will be picked up by ASP.NET Core. The private key of the CA certificate is not stored and therefore it can not be used to sign additional certificates.
