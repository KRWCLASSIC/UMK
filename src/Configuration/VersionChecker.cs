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

        return (new Version(0, 1, 4), "0.1.4");
    });

    public static Version CurrentVersion => CurrentVersionInfo.Value.Version;
    public static string CurrentVersionString => CurrentVersionInfo.Value.VersionString;

    public static void ValidateVersionMatch(string manifestUmkVersion, bool ignoreVersionMismatch = false)
    {
        if (string.IsNullOrWhiteSpace(manifestUmkVersion))
        {
            throw new UmkException("Manifest is missing the required 'umk' format version field.");
        }

        if (ignoreVersionMismatch)
        {
            return;
        }

        string installedVersion = CurrentVersionString;
        if (string.Equals(manifestUmkVersion.Trim(), installedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Version.TryParse(manifestUmkVersion.Trim(), out Version? manifestVer) &&
            Version.TryParse(installedVersion, out Version? toolVer))
        {
            if (manifestVer < toolVer)
            {
                throw new UmkException(
                    $"UMK version mismatch: project manifest umk.json is v{manifestUmkVersion}, but installed UMK is v{installedVersion}.\n" +
                    $"Run 'umk upgrade' to update the project manifest, or bypass with '--ignore-umkv'.");
            }

            if (manifestVer > toolVer)
            {
                throw new UmkException(
                    $"UMK version mismatch: project manifest umk.json requires v{manifestUmkVersion}, but installed UMK is v{installedVersion}.\n" +
                    $"Update your UMK tool with 'dotnet tool update -g KRWCLASSIC.UMK', or bypass with '--ignore-umkv'.");
            }
        }

        throw new UmkException(
            $"UMK version mismatch: project manifest umk.json is '{manifestUmkVersion}', but installed UMK is '{installedVersion}'.\n" +
            $"Use '--ignore-umkv' to bypass this check.");
    }
}
