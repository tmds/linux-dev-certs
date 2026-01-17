The .NET `dotnet dev-certs` command that is part of the .NET SDK supports generating a development certificate on Linux [to some extent](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl#linux-specific-considerations).

This repo contains a .NET tool that supports additional distributions. Because the tool uses a CA-signed development certificate, the local HTTPS endpoint is trusted by a broader part of the system (browsers, tools, ...).

# Using

**note** the tool uses `sudo` to update the system CA store and install required system tools.

```
dotnet tool update -g linux-dev-certs
dotnet linux-dev-certs install
```

You can add the `--no-deps` argument to stop the tool from installing system tools and print out the list of packages to install instead.

If you get an error saying _Could not execute because the specified command or file was not found._, try adding the .NET tools folder to PATH: `PATH=$PATH:~/.dotnet/tools`.

# Supported distros

- Fedora and derived (RHEL, AlmaLinux, ...)
- Debian and derived (Ubuntu, ...)
- Arch Linux
- Gentoo
- Slackware
- OpenSUSE Leap and SUSE Linux Enterprise Server (SLES).  Tumbleweed and SLED untested but expected to work.

Limitations:

- Ubuntu browsers are packaged as snaps. Snaps do not use system certificates. If the user uses a snap-based Firefox, the CA certificate is added to its certificate store. Other browsers are (currently) not configured.
- On gentoo, binary versions of browsers do not support system certificates. If the user uses a binary-based Firefox/LibreWolf, the CA certificate is added to its certificate store. Other browsers are (currently) not configured.

# How it works

To work across a range of distro applications, the tool generates a CA certificate which is added to the system store. This CA certificate is used to create an ASP.NET Core developer certificate that will be picked up by ASP.NET Core. The private key of the CA certificate is not stored and therefore it can not be used to sign additional certificates.
