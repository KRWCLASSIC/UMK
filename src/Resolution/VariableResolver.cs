using System.Text;
using System.Text.RegularExpressions;
using UMK.Diagnostics;
using UMK.Execution;

namespace UMK.Resolution;

public static partial class VariableResolver
{
    [GeneratedRegex(@"\$\{([^{}]+)\}")]
    private static partial Regex VariablePattern();

    public static IReadOnlyDictionary<string, string> ResolveForScript(
        string targetScript,
        IReadOnlyDictionary<string, string> rawVariables,
        IReadOnlyDictionary<string, List<string>> scripts,
        string shell = "cmd",
        string? workingDirectory = null)
    {
        var rawCommands = ScriptResolver.CollectRawCommands(targetScript, scripts);
        var requiredVariables = new HashSet<string>(StringComparer.Ordinal);

        foreach (string cmd in rawCommands)
        {
            CollectVariableReferences(cmd, rawVariables, requiredVariables);
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var callStack = new List<string>();

        foreach (string key in requiredVariables)
        {
            ResolveSingle(key, rawVariables, scripts, resolved, callStack, shell, workingDirectory);
        }

        return resolved;
    }

    public static IReadOnlyDictionary<string, string> ResolveAll(
        IReadOnlyDictionary<string, string> rawVariables,
        IReadOnlyDictionary<string, List<string>>? scripts = null,
        string shell = "cmd",
        string? workingDirectory = null)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var callStack = new List<string>();

        foreach (string key in rawVariables.Keys)
        {
            ResolveSingle(key, rawVariables, scripts, resolved, callStack, shell, workingDirectory);
        }

        return resolved;
    }

