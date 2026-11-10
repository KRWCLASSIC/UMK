using UMK.Configuration;
using UMK.Diagnostics;
using UMK.Resolution;

namespace UMK.Cli;

public static class HelpPrinter
{
    public static int PrintVersion()
    {
        Console.WriteLine($"UMK (Unity Modding Kit) v{VersionChecker.CurrentVersionString}");
        return (int)UmkExitCode.Success;
    }

    public static int PrintGeneralHelp()
    {
        ConsoleLogger.WriteLineColored($"UMK — Unity Modding Kit v{VersionChecker.CurrentVersionString}", ConsoleColor.Cyan);
        Console.WriteLine("A CLI-only, shell-driven project configurator and task runner for Unity mod development.");
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("USAGE:", ConsoleColor.Yellow);
        Console.WriteLine("  umk init                  Run interactive configuration wizard to generate umk.json");
        Console.WriteLine("  umk setup                 Reconfigure local game path & create libs\\ junctions on clone");
        Console.WriteLine("  umk upgrade               Upgrade umk.json format version to match installed UMK tool");
        Console.WriteLine("  umk linkgamelibs          Link BepInEx, Unity, and game DLLs into .csproj via libs\\ junctions");
        Console.WriteLine("  umk <script>              Execute a named script from umk.json (e.g. build, run, clean)");
        Console.WriteLine("  umk <script> --dry-run    Display resolved shell commands without executing them");
        Console.WriteLine("  umk list                  Display available scripts and variables in umk.json");
        Console.WriteLine("  umk help                  Display general help documentation");
        Console.WriteLine("  umk help variables        Explain variable syntax and resolution");
        Console.WriteLine("  umk help scripts          Explain script definitions and execution");
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("OPTIONS:", ConsoleColor.Yellow);
        Console.WriteLine("  -n, --dry-run             Preview resolved commands without running them");
        Console.WriteLine("  --ignore-umkv             Bypass UMK version mismatch check with umk.json");
        Console.WriteLine("  -m, --manifest <path>     Use a specific manifest file instead of ./umk.json");
        Console.WriteLine("  -v, --version             Display UMK version");
        Console.WriteLine("  -h, --help                Display this help message");
        return (int)UmkExitCode.Success;
    }

