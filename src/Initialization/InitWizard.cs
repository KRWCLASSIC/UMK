using System.Text.RegularExpressions;
using UMK.Configuration;
using UMK.Diagnostics;

namespace UMK.Initialization;

public static partial class InitWizard
{
    [GeneratedRegex(@"<TargetFrameworks?>([^<]+)</TargetFrameworks?>", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"<AssemblyName>([^<]+)</AssemblyName>", RegexOptions.IgnoreCase)]
    private static partial Regex AssemblyNameRegex();

    public static int Run(string? customManifestPath = null)
    {
        string targetPath = string.IsNullOrWhiteSpace(customManifestPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), ManifestLoader.DefaultFileName)
            : Path.GetFullPath(customManifestPath);

        ConsoleLogger.Info("UMK Interactive Project Configuration Wizard");
        Console.WriteLine();

        if (File.Exists(targetPath))
        {
            string overwrite = PromptRequired($"'{ Path.GetFileName(targetPath) }' already exists. Overwrite? (y/n)");
            if (!overwrite.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                !overwrite.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleLogger.Info("Initialization cancelled.");
                return (int)UmkExitCode.Success;
            }
            Console.WriteLine();
        }

        string[] localCsprojs = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? suggestedProjectName = localCsprojs.Length == 1
            ? Path.GetFileNameWithoutExtension(localCsprojs[0])
            : Path.GetFileName(Directory.GetCurrentDirectory());

        // 1. Project name
        ConsoleLogger.WriteLineColored("1. Project Name", ConsoleColor.Cyan);
        string projectName = PromptWithDefault("   Project name", suggestedProjectName);
        Console.WriteLine();

        // 2. Game launch method
        ConsoleLogger.WriteLineColored("2. Game Launch Method", ConsoleColor.Cyan);
        Console.WriteLine("   [1] Steam ID");
        Console.WriteLine("   [2] Executable path");
        Console.WriteLine("   [3] Custom command");
        int launchChoice = PromptMenuChoice("   Select launch method (1-3)", 1, 3, defaultOption: 1);

        string? steamId = null;
        string? exePath = null;
        string? customLaunchCommand = null;

        switch (launchChoice)
        {
            case 1:
                steamId = PromptRequired("   Enter Steam App ID");
                break;
            case 2:
                exePath = SteamGameLocator.NormalizeSlashes(PromptRequired("   Enter game executable path"));
                break;
            case 3:
                customLaunchCommand = PromptRequired("   Enter custom launch command");
                break;
        }
        Console.WriteLine();

        // 3. Game installation directory
        ConsoleLogger.WriteLineColored("3. Game Installation Directory", ConsoleColor.Cyan);
        Console.WriteLine("   [1] Automatic detection");
        Console.WriteLine("   [2] Manual path");
        int gameDirChoice = PromptMenuChoice("   Select method (1-2)", 1, 2, defaultOption: 1);

        string? gameDir = null;
        if (gameDirChoice == 1)
        {
            if (!string.IsNullOrWhiteSpace(steamId))
            {
                if (SteamGameLocator.TryLocateBySteamId(steamId, out string? locatedDir, out string errorReason))
                {
                    gameDir = locatedDir;
                    ConsoleLogger.Success($"   Detected game directory: {gameDir}");
                }
                else
                {
                    ConsoleLogger.Warn($"   Automatic detection failed: {errorReason}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(exePath))
            {
                string? parentDir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir))
                {
                    gameDir = SteamGameLocator.NormalizeSlashes(Path.GetFullPath(parentDir));
                    ConsoleLogger.Success($"   Detected game directory from executable: {gameDir}");
                }
                else
                {
                    ConsoleLogger.Warn("   Could not resolve existing directory from executable path.");
                }
            }
            else
            {
                string lookupSteamId = PromptRequired("   Enter Steam App ID for automatic detection");
                if (SteamGameLocator.TryLocateBySteamId(lookupSteamId, out string? locatedDir, out string errorReason))
                {
                    gameDir = locatedDir;
                    ConsoleLogger.Success($"   Detected game directory: {gameDir}");
                }
                else
                {
                    ConsoleLogger.Warn($"   Automatic detection failed: {errorReason}");
                }
            }
        }

        while (string.IsNullOrWhiteSpace(gameDir))
        {
            string manualInput = PromptRequired("   Enter game installation directory path");
            gameDir = SteamGameLocator.NormalizeSlashes(manualInput.Trim('"', '\''));
        }

        string? detectedExeName = !string.IsNullOrWhiteSpace(exePath)
            ? Path.GetFileName(exePath)
            : SteamGameLocator.TryDetectGameExecutable(gameDir);
        string gameExeName = PromptWithDefault("   Game process executable name (for pre-run kill)", detectedExeName);
        Console.WriteLine();

        // 4. BepInEx plugins directory
        ConsoleLogger.WriteLineColored("4. BepInEx Plugins Directory", ConsoleColor.Cyan);
        string pluginsDir = PromptWithDefault("   Plugins directory", @"${game}\BepInEx\plugins");
        pluginsDir = SteamGameLocator.NormalizeSlashes(pluginsDir);
        Console.WriteLine();

        // 5. C# project file
        ConsoleLogger.WriteLineColored("5. C# Project File (.csproj)", ConsoleColor.Cyan);
        if (localCsprojs.Length > 0)
        {
            Console.WriteLine($"   Found in workspace: {string.Join(", ", localCsprojs)}");
        }
        string? defaultCsproj = localCsprojs.Length == 1 ? localCsprojs[0] : null;
        string csprojFile = PromptWithDefault("   C# project file", defaultCsproj);
        csprojFile = SteamGameLocator.NormalizeSlashes(csprojFile);
        Console.WriteLine();

        // 6. Build configuration
        ConsoleLogger.WriteLineColored("6. Build Configuration", ConsoleColor.Cyan);
        Console.WriteLine("   [1] Release");
        Console.WriteLine("   [2] Debug");
        int configChoice = PromptMenuChoice("   Select build configuration (1-2)", 1, 2, defaultOption: 1);
        string buildConfiguration = configChoice == 1 ? "Release" : "Debug";
        Console.WriteLine();

        // 7. Build output directory
        ConsoleLogger.WriteLineColored("7. Build Output Directory", ConsoleColor.Cyan);
        string? detectedTfm = TryReadTargetFramework(csprojFile);
        string? suggestedOutput = !string.IsNullOrWhiteSpace(detectedTfm)
            ? $@"bin\${{configuration}}\{detectedTfm}"
            : null;
        string outputDir = PromptWithDefault("   Build output directory", suggestedOutput);
        outputDir = SteamGameLocator.NormalizeSlashes(outputDir);
        Console.WriteLine();

        // 8. Preferred shell
        ConsoleLogger.WriteLineColored("8. Preferred Shell", ConsoleColor.Cyan);
        Console.WriteLine("   [1] cmd");
        Console.WriteLine("   [2] powershell");
        int shellChoice = PromptMenuChoice("   Select shell (1-2)", 1, 2, defaultOption: 1);
        string preferredShell = shellChoice == 1 ? "cmd" : "powershell";
        Console.WriteLine();

        // Construct manifest
        string dllBaseName = TryReadAssemblyName(csprojFile) ?? Path.GetFileNameWithoutExtension(csprojFile);
        if (string.IsNullOrWhiteSpace(dllBaseName))
        {
            dllBaseName = projectName;
        }

        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["configuration"] = buildConfiguration
        };

