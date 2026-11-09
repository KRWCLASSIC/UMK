using System.Reflection;
using UMK.Diagnostics;

namespace UMK.Configuration;

public static class VersionChecker
{
    private static readonly Lazy<(Version Version, string VersionString)> CurrentVersionInfo = new(() =>
    {
        Assembly assembly = typeof(VersionChecker).Assembly;

        string? infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            string cleanVersion = infoVersion.Split('+')[0].Trim();
            if (Version.TryParse(cleanVersion, out Version? parsed))
            {
                return (parsed, cleanVersion);
            }
        }

        Version? asmVersion = assembly.GetName().Version;
        if (asmVersion is not null)
        {
            string versionStr = $"{asmVersion.Major}.{asmVersion.Minor}.{Math.Max(0, asmVersion.Build)}";
            return (new Version(asmVersion.Major, asmVersion.Minor, Math.Max(0, asmVersion.Build)), versionStr);
        }

        return (new Version(0, 1, 0), "0.1.0");
    });

    public static Version CurrentVersion => CurrentVersionInfo.Value.Version;
    public static string CurrentVersionString => CurrentVersionInfo.Value.VersionString;

    public static void ValidateCompatibility(string manifestUmkVersion)
    {
        if (string.IsNullOrWhiteSpace(manifestUmkVersion))
        {
            throw new UmkException("Manifest is missing the required 'umk' format version field.");
        }

        if (!Version.TryParse(manifestUmkVersion.Trim(), out Version? manifestVersion))
        {
            throw new UmkException($"Invalid 'umk' version format '{manifestUmkVersion}'. Expected 'major.minor.patch' (e.g. '{CurrentVersionString}').");
        }

        if (manifestVersion.Major != CurrentVersion.Major)
        {
            throw new UmkException(
                $"Incompatible manifest version '{manifestUmkVersion}'. Installed UMK version is '{CurrentVersionString}' (major version mismatch).");
        }

        if (manifestVersion > CurrentVersion)
        {
            throw new UmkException(
                $"Manifest requires UMK version '{manifestUmkVersion}', but the installed UMK version is '{CurrentVersionString}'. Please update KRWCLASSIC.UMK.");
        }
    }
}
