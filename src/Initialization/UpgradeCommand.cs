using UMK.Configuration;
using UMK.Diagnostics;

namespace UMK.Initialization;

public static class UpgradeCommand
{
    public static int Run(string? customManifestPath = null)
    {
        string manifestPath = string.IsNullOrWhiteSpace(customManifestPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), ManifestLoader.DefaultFileName)
            : Path.GetFullPath(customManifestPath);

        UmkManifest manifest = ManifestLoader.Load(manifestPath);

        string currentTarget = VersionChecker.CurrentVersionString;
        string manifestVersion = manifest.Umk;

        if (string.Equals(manifestVersion.Trim(), currentTarget, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleLogger.Info($"Manifest '{Path.GetFileName(manifestPath)}' is already up to date (format version {currentTarget}).");
            return (int)UmkExitCode.Success;
        }

        manifest.Umk = currentTarget;
        ManifestLoader.Save(manifest, manifestPath);

        ConsoleLogger.Success(
            $"Upgraded '{Path.GetFileName(manifestPath)}' from v{manifestVersion} to v{currentTarget}.");
        return (int)UmkExitCode.Success;
    }
}

