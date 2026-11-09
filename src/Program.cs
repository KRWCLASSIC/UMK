using UMK.Cli;
using UMK.Configuration;
using UMK.Diagnostics;
using UMK.Execution;
using UMK.Initialization;
using UMK.Linking;
using UMK.Resolution;

namespace UMK;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            CliArguments parsed = CliParser.Parse(args);

            switch (parsed.Kind)
            {
                case CliCommandKind.Version:
                    return HelpPrinter.PrintVersion();

                case CliCommandKind.Help:
                    return HelpPrinter.PrintGeneralHelp();

                case CliCommandKind.HelpVariables:
                    return HelpPrinter.PrintVariablesHelp();

                case CliCommandKind.HelpScripts:
                    return HelpPrinter.PrintScriptsHelp();

                case CliCommandKind.Init:
                    return InitWizard.Run(parsed.ManifestPath);

                case CliCommandKind.Setup:
                    return SetupCommand.Run(parsed.ManifestPath);

                case CliCommandKind.Upgrade:
                    return UpgradeCommand.Run(parsed.ManifestPath);

                case CliCommandKind.LinkGameLibs:
                    return GameLibsLinker.Execute(parsed.ManifestPath);

                case CliCommandKind.List:
                {
                    UmkManifest manifest = ManifestLoader.Load(parsed.ManifestPath);
                    return HelpPrinter.PrintScriptList(manifest);
                }

                case CliCommandKind.RunScript:
                {
                    UmkManifest manifest = ManifestLoader.Load(parsed.ManifestPath);
                    IReadOnlyDictionary<string, string> resolvedVars = VariableResolver.ResolveAll(manifest.Variables);
                    IReadOnlyList<ResolvedCommand> resolvedCommands = ScriptResolver.Resolve(
                        parsed.ScriptName!,
                        manifest.Scripts,
                        resolvedVars);

                    return ShellExecutor.Execute(
                        parsed.ScriptName!,
                        manifest.Shell,
                        resolvedCommands,
                        parsed.DryRun);
                }

                default:
                    return HelpPrinter.PrintGeneralHelp();
            }
        }
        catch (UmkException ex)
        {
            ConsoleLogger.Error(ex.Message);
            return (int)ex.ExitCode;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Unexpected error: {ex.Message}");
            return (int)UmkExitCode.ExecutionError;
        }
    }
}

