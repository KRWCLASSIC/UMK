using System.Diagnostics;
using UMK.Diagnostics;
using UMK.Resolution;

namespace UMK.Execution;

public static class ShellExecutor
{
    public static int Execute(
        string targetScript,
        string shell,
        IReadOnlyList<ResolvedCommand> commands,
        bool dryRun)
    {
        string normalizedShell = shell.Trim().ToLowerInvariant();
        if (normalizedShell is not ("cmd" or "powershell"))
        {
            throw new UmkException($"Unsupported shell '{shell}'. Expected 'cmd' or 'powershell'.");
        }

        if (commands.Count == 0)
        {
            ConsoleLogger.Warn($"Script '{targetScript}' resolved to 0 commands.");
            return (int)UmkExitCode.Success;
        }

        if (dryRun)
        {
            ConsoleLogger.Info($"Dry run for script '{targetScript}' (shell: {normalizedShell}, {commands.Count} command(s)):");
            foreach (ResolvedCommand cmd in commands)
            {
                ConsoleLogger.Step(cmd.ScriptName, cmd.Command, isDryRun: true);
            }
            return (int)UmkExitCode.Success;
        }

        ConsoleLogger.Info($"Running script '{targetScript}' (shell: {normalizedShell})...");

        foreach (ResolvedCommand cmd in commands)
        {
            ConsoleLogger.Step(cmd.ScriptName, cmd.Command, isDryRun: false);

            int exitCode = RunProcess(normalizedShell, cmd.Command);
            if (exitCode != 0)
            {
                ConsoleLogger.Error(
                    $"Command failed in script '{cmd.ScriptName}' with exit code {exitCode}: {cmd.Command}");
                return exitCode;
            }
        }

        ConsoleLogger.Success($"Script '{targetScript}' finished successfully.");
        return (int)UmkExitCode.Success;
    }

    private static int RunProcess(string normalizedShell, string command)
    {
        ProcessStartInfo startInfo = normalizedShell switch
        {
            "cmd" => CreateCmdStartInfo(command),
            "powershell" => CreatePowerShellStartInfo(command),
            _ => throw new UmkException($"Unsupported shell '{normalizedShell}'.")
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                throw new UmkException(
                    $"Failed to start shell process '{startInfo.FileName}'.",
                    UmkExitCode.ExecutionError);
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (UmkException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UmkException(
                $"Failed to execute command via '{normalizedShell}': {ex.Message}",
                ex,
                UmkExitCode.ExecutionError);
        }
    }

    private static ProcessStartInfo CreateCmdStartInfo(string command)
    {
        // /d disables AutoRun registry commands.
        // /s /c "\"<command>\"" strips the outer quotes and preserves all inner quotes verbatim.
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /s /c \"{command}\"",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false
        };
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false
        };

        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);

        return psi;
    }
}

