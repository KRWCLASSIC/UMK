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

        UmkManifest manifest = ManifestLoader.Load(manifestPath, ignoreVersionMismatch: true);

        string currentTarget = VersionChecker.CurrentVersionString;
        string manifestVersion = manifest.Umk;
        bool versionChanged = !string.Equals(manifestVersion.Trim(), currentTarget, StringComparison.OrdinalIgnoreCase);

        int migratedReferences = 0;
        foreach (var steps in manifest.Scripts.Values)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                string trimmed = steps[i].Trim();
                if (!trimmed.StartsWith('@') && !trimmed.StartsWith("@@") && manifest.Scripts.ContainsKey(trimmed))
                {
                    steps[i] = "@" + trimmed;
                    migratedReferences++;
                }
            }
        }

        if (manifest.PostSetup != null)
        {
            for (int i = 0; i < manifest.PostSetup.Count; i++)
            {
                string trimmed = manifest.PostSetup[i].Trim();
                if (!trimmed.StartsWith('@') && !trimmed.StartsWith("@@") && manifest.Scripts.ContainsKey(trimmed))
                {
                    manifest.PostSetup[i] = "@" + trimmed;
                    migratedReferences++;
                }
            }
        }

        if (!versionChanged && migratedReferences == 0)
        {
            ConsoleLogger.Info($"Manifest '{Path.GetFileName(manifestPath)}' is already up to date (format version {currentTarget}).");
            return (int)UmkExitCode.Success;
        }

        manifest.Umk = currentTarget;
        ManifestLoader.Save(manifest, manifestPath);

        if (versionChanged)
        {
            ConsoleLogger.Success(
                $"Upgraded '{Path.GetFileName(manifestPath)}' from v{manifestVersion} to v{currentTarget}.");
        }

        if (migratedReferences > 0)
        {
            ConsoleLogger.Info(
                $"Migrated {migratedReferences} script reference(s) to '@script' prefix syntax.");
        }

        return (int)UmkExitCode.Success;
    }
}

