using UMK.Diagnostics;

namespace UMK.Resolution;

public sealed record ResolvedCommand(string ScriptName, string Command);

public static class ScriptResolver
{
    public static IReadOnlyList<ResolvedCommand> Resolve(
        string targetScript,
        IReadOnlyDictionary<string, List<string>> scripts,
        IReadOnlyDictionary<string, string> resolvedVariables)
    {
        if (!scripts.ContainsKey(targetScript))
        {
            string available = scripts.Count > 0
                ? string.Join(", ", scripts.Keys)
                : "(none)";
            throw new UmkException(
                $"Script '{targetScript}' is not defined in manifest. Available scripts: {available}");
        }

        var commands = new List<ResolvedCommand>();
        var callStack = new List<string>();

        ExpandScript(targetScript, scripts, resolvedVariables, commands, callStack);

        return commands;
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

            // Check if the entry is a reference to another script
            if (scripts.ContainsKey(trimmedEntry))
            {
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

