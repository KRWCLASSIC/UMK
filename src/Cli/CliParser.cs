using UMK.Diagnostics;

namespace UMK.Cli;

public static class CliParser
{
    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments(CliCommandKind.Help);
        }

        bool dryRun = false;
        string? manifestPath = null;
        var positionals = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "--dry-run":
                case "-n":
                    dryRun = true;
                    break;

                case "--version":
                case "-v":
                    return new CliArguments(CliCommandKind.Version);

                case "--help":
                case "-h":
                    return new CliArguments(CliCommandKind.Help);

                case "--manifest":
                case "-m":
                    if (i + 1 >= args.Length)
                    {
                        throw new UmkException(
                            $"Option '{arg}' requires a path argument.",
                            UmkExitCode.UsageError);
                    }
                    manifestPath = args[++i];
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new UmkException(
                            $"Unknown option '{arg}'. Run 'umk help' for usage information.",
                            UmkExitCode.UsageError);
                    }
                    positionals.Add(arg);
                    break;
            }
        }

        if (positionals.Count == 0)
        {
            return new CliArguments(CliCommandKind.Help);
        }

        string primary = positionals[0];

        if (primary.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count == 1)
            {
                return new CliArguments(CliCommandKind.Help);
            }

            string topic = positionals[1].ToLowerInvariant();
            return topic switch
            {
                "variables" or "variable" or "vars" => new CliArguments(CliCommandKind.HelpVariables),
                "scripts" or "script" => new CliArguments(CliCommandKind.HelpScripts),
                _ => throw new UmkException(
                    $"Unknown help topic '{positionals[1]}'. Available topics: variables, scripts.",
                    UmkExitCode.UsageError)
            };
        }

        if (primary.Equals("init", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count > 1)
            {
                throw new UmkException(
                    "Unexpected arguments after 'init'. Usage: umk init",
                    UmkExitCode.UsageError);
            }
            return new CliArguments(CliCommandKind.Init, ManifestPath: manifestPath);
        }

        if (primary.Equals("setup", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count > 1)
            {
                throw new UmkException(
                    "Unexpected arguments after 'setup'. Usage: umk setup",
                    UmkExitCode.UsageError);
            }
            return new CliArguments(CliCommandKind.Setup, ManifestPath: manifestPath);
        }

        if (primary.Equals("upgrade", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count > 1)
            {
                throw new UmkException(
                    "Unexpected arguments after 'upgrade'. Usage: umk upgrade",
                    UmkExitCode.UsageError);
            }
            return new CliArguments(CliCommandKind.Upgrade, ManifestPath: manifestPath);
        }

        if (primary.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count > 1)
            {
                throw new UmkException(
                    "Unexpected arguments after 'list'. Usage: umk list",
                    UmkExitCode.UsageError);
            }
            return new CliArguments(CliCommandKind.List, ManifestPath: manifestPath);
        }

        if (primary.Equals("linkgamelibs", StringComparison.OrdinalIgnoreCase) ||
            primary.Equals("link", StringComparison.OrdinalIgnoreCase))
        {
            if (positionals.Count > 1)
            {
                throw new UmkException(
                    $"Unexpected arguments after '{primary}'. Usage: umk linkgamelibs",
                    UmkExitCode.UsageError);
            }
            return new CliArguments(CliCommandKind.LinkGameLibs, ManifestPath: manifestPath);
        }

        if (positionals.Count > 1)
        {
            throw new UmkException(
                $"Unexpected argument '{positionals[1]}'. Only one script name can be executed at a time.",
                UmkExitCode.UsageError);
        }

        return new CliArguments(
            CliCommandKind.RunScript,
            ScriptName: primary,
            DryRun: dryRun,
            ManifestPath: manifestPath);
    }
}

