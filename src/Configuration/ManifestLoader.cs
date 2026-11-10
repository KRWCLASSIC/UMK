using System.Text.Encodings.Web;
using System.Text.Json;
using UMK.Diagnostics;

namespace UMK.Configuration;

public static class ManifestLoader
{
    public const string DefaultFileName = "umk.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = UmkJsonContext.Default
    };

    public static UmkManifest Load(string? customPath = null, bool ignoreVersionMismatch = false)
    {
        string path = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName)
            : Path.GetFullPath(customPath);

        if (!File.Exists(path))
        {
            throw new UmkException(
                $"Manifest file not found at '{path}'. Run 'umk init' to create a new manifest.");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            throw new UmkException($"Failed to read manifest file '{path}': {ex.Message}", ex);
        }

        UmkManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, UmkJsonContext.Default.UmkManifest);
        }
        catch (JsonException ex)
        {
            throw new UmkException($"Invalid JSON in '{path}': {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new UmkException($"Manifest file '{path}' deserialized to null.");
        }

        Validate(manifest, ignoreVersionMismatch);
        return manifest;
    }

    public static void Save(UmkManifest manifest, string? customPath = null)
    {
        Validate(manifest, ignoreVersionMismatch: true);

        string path = string.IsNullOrWhiteSpace(customPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName)
            : Path.GetFullPath(customPath);

        try
        {
            string json = JsonSerializer.Serialize(manifest, typeof(UmkManifest), SerializerOptions);
            File.WriteAllText(path, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            throw new UmkException($"Failed to write manifest file '{path}': {ex.Message}", ex);
        }
    }

    public static void Validate(UmkManifest manifest, bool ignoreVersionMismatch = false)
    {
        VersionChecker.ValidateVersionMatch(manifest.Umk, ignoreVersionMismatch);

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new UmkException("Manifest is missing the required 'name' field.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Shell))
        {
            throw new UmkException("Manifest is missing the required 'shell' field. Expected 'cmd' or 'powershell'.");
        }

        string normalizedShell = manifest.Shell.Trim().ToLowerInvariant();
        if (normalizedShell is not ("cmd" or "powershell"))
        {
            throw new UmkException(
                $"Unsupported shell '{manifest.Shell}' in manifest. Supported values are 'cmd' and 'powershell'.");
        }

        if (manifest.Variables is null)
        {
            throw new UmkException("Manifest 'variables' field cannot be null.");
        }

        if (manifest.Scripts is null)
        {
            throw new UmkException("Manifest 'scripts' field cannot be null.");
        }

        foreach (var (scriptName, commands) in manifest.Scripts)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                throw new UmkException("Manifest contains an empty script name.");
            }

            if (commands is null)
            {
                throw new UmkException($"Script '{scriptName}' has a null command list.");
            }
        }
    }
}
