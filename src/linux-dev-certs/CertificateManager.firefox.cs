using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static LinuxDevCerts.ProcessHelper;

namespace LinuxDevCerts;

partial class CertificateManager
{
    private static void FindFirefoxCertificateStores(string firefoxUserDirectory, List<ICertificateStore> stores)
    {
        string profilesIniFileName = Path.Combine(firefoxUserDirectory, "profiles.ini");
        if (File.Exists(profilesIniFileName))
        {
            using FileStream profilesIniFile = File.OpenRead(profilesIniFileName);
            List<IniSection> sections = ReadIniFile(profilesIniFile);
            List<string> profileFolders = new();
            foreach (var section in sections)
            {
                string? path;
                if (section.Name.StartsWith("Install", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!section.Properties.TryGetValue("Default", out path))
                    {
                        continue;
                    }
                }
                else if (!section.Properties.TryGetValue("Path", out path))
                {
                    continue;
                }

                string profileFolder = Path.Combine(firefoxUserDirectory, path);
                if (!profileFolders.Contains(profileFolder))
                {
                    profileFolders.Add(profileFolder);
                    if (Directory.Exists(profileFolder))
                    {
                        stores.Add(new NssCertificateDatabase($"Firefox profile '{profileFolder}'", profileFolder));
                    }
                }
            }
        }
    }

    private class IniSection
    {
        public string Name { get; }

        public IniSection(string name)
        {
            Name = name;
        }

        public Dictionary<string, string> Properties { get; } = new();
    }

    private static List<IniSection> ReadIniFile(Stream stream)
    {
        // Implementation from https://raw.githubusercontent.com/dotnet/runtime/5a1b8223dab2f7954e7f206095ba937e5d237299/src/libraries/Microsoft.Extensions.Configuration.Ini/src/IniStreamConfigurationProvider.cs.
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.

        var sections = new List<IniSection>();
        IniSection? currentSection = null;

        using (var reader = new StreamReader(stream))
        {
            string sectionPrefix = string.Empty;

            while (reader.Peek() != -1)
            {
                string rawLine = reader.ReadLine()!;
                string line = rawLine.Trim();

                // Ignore blank lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                // Ignore comments
                if (line[0] == ';' || line[0] == '#' || line[0] == '/')
                {
                    continue;
                }
                // [Section:header]
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    // remove the brackets
                    string sectionName = line.Substring(1, line.Length - 2);
                    currentSection = new IniSection(sectionName);
                    sections.Add(currentSection);
                    continue;
                }

                if (currentSection == null)
                {
                    continue;
                }

                // key = value OR "value"
                int separator = line.IndexOf('=');
                if (separator < 0)
                {
                    throw new FormatException($"Unrecognized line format: '{rawLine}'.");
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                // Remove quotes
                if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (currentSection.Properties.ContainsKey(key))
                {
                    throw new FormatException($"A duplicate key '{key}' was found.");
                }

                currentSection.Properties.Add(key, value);
            }
        }

        return sections;
    }
}