namespace UMK.Cli;

public enum CliCommandKind
{
    Help,
    HelpVariables,
    HelpScripts,
    Version,
    Init,
    Setup,
    List,
    LinkGameLibs,
    RunScript
}

public sealed record CliArguments(
    CliCommandKind Kind,
    string? ScriptName = null,
    bool DryRun = false,
    string? ManifestPath = null);