        if (!string.IsNullOrWhiteSpace(steamId))
        {
            variables["steamId"] = steamId;
        }

        variables["game"] = gameDir;
        variables["gameExe"] = gameExeName;

        if (!string.IsNullOrWhiteSpace(exePath))
        {
            variables["gameExePath"] = exePath;
        }

        variables["plugins"] = pluginsDir;
        variables["output"] = outputDir;

        string killCommand = preferredShell == "cmd"
            ? "taskkill /F /IM \"${gameExe}\" >nul 2>&1 || ver >nul"
            : "Stop-Process -Name ([io.path]::GetFileNameWithoutExtension('${gameExe}')) -Force -ErrorAction SilentlyContinue";

        string copyCommand = preferredShell == "cmd"
            ? $@"copy /Y ""${{output}}\{dllBaseName}.dll"" ""${{plugins}}"""
            : $@"Copy-Item -Path ""${{output}}\{dllBaseName}.dll"" -Destination ""${{plugins}}"" -Force";

        string launchCommand = launchChoice switch
        {
            1 => "start \"\" \"steam://rungameid/${steamId}\"",
            2 => preferredShell == "cmd"
                ? "start \"\" \"${gameExePath}\""
                : "Start-Process -FilePath \"${gameExePath}\"",
            3 => customLaunchCommand!,
            _ => throw new UmkException("Invalid launch method selection.")
        };

        var scripts = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["kill"] = [killCommand],
            ["compile"] = [$"dotnet build {csprojFile} -c ${{configuration}}"],
            ["copy"] = [copyCommand],
            ["build"] = ["@compile", "@copy"],
            ["run"] = ["@kill", "@build", launchCommand],
            ["clean"] = ["dotnet clean"]
        };

        var manifest = new UmkManifest
        {
            Umk = VersionChecker.CurrentVersionString,
            Name = projectName,
            Shell = preferredShell,
            Variables = variables,
            Scripts = scripts
        };

        ManifestLoader.Save(manifest, targetPath);
        ConsoleLogger.Success($"Generated '{Path.GetFileName(targetPath)}' successfully.");
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
                throw new UmkException("Standard input ended before initialization completed.", UmkExitCode.ExecutionError);
            }

            string input = line.Trim();
            if (!string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            ConsoleLogger.Warn("   Value is required.");
        }
    }

    private static string PromptWithDefault(string label, string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return PromptRequired(label);
        }

        Console.Write($"{label} [{defaultValue}]: ");
        string? line = Console.ReadLine();
        if (line is null)
        {
            throw new UmkException("Standard input ended before initialization completed.", UmkExitCode.ExecutionError);
        }

        string input = line.Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static int PromptMenuChoice(string label, int min, int max, int defaultOption)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultOption}]: ");
            string? line = Console.ReadLine();
            if (line is null)
            {
                throw new UmkException("Standard input ended before initialization completed.", UmkExitCode.ExecutionError);
            }

            string input = line.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultOption;
            }

            if (int.TryParse(input, out int value) && value >= min && value <= max)
            {
                return value;
            }

            ConsoleLogger.Warn($"   Please enter a number between {min} and {max}.");
        }
    }

    private static string? TryReadTargetFramework(string csprojPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(csprojPath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            string content = File.ReadAllText(fullPath);
            Match match = TargetFrameworkRegex().Match(content);
            if (!match.Success)
            {
                return null;
            }

            string tfmRaw = match.Groups[1].Value.Trim();
            string firstTfm = tfmRaw.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return string.IsNullOrWhiteSpace(firstTfm) ? null : firstTfm;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadAssemblyName(string csprojPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(csprojPath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            string content = File.ReadAllText(fullPath);
            Match match = AssemblyNameRegex().Match(content);
            if (!match.Success)
            {
                return null;
            }

            string name = match.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }
}