    public static string Interpolate(
        string text,
        IReadOnlyDictionary<string, string> resolvedVariables,
        string contextDescription,
        string? workingDirectory = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return VariablePattern().Replace(text, match =>
        {
            string token = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(token))
            {
                throw new UmkException($"Empty variable placeholder '${{}}' found in {contextDescription}.");
            }

            if (token.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                string rawFilePath = token[5..].Trim().Trim('"', '\'');
                return ReadFileContent(rawFilePath, token, contextDescription, workingDirectory);
            }

            if (!resolvedVariables.TryGetValue(token, out string? value))
            {
                throw new UmkException(
                    $"Undefined variable '${{{token}}}' referenced in {contextDescription}.");
            }

            return value;
        });
    }

    private static void CollectVariableReferences(
        string template,
        IReadOnlyDictionary<string, string> rawVariables,
        HashSet<string> collected)
    {
        foreach (Match match in VariablePattern().Matches(template))
        {
            string token = match.Groups[1].Value.Trim();
            if (token.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (collected.Add(token) && rawVariables.TryGetValue(token, out string? depTemplate))
            {
                CollectVariableReferences(depTemplate, rawVariables, collected);
            }
        }
    }

    private static string ResolveSingle(
        string key,
        IReadOnlyDictionary<string, string> rawVariables,
        IReadOnlyDictionary<string, List<string>>? scripts,
        Dictionary<string, string> resolved,
        List<string> callStack,
        string shell,
        string? workingDirectory)
    {
        if (resolved.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        int cycleIndex = callStack.IndexOf(key);
        if (cycleIndex >= 0)
        {
            string cyclePath = string.Join(" -> ", callStack.Skip(cycleIndex).Append(key));
            throw new UmkException($"Circular variable reference detected: {cyclePath}");
        }

        if (!rawVariables.TryGetValue(key, out string? rawValue))
        {
            string callerContext = callStack.Count > 0
                ? $"variable '{callStack[^1]}'"
                : "manifest";
            throw new UmkException($"Undefined variable '${{{key}}}' referenced in {callerContext}.");
        }

        callStack.Add(key);

        bool containsFileToken = false;

        string expanded = VariablePattern().Replace(rawValue, match =>
        {
            string token = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(token))
            {
                throw new UmkException($"Empty variable placeholder '${{}}' in variable '{key}'.");
            }

            if (token.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                containsFileToken = true;
                string rawFilePath = token[5..].Trim().Trim('"', '\'');
                return ReadFileContent(rawFilePath, token, $"variable '{key}'", workingDirectory);
            }

            return ResolveSingle(token, rawVariables, scripts, resolved, callStack, shell, workingDirectory);
        });

        bool isDynamicOutput = false;

        if (expanded.StartsWith("$[", StringComparison.Ordinal) && expanded.EndsWith(']'))
        {
            string expression = expanded[2..^1].Trim();
            expanded = EvaluateDynamicExpression(expression, key, scripts, resolved, shell, workingDirectory);
            isDynamicOutput = true;
        }

        callStack.RemoveAt(callStack.Count - 1);

        if (OperatingSystem.IsWindows() && !isDynamicOutput && !containsFileToken && !expanded.Contains("://", StringComparison.Ordinal))
        {
            expanded = expanded.Replace('/', '\\');
        }

        resolved[key] = expanded;
        return expanded;
    }

    private static string EvaluateDynamicExpression(
        string expression,
        string variableKey,
        IReadOnlyDictionary<string, List<string>>? scripts,
        Dictionary<string, string> resolved,
        string shell,
        string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new UmkException($"Empty dynamic expression in '$[]' found in variable '{variableKey}'.");
        }

        // 1. Exit code evaluation: $[exitcode:...]
        if (expression.StartsWith("exitcode:", StringComparison.OrdinalIgnoreCase))
        {
            string subExpr = expression[9..].Trim();
            return EvaluateExitCodeExpression(subExpr, variableKey, scripts, resolved, shell, workingDirectory);
        }

        // 2. File content evaluation: $[file:path]
        if (expression.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            string filePath = expression[5..].Trim().Trim('"', '\'');
            return ReadFileContent(filePath, expression, $"variable '{variableKey}'", workingDirectory);
        }

        // 3. Script execution: $[script:scriptName]
        if (expression.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
        {
            string scriptName = expression[7..].Trim();
            return ExecuteScriptCaptureOutput(scriptName, variableKey, scripts, resolved, shell, workingDirectory);
        }

        // 4. Command execution: $[command:cmd] or shorthand $[cmd]
        string commandToRun = expression.StartsWith("command:", StringComparison.OrdinalIgnoreCase)
            ? expression[8..].Trim()
            : expression;

        return ShellExecutor.CaptureOutput(shell, commandToRun, workingDirectory);
    }

    private static string EvaluateExitCodeExpression(
        string subExpr,
        string variableKey,
        IReadOnlyDictionary<string, List<string>>? scripts,
        Dictionary<string, string> resolved,
        string shell,
        string? workingDirectory)
    {
        if (subExpr.StartsWith("script:", StringComparison.OrdinalIgnoreCase))
        {
            string scriptName = subExpr[7..].Trim();
            return ExecuteScriptCaptureExitCode(scriptName, variableKey, scripts, resolved, shell, workingDirectory).ToString();
        }

        if (subExpr.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
        {
            string command = subExpr[8..].Trim();
            return ShellExecutor.CaptureExitCode(shell, command, workingDirectory).ToString();
        }

        // If target matches a script, evaluate as script exit code; otherwise as command exit code
        if (scripts != null && scripts.ContainsKey(subExpr))
        {
            return ExecuteScriptCaptureExitCode(subExpr, variableKey, scripts, resolved, shell, workingDirectory).ToString();
        }

        return ShellExecutor.CaptureExitCode(shell, subExpr, workingDirectory).ToString();
    }

    private static string ExecuteScriptCaptureOutput(
        string scriptName,
        string variableKey,
        IReadOnlyDictionary<string, List<string>>? scripts,
        Dictionary<string, string> resolved,
        string shell,
        string? workingDirectory)
    {
        if (scripts == null || !scripts.ContainsKey(scriptName))
        {
            throw new UmkException(
                $"Script '{scriptName}' referenced in variable '{variableKey}' does not exist in manifest.");
        }

        var commands = ScriptResolver.ResolveInternalScript(scriptName, scripts, resolved);
        var sb = new StringBuilder();

        foreach (var cmd in commands)
        {
            string output = ShellExecutor.CaptureOutput(shell, cmd.Command, workingDirectory);
            if (!string.IsNullOrWhiteSpace(output))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(output);
            }
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static int ExecuteScriptCaptureExitCode(
        string scriptName,
        string variableKey,
        IReadOnlyDictionary<string, List<string>>? scripts,
        Dictionary<string, string> resolved,
        string shell,
        string? workingDirectory)
    {
        if (scripts == null || !scripts.ContainsKey(scriptName))
        {
            throw new UmkException(
                $"Script '{scriptName}' referenced in variable '{variableKey}' does not exist in manifest.");
        }

        var commands = ScriptResolver.ResolveInternalScript(scriptName, scripts, resolved);
        foreach (var cmd in commands)
        {
            int code = ShellExecutor.CaptureExitCode(shell, cmd.Command, workingDirectory);
            if (code != 0)
            {
                return code;
            }
        }

        return 0;
    }

    private static string ReadFileContent(
        string rawFilePath,
        string token,
        string contextDescription,
        string? workingDirectory)
    {
        if (string.IsNullOrEmpty(rawFilePath))
        {
            throw new UmkException($"Empty file path in '${{{token}}}' found in {contextDescription}.");
        }

        string fullPath = Path.IsPathRooted(rawFilePath)
            ? rawFilePath
            : Path.GetFullPath(Path.Combine(workingDirectory ?? Directory.GetCurrentDirectory(), rawFilePath));

        if (!File.Exists(fullPath))
        {
            throw new UmkException(
                $"File '{rawFilePath}' referenced via '${{{token}}}' in {contextDescription} does not exist.");
        }

        try
        {
            return File.ReadAllText(fullPath).TrimEnd('\r', '\n');
        }
        catch (Exception ex)
        {
            throw new UmkException(
                $"Failed to read file '{rawFilePath}' in {contextDescription}: {ex.Message}", ex);
        }
    }
}
