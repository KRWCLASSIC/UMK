using UMK.Diagnostics;

namespace UMK.Resolution;

public sealed record ResolvedCommand(string ScriptName, string Command);

public static class ScriptResolver
{
    public static bool IsPrivateScript(string scriptName)
    {
        return !string.IsNullOrEmpty(scriptName) && scriptName.StartsWith('*');
    }

    public static IReadOnlyList<ResolvedCommand> Resolve(
        string targetScript,
        IReadOnlyDictionary<string, List<string>> scripts,
        IReadOnlyDictionary<string, string> resolvedVariables)
    {
        var publicScripts = scripts.Keys.Where(k => !IsPrivateScript(k)).ToList();

        if (IsPrivateScript(targetScript) || !scripts.ContainsKey(targetScript))
        {
            string available = publicScripts.Count > 0
                ? string.Join(", ", publicScripts)
                : "(none)";

            throw new UmkException(
                $"Script '{targetScript}' is not defined in manifest. Available scripts: {available}");
        }

        var commands = new List<ResolvedCommand>();
        var callStack = new List<string>();

        ExpandScript(targetScript, scripts, resolvedVariables, commands, callStack);

        return commands;
    }

    public static IReadOnlyList<ResolvedCommand> ResolveInternalScript(
        string targetScript,
        IReadOnlyDictionary<string, List<string>> scripts,
        IReadOnlyDictionary<string, string> resolvedVariables)
    {
        if (!scripts.ContainsKey(targetScript))
        {
            throw new UmkException($"Script '{targetScript}' is not defined in manifest.");
        }

        var commands = new List<ResolvedCommand>();
        var callStack = new List<string>();

        ExpandScript(targetScript, scripts, resolvedVariables, commands, callStack);

        return commands;
    }

    public static IReadOnlyList<string> CollectRawCommands(
        string targetScript,
        IReadOnlyDictionary<string, List<string>> scripts)
    {
        var commands = new List<string>();
        var callStack = new List<string>();

        CollectInternal(targetScript, scripts, commands, callStack);

        return commands;
    }

    private static void CollectInternal(
        string scriptName,
        IReadOnlyDictionary<string, List<string>> scripts,
        List<string> output,
        List<string> callStack)
    {
        int cycleIndex = callStack.IndexOf(scriptName);
        if (cycleIndex >= 0)
        {
            string cyclePath = string.Join(" -> ", callStack.Skip(cycleIndex).Append(scriptName));
            throw new UmkException($"Circular script reference detected: {cyclePath}");
        }

        if (!scripts.TryGetValue(scriptName, out List<string>? entries))
        {
            throw new UmkException($"Script '{scriptName}' is not defined in manifest.");
        }

        callStack.Add(scriptName);

        foreach (string rawEntry in entries)
        {
            if (string.IsNullOrWhiteSpace(rawEntry))
            {
                continue;
            }

            string trimmed = rawEntry.Trim();
            if (trimmed.StartsWith("@@"))
            {
                output.Add(rawEntry.Replace("@@", "@"));
            }
            else if (trimmed.StartsWith('@'))
            {
                string target = trimmed[1..].Trim();
                if (scripts.ContainsKey(target))
                {
                    CollectInternal(target, scripts, output, callStack);
                }
            }
            else if (scripts.ContainsKey(trimmed))
            {
                CollectInternal(trimmed, scripts, output, callStack);
            }
            else
            {
                output.Add(rawEntry);
            }
        }

        callStack.RemoveAt(callStack.Count - 1);
    }

    private static void ExpandScript(
        string scriptName,
        IReadOnlyDictionary<string, List<string>> scripts,
        IReadOnlyDictionary<string, string> resolvedVariables,
        List<ResolvedCommand> output,
        List<string> callStack)
    {
        int cycleIndex = callStack.IndexOf(scriptName);
        if (cycleIndex >= 0)
        {
            string cyclePath = string.Join(" -> ", callStack.Skip(cycleIndex).Append(scriptName));
            throw new UmkException($"Circular script reference detected: {cyclePath}");
        }

        if (!scripts.TryGetValue(scriptName, out List<string>? entries))
        {
            throw new UmkException($"Script '{scriptName}' is not defined in manifest.");
        }

        callStack.Add(scriptName);

        for (int i = 0; i < entries.Count; i++)
        {
            string rawEntry = entries[i];
            if (string.IsNullOrWhiteSpace(rawEntry))
            {
                throw new UmkException($"Script '{scriptName}' contains an empty command at index {i}.");
            }

            string trimmedEntry = rawEntry.Trim();

            if (trimmedEntry.StartsWith("@@"))
            {
                string literalCommand = rawEntry.Replace("@@", "@");
                string interpolated = VariableResolver.Interpolate(
                    literalCommand,
                    resolvedVariables,
                    $"script '{scriptName}' (step {i + 1})");

                output.Add(new ResolvedCommand(scriptName, interpolated));
            }
            else if (trimmedEntry.StartsWith('@'))
            {
                string targetScriptName = trimmedEntry[1..].Trim();
                if (!scripts.ContainsKey(targetScriptName))
                {
                    throw new UmkException(
                        $"Script '{scriptName}' references undefined script '@{targetScriptName}'.");
                }

                ExpandScript(targetScriptName, scripts, resolvedVariables, output, callStack);
            }
            else if (scripts.ContainsKey(trimmedEntry))
            {
                // Backward compatibility for scripts defined without '@' prefix
                ExpandScript(trimmedEntry, scripts, resolvedVariables, output, callStack);
            }
            else
            {
                string interpolated = VariableResolver.Interpolate(
                    rawEntry,
                    resolvedVariables,
                    $"script '{scriptName}' (step {i + 1})");

                output.Add(new ResolvedCommand(scriptName, interpolated));
            }
        }

        callStack.RemoveAt(callStack.Count - 1);
    }
}
