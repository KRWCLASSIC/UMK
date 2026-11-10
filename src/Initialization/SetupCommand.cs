using UMK.Configuration;
using UMK.Diagnostics;
using UMK.Execution;
using UMK.Linking;
using UMK.Resolution;

namespace UMK.Initialization;

public static class SetupCommand
{
    public static int Run(string? customManifestPath = null, bool ignoreVersionMismatch = false)
    {
        string manifestPath = string.IsNullOrWhiteSpace(customManifestPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), ManifestLoader.DefaultFileName)
            : Path.GetFullPath(customManifestPath);

        UmkManifest manifest = ManifestLoader.Load(manifestPath, ignoreVersionMismatch);
        string projectDir = Path.GetDirectoryName(manifestPath) ?? Directory.GetCurrentDirectory();

        ConsoleLogger.Info($"UMK Project Setup — {manifest.Name}");
        Console.WriteLine();

        manifest.Variables.TryGetValue("game", out string? currentGameDir);
        manifest.Variables.TryGetValue("steamId", out string? steamId);

        string targetGameDir = string.Empty;

        if (!string.IsNullOrWhiteSpace(currentGameDir) && Directory.Exists(currentGameDir))
        {
            Console.WriteLine($"Current game directory: {currentGameDir} (Found on disk)");
            string choice = PromptWithDefault("Keep this game directory? (y/n)", "y");
            if (choice.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                choice.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                targetGameDir = currentGameDir;
            }
        }
        else if (!string.IsNullOrWhiteSpace(currentGameDir))
        {
            ConsoleLogger.Warn($"Configured game directory '{currentGameDir}' does not exist on this machine.");
        }

        while (string.IsNullOrWhiteSpace(targetGameDir))
        {
            if (!string.IsNullOrWhiteSpace(steamId))
            {
                if (SteamGameLocator.TryLocateBySteamId(steamId, out string? locatedDir, out _))
                {
                    Console.WriteLine($"Detected Steam game directory: {locatedDir}");
                    string useDetected = PromptWithDefault("Use detected Steam directory? (y/n)", "y");
                    if (useDetected.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                        useDetected.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    {
                        targetGameDir = locatedDir!;
                        break;
                    }
                }
            }

            string manual = PromptRequired("Enter game installation directory path");
            string normalized = SteamGameLocator.NormalizeSlashes(manual.Trim('"', '\''));
            if (Directory.Exists(normalized))
            {
                targetGameDir = normalized;
            }
            else
            {
                ConsoleLogger.Warn($"Directory '{normalized}' does not exist on disk. Please enter a valid path.");
            }
        }

        targetGameDir = SteamGameLocator.NormalizeSlashes(targetGameDir);
        manifest.Variables["game"] = targetGameDir;

        // Verify or update gameExe
        string? detectedExe = SteamGameLocator.TryDetectGameExecutable(targetGameDir);
        if (!manifest.Variables.TryGetValue("gameExe", out string? currentExe) || string.IsNullOrWhiteSpace(currentExe))
        {
            if (!string.IsNullOrWhiteSpace(detectedExe))
            {
                manifest.Variables["gameExe"] = detectedExe;
            }
        }
        else if (!string.IsNullOrWhiteSpace(detectedExe) && !currentExe.Equals(detectedExe, StringComparison.OrdinalIgnoreCase))
        {
            manifest.Variables["gameExe"] = detectedExe;
        }

        ManifestLoader.Save(manifest, manifestPath);
        ConsoleLogger.Success($"Updated '{Path.GetFileName(manifestPath)}' with game directory: {targetGameDir}");

        // Create directory junctions without modifying .csproj
        Console.WriteLine();
        GameLibsLinker.EnsureJunctions(projectDir, targetGameDir);

        if (manifest.PostSetup is { Count: > 0 })
        {
            Console.WriteLine();
            var postSetupVars = VariableResolver.ResolveForSteps(
                manifest.PostSetup,
                manifest.Variables,
                manifest.Scripts,
                manifest.Shell,
                projectDir);

            var resolvedSteps = ScriptResolver.ResolveStepList(
                "post-setup",
                manifest.PostSetup,
                manifest.Scripts,
                postSetupVars);

            int exitCode = ShellExecutor.Execute(
                "post-setup",
                manifest.Shell,
                resolvedSteps,
                dryRun: false,
                workingDirectory: projectDir);

            if (exitCode != (int)UmkExitCode.Success)
            {
                return exitCode;
            }
        }

        ConsoleLogger.Success($"Project setup complete! Directory junctions in 'libs\\' are ready.");
        Console.WriteLine("You can now build with 'umk build'.");
        return (int)UmkExitCode.Success;
    }

    private static string PromptRequired(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            string? line = Console.ReadLine();
            if (line is null)
            {
                throw new UmkException("Standard input ended unexpectedly.", UmkExitCode.ExecutionError);
            }

            string input = line.Trim();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            ConsoleLogger.Warn("   Value is required.");
        }
    }

    private static string PromptWithDefault(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        string? line = Console.ReadLine();
        if (line is null)
        {
            throw new UmkException("Standard input ended unexpectedly.", UmkExitCode.ExecutionError);
        }

        string input = line.Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }
}

