using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace UMK.Initialization;

public static partial class SteamGameLocator
{
    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AcfInstallDirRegex();

    public static bool TryLocateBySteamId(string steamId, out string? installDir, out string errorReason)
    {
        installDir = null;

        if (!OperatingSystem.IsWindows())
        {
            errorReason = "Automatic Steam detection via Windows Registry is only supported on Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(steamId))
        {
            errorReason = "Steam ID is empty.";
            return false;
        }

        string? steamRoot = GetSteamInstallPathWindows();
        if (string.IsNullOrWhiteSpace(steamRoot) || !Directory.Exists(steamRoot))
        {
            errorReason = "Could not locate Steam installation path in the Windows Registry.";
            return false;
        }

        var libraryPaths = new List<string> { Path.GetFullPath(steamRoot) };
        string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");

        if (File.Exists(vdfPath))
        {
            try
            {
                string vdfContent = File.ReadAllText(vdfPath);
                foreach (Match match in VdfPathRegex().Matches(vdfContent))
                {
                    string rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(rawPath))
                    {
                        string fullLibPath = Path.GetFullPath(rawPath);
                        if (!libraryPaths.Contains(fullLibPath, StringComparer.OrdinalIgnoreCase))
                        {
                            libraryPaths.Add(fullLibPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorReason = $"Failed to read Steam libraryfolders.vdf: {ex.Message}";
                return false;
            }
        }

        string manifestFileName = $"appmanifest_{steamId.Trim()}.acf";

        foreach (string libPath in libraryPaths)
        {
            string manifestPath = Path.Combine(libPath, "steamapps", manifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                string acfContent = File.ReadAllText(manifestPath);
                Match match = AcfInstallDirRegex().Match(acfContent);
                if (!match.Success)
                {
                    errorReason = $"Found '{manifestPath}', but it did not contain an 'installdir' entry.";
                    return false;
                }

                string folderName = match.Groups[1].Value.Replace(@"\\", @"\");
                string candidateDir = Path.Combine(libPath, "steamapps", "common", folderName);
                if (Directory.Exists(candidateDir))
                {
                    installDir = NormalizeSlashes(Path.GetFullPath(candidateDir));
                    errorReason = string.Empty;
                    return true;
                }

                errorReason = $"Manifest '{manifestPath}' points to '{candidateDir}', which does not exist on disk.";
                return false;
            }
            catch (Exception ex)
            {
                errorReason = $"Failed to read '{manifestPath}': {ex.Message}";
                return false;
            }
        }

        errorReason = $"Could not find '{manifestFileName}' in any of the {libraryPaths.Count} Steam library folder(s).";
        return false;
    }

    public static string NormalizeSlashes(string path)
    {
        return path.Replace('/', '\\').TrimEnd('\\');
    }

    public static string? TryDetectGameExecutable(string gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
        {
            return null;
        }

        try
        {
            // Unity games have a matching <ExecutableName>_Data directory alongside <ExecutableName>.exe
            foreach (string subDir in Directory.GetDirectories(gameDir, "*_Data", SearchOption.TopDirectoryOnly))
            {
                string dirName = Path.GetFileName(subDir);
                if (dirName.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                {
                    string exeName = dirName[..^5] + ".exe";
                    if (File.Exists(Path.Combine(gameDir, exeName)))
                    {
                        return exeName;
                    }
                }
            }

            string folderExe = Path.GetFileName(gameDir.TrimEnd('\\', '/')) + ".exe";
            if (File.Exists(Path.Combine(gameDir, folderExe)))
            {
                return folderExe;
            }
        }
        catch
        {
            // Ignore directory enumeration errors and return null
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? GetSteamInstallPathWindows()
    {
        try
        {
            using RegistryKey? hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (hkcu?.GetValue("SteamPath") is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
            {
                return steamPath;
            }

            using RegistryKey? hklm = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (hklm?.GetValue("InstallPath") is string installPath && !string.IsNullOrWhiteSpace(installPath))
            {
                return installPath;
            }
        }
        catch
        {
            // Handled by caller checking null return
        }

        return null;
    }
}

