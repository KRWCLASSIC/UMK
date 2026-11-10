using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UMK.Configuration;
using UMK.Diagnostics;
using UMK.Resolution;

namespace UMK.Linking;

public static partial class GameLibsLinker
{
    private const string UmkItemGroupLabel = "UMKGameLibs";

    private static readonly string[] ManagedWildcardRemovals =
    [
        @"libs\managed\mscorlib.dll",
        @"libs\managed\netstandard.dll",
        @"libs\managed\WindowsBase.dll",
        @"libs\managed\Accessibility.dll",
        @"libs\managed\nunit.framework.dll",
        @"libs\managed\System*.dll",
        @"libs\managed\Mono*.dll",
        @"libs\managed\Microsoft*.dll",
        @"libs\managed\I18N*.dll"
    ];

    private static readonly HashSet<string> ExcludedManagedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib.dll",
        "netstandard.dll",
        "System.dll",
        "WindowsBase.dll",
        "Accessibility.dll",
        "nunit.framework.dll"
    };

    private static readonly string[] ExcludedManagedPrefixes =
    [
        "System.",
        "Mono.",
        "Microsoft.",
        "I18N"
    ];

    [GeneratedRegex(@"([^\s""']+\.csproj)", RegexOptions.IgnoreCase)]
    private static partial Regex CsprojInScriptRegex();

    public static int Execute(string? customManifestPath = null, bool ignoreVersionMismatch = false)
    {
        UmkManifest manifest = ManifestLoader.Load(customManifestPath, ignoreVersionMismatch);
        IReadOnlyDictionary<string, string> resolvedVars = VariableResolver.ResolveAll(manifest.Variables);

        if (!resolvedVars.TryGetValue("game", out string? gameDir) || string.IsNullOrWhiteSpace(gameDir))
        {
            throw new UmkException("Variable 'game' is not defined in umk.json.");
        }

        if (!Directory.Exists(gameDir))
        {
            throw new UmkException($"Game directory '{gameDir}' does not exist on disk.");
        }

        string projectDir = string.IsNullOrWhiteSpace(customManifestPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(customManifestPath)) ?? Directory.GetCurrentDirectory();

        string csprojPath = ResolveCsprojPath(projectDir, manifest, resolvedVars);

        var (bepInExCoreDir, managedDir) = EnsureJunctions(projectDir, gameDir);

        var coveredAssemblyNames = CollectCoveredAssemblyNames(bepInExCoreDir, managedDir);

        UpdateCsprojReferences(csprojPath, coveredAssemblyNames);

        ConsoleLogger.Success(
            $"Updated '{Path.GetFileName(csprojPath)}' with compact '{UmkItemGroupLabel}' wildcard references ({coveredAssemblyNames.Count} assemblies linked via junctions).");
        return (int)UmkExitCode.Success;
    }

    public static (string BepInExCoreDir, string ManagedDir) EnsureJunctions(string projectDir, string gameDir)
    {
        string bepInExCoreDir = Path.Combine(gameDir, "BepInEx", "core");
        if (!Directory.Exists(bepInExCoreDir))
        {
            throw new UmkException(
                $"BepInEx core directory not found at '{bepInExCoreDir}'. Ensure BepInEx is installed in the game directory.");
        }

        string managedDir = LocateManagedDirectory(gameDir);

        string libsDir = Path.Combine(projectDir, "libs");
        Directory.CreateDirectory(libsDir);

        string gitignorePath = Path.Combine(libsDir, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            try
            {
                File.WriteAllText(gitignorePath, "*" + Environment.NewLine + "!.gitignore" + Environment.NewLine);
            }
            catch
            {
                // Non-critical; ignore if write fails
            }
        }

        string bepInExJunction = Path.Combine(libsDir, "bepinex");
        string managedJunction = Path.Combine(libsDir, "managed");

        ConsoleLogger.Info($"Creating directory junctions in '{Path.GetRelativePath(projectDir, libsDir)}'...");
        CreateOrUpdateJunction(bepInExJunction, bepInExCoreDir);
        CreateOrUpdateJunction(managedJunction, managedDir);

        return (bepInExCoreDir, managedDir);
    }

    private static HashSet<string> CollectCoveredAssemblyNames(string bepInExCoreDir, string managedDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dllPath in Directory.GetFiles(bepInExCoreDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(dllPath);
            if (fileName.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(Path.GetFileNameWithoutExtension(fileName));
            }
        }

        foreach (string dllPath in Directory.GetFiles(managedDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(dllPath);
            if (ExcludedManagedExactNames.Contains(fileName))
            {
                continue;
            }

            bool excluded = false;
            foreach (string prefix in ExcludedManagedPrefixes)
            {
                if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    excluded = true;
                    break;
                }
            }

            if (!excluded)
            {
                names.Add(Path.GetFileNameWithoutExtension(fileName));
            }
        }

        return names;
    }

    private static string LocateManagedDirectory(string gameDir)
    {
        foreach (string dataDir in Directory.GetDirectories(gameDir, "*_Data", SearchOption.TopDirectoryOnly))
        {
            string candidate = Path.Combine(dataDir, "Managed");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new UmkException(
            $"Could not find a Unity '*_Data\\Managed' directory inside '{gameDir}'.");
    }

    private static string ResolveCsprojPath(
        string projectDir,
        UmkManifest manifest,
        IReadOnlyDictionary<string, string> resolvedVars)
    {
        foreach (List<string> steps in manifest.Scripts.Values)
        {
            foreach (string rawStep in steps)
            {
                string step = VariableResolver.Interpolate(rawStep, resolvedVars, "manifest script");
                Match match = CsprojInScriptRegex().Match(step);
                if (match.Success)
                {
                    string candidate = Path.GetFullPath(Path.Combine(projectDir, match.Groups[1].Value));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        string[] csprojs = Directory.GetFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojs.Length == 1)
        {
            return csprojs[0];
        }

        if (csprojs.Length == 0)
        {
            throw new UmkException($"No .csproj file found in '{projectDir}'.");
        }

        throw new UmkException(
            $"Multiple .csproj files found in '{projectDir}'. Reference the target .csproj in your umk.json 'compile' script.");
    }

    private static void CreateOrUpdateJunction(string junctionPath, string targetPath)
    {
        if (Directory.Exists(junctionPath))
        {
            var dirInfo = new DirectoryInfo(junctionPath);
            if (!dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new UmkException(
                    $"Cannot create junction at '{junctionPath}' because a normal directory already exists at that path.");
            }

            Directory.Delete(junctionPath, recursive: false);
        }
        else if (File.Exists(junctionPath))
        {
            throw new UmkException(
                $"Cannot create junction at '{junctionPath}' because a file already exists at that path.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /s /c \"mklink /J \"{junctionPath}\" \"{targetPath}\" >nul\"",
            UseShellExecute = false
        };

        using Process? process = Process.Start(psi);
        if (process is null)
        {
            throw new UmkException($"Failed to start cmd.exe to create directory junction '{junctionPath}'.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(junctionPath))
        {
            throw new UmkException(
                $"Failed to create directory junction '{junctionPath}' -> '{targetPath}' (exit code {process.ExitCode}).");
        }
    }

    private static void UpdateCsprojReferences(
        string csprojPath,
        HashSet<string> coveredAssemblyNames)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(csprojPath);
        }
        catch (Exception ex)
        {
            throw new UmkException($"Failed to parse '{csprojPath}': {ex.Message}", ex);
        }

        XElement? root = doc.Root;
        if (root is null || !root.Name.LocalName.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            throw new UmkException($"Invalid .csproj root element in '{csprojPath}'.");
        }

        XNamespace ns = root.Name.Namespace;

        // 1. Remove any previous UMKGameLibs ItemGroup
        List<XElement> existingUmkGroups = root
            .Elements(ns + "ItemGroup")
            .Where(ig => string.Equals((string?)ig.Attribute("Label"), UmkItemGroupLabel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (XElement group in existingUmkGroups)
        {
            group.Remove();
        }

        // 2. Remove duplicate <Reference> tags in other ItemGroups that match covered assemblies
        foreach (XElement itemGroup in root.Elements(ns + "ItemGroup").ToList())
        {
            foreach (XElement reference in itemGroup.Elements(ns + "Reference").ToList())
            {
                string? includeAttr = (string?)reference.Attribute("Include");
                string? includeSimpleName = includeAttr?.Split(',')[0].Trim();
                string? hintPath = (string?)reference.Element(ns + "HintPath");
                string? hintFileName = !string.IsNullOrWhiteSpace(hintPath)
                    ? Path.GetFileNameWithoutExtension(hintPath.Replace('/', '\\'))
                    : null;

                if ((!string.IsNullOrWhiteSpace(includeSimpleName) && coveredAssemblyNames.Contains(includeSimpleName)) ||
                    (!string.IsNullOrWhiteSpace(hintFileName) && coveredAssemblyNames.Contains(hintFileName)))
                {
                    reference.Remove();
                }
            }

            if (!itemGroup.HasElements && itemGroup.Attributes().All(a => a.IsNamespaceDeclaration))
            {
                itemGroup.Remove();
            }
        }

        // 3. Append compact wildcard <ItemGroup Label="UMKGameLibs">
        var umkGroup = new XElement(
            ns + "ItemGroup",
            new XAttribute("Label", UmkItemGroupLabel),
            new XElement(
                ns + "Reference",
                new XAttribute("Include", @"libs\bepinex\0Harmony.dll"),
                new XAttribute("Private", "False")),
            new XElement(
                ns + "Reference",
                new XAttribute("Include", @"libs\bepinex\BepInEx*.dll"),
                new XAttribute("Private", "False")),
            new XElement(
                ns + "Reference",
                new XAttribute("Include", @"libs\managed\*.dll"),
                new XAttribute("Private", "False")));

        foreach (string removalPattern in ManagedWildcardRemovals)
        {
            umkGroup.Add(new XElement(
                ns + "Reference",
                new XAttribute("Remove", removalPattern)));
        }

        root.Add(umkGroup);

        var writerSettings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = doc.Declaration is null,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        try
        {
            using XmlWriter writer = XmlWriter.Create(csprojPath, writerSettings);
            doc.Save(writer);
        }
        catch (Exception ex)
        {
            throw new UmkException($"Failed to save updated '{csprojPath}': {ex.Message}", ex);
        }
    }
}
