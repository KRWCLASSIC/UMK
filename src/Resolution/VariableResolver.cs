using System.Text.RegularExpressions;
using UMK.Diagnostics;

namespace UMK.Resolution;

public static partial class VariableResolver
{
    [GeneratedRegex(@"\$\{([^{}]+)\}")]
    private static partial Regex VariablePattern();

    public static IReadOnlyDictionary<string, string> ResolveAll(IReadOnlyDictionary<string, string> rawVariables)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var callStack = new List<string>();

        foreach (string key in rawVariables.Keys)
        {
            ResolveSingle(key, rawVariables, resolved, callStack);
        }

        return resolved;
    }

    public static string Interpolate(
        string text,
        IReadOnlyDictionary<string, string> resolvedVariables,
        string contextDescription)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return VariablePattern().Replace(text, match =>
        {
            string varName = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(varName))
            {
                throw new UmkException($"Empty variable placeholder '${{}}' found in {contextDescription}.");
            }

            if (!resolvedVariables.TryGetValue(varName, out string? value))
            {
                throw new UmkException(
                    $"Undefined variable '${{{varName}}}' referenced in {contextDescription}.");
            }

            return value;
        });
    }

    private static string ResolveSingle(
        string key,
        IReadOnlyDictionary<string, string> rawVariables,
        Dictionary<string, string> resolved,
        List<string> callStack)
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

        string expanded = VariablePattern().Replace(rawValue, match =>
        {
            string referencedKey = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(referencedKey))
            {
                throw new UmkException($"Empty variable placeholder '${{}}' in variable '{key}'.");
            }

            return ResolveSingle(referencedKey, rawVariables, resolved, callStack);
        });

        callStack.RemoveAt(callStack.Count - 1);

        if (OperatingSystem.IsWindows() && !expanded.Contains("://", StringComparison.Ordinal))
        {
            expanded = expanded.Replace('/', '\\');
        }

        resolved[key] = expanded;
        return expanded;
    }
}