    public static int PrintVariablesHelp()
    {
        ConsoleLogger.WriteLineColored("UMK Help — Variables", ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine("Variables are defined in the 'variables' section of umk.json and referenced using ${variable} syntax.");
        Console.WriteLine("Variables can reference other variables and are resolved before script commands are executed.");
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("EXAMPLE:", ConsoleColor.Yellow);
        Console.WriteLine("""
          {
            "variables": {
              "configuration": "Release",
              "game": "C:\\Games\\MyGame",
              "gameExe": "MyGame.exe",
              "plugins": "${game}\\BepInEx\\plugins",
              "output": "bin\\${configuration}\\netstandard2.1",
              "gitHash": "$[git rev-parse --short HEAD]",
              "modVersion": "${file:version.txt}"
            }
          }
        """);
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("RESOLUTION RULES:", ConsoleColor.Yellow);
        Console.WriteLine("  - All ${name} placeholders in variables and script commands are expanded recursively.");
        Console.WriteLine("  - Dynamic Commands: Values wrapped in '$[command]' are executed at runtime; stdout becomes the variable value.");
        Console.WriteLine("  - File Tokens: '${file:path}' reads the text content of the target file directly.");
        Console.WriteLine("  - On Windows, forward slashes (/) in path variables are automatically normalized to backslashes (\\).");
        Console.WriteLine("  - Referencing an undefined variable results in an immediate error before any command executes.");
        Console.WriteLine("  - Circular references between variables (e.g. A -> ${B} -> ${A}) are detected and rejected.");
        return (int)UmkExitCode.Success;
    }

    public static int PrintScriptsHelp()
    {
        ConsoleLogger.WriteLineColored("UMK Help — Scripts", ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine("Scripts are defined in the 'scripts' section of umk.json as sequential arrays of strings.");
        Console.WriteLine("UMK has no built-in build operations: 'kill', 'compile', 'copy', 'build', and 'run' are all user-defined scripts.");
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("EXAMPLE:", ConsoleColor.Yellow);
        Console.WriteLine("""
          {
            "scripts": {
              "kill": [
                "taskkill /F /IM \"${gameExe}\" >nul 2>&1 || ver >nul"
              ],
              "compile": [
                "dotnet build MyMod.csproj -c ${configuration}"
              ],
              "copy": [
                "copy /Y \"${output}\\MyMod.dll\" \"${plugins}\""
              ],
              "build": [
                "@compile",
                "@copy"
              ],
              "run": [
                "@kill",
                "@build",
                "start \"\" \"steam://rungameid/${steamId}\""
              ],
              "*uninstall": [
                "del /F /Q \"${plugins}\\MyMod.dll\""
              ]
            }
          }
        """);
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("EXECUTION RULES:", ConsoleColor.Yellow);
        Console.WriteLine("  - Script References: Steps starting with '@' (e.g. '@build', '@*uninstall') invoke another script inline.");
        Console.WriteLine("  - Raw Commands: Lines without '@' are executed directly in the configured shell ('cmd' or 'powershell').");
        Console.WriteLine("  - Escaping: Use '@@' if a literal shell command begins with '@'.");
        Console.WriteLine("  - Execution is strictly sequential and fail-fast (stops immediately on the first non-zero exit code).");
        Console.WriteLine("  - Circular script references (e.g. @A -> @B -> @A) are detected and rejected before execution.");
        Console.WriteLine();
        ConsoleLogger.WriteLineColored("PRIVATE SCRIPTS:", ConsoleColor.Yellow);
        Console.WriteLine("  - Scripts prefixed with '*' (e.g. '*uninstall') are private.");
        Console.WriteLine("  - Private scripts can be called by other scripts (via '@*name'), but cannot be executed directly from the CLI.");
        return (int)UmkExitCode.Success;
    }

    public static int PrintScriptList(UmkManifest manifest, string? workingDirectory = null)
    {
        IReadOnlyDictionary<string, string> resolvedVars = VariableResolver.ResolveAll(manifest.Variables, manifest.Scripts, manifest.Shell, workingDirectory);

        ConsoleLogger.WriteLineColored($"{manifest.Name} (umk {manifest.Umk}, shell: {manifest.Shell})", ConsoleColor.Cyan);
        Console.WriteLine();

        if (resolvedVars.Count > 0)
        {
            ConsoleLogger.WriteLineColored("Variables:", ConsoleColor.Yellow);
            foreach (var (key, val) in resolvedVars)
            {
                ConsoleLogger.WriteColored($"  {key}", ConsoleColor.Green);
                Console.WriteLine($" = {val}");
            }
            Console.WriteLine();
        }

        var publicScripts = manifest.Scripts.Where(s => !ScriptResolver.IsPrivateScript(s.Key)).ToList();

        ConsoleLogger.WriteLineColored("Available Scripts:", ConsoleColor.Yellow);
        if (publicScripts.Count == 0)
        {
            Console.WriteLine("  (no scripts defined)");
        }
        else
        {
            foreach (var (scriptName, steps) in publicScripts)
            {
                PrintScriptEntry(scriptName, steps, manifest.Scripts);
            }
        }

        return (int)UmkExitCode.Success;
    }

    private static void PrintScriptEntry(string scriptName, List<string> steps, Dictionary<string, List<string>> allScripts)
    {
        ConsoleLogger.WriteColored($"  {scriptName}", ConsoleColor.Green);
        Console.WriteLine();
        foreach (string step in steps)
        {
            string trimmed = step.Trim();
            bool isExplicitScript = trimmed.StartsWith('@') && !trimmed.StartsWith("@@");
            bool isLegacyScript = allScripts.ContainsKey(trimmed);

            if (isExplicitScript || isLegacyScript)
            {
                string target = isExplicitScript ? trimmed[1..].Trim() : trimmed;
                ConsoleLogger.WriteColored("    -> [script] ", ConsoleColor.DarkCyan);
                Console.WriteLine($"@{target}");
            }
            else
            {
                string displayCmd = trimmed.StartsWith("@@") ? step.Replace("@@", "@") : step;
                ConsoleLogger.WriteColored("    $  ", ConsoleColor.DarkGray);
                Console.WriteLine(displayCmd);
            }
        }
    }
}
