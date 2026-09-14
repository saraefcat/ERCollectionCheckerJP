using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ERCollectionCheckerJP.App.Services;

public sealed class JsonUserSettingsService : IUserSettingsService
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumSettingsFileSize = 64 * 1024;
    public const string ApplicationDirectoryName = "ERCollectionCheckerJP";
    public const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string? settingsPath;

    public JsonUserSettingsService(string? settingsPath = null)
    {
        if (settingsPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
            this.settingsPath = Path.GetFullPath(settingsPath);
            return;
        }

        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            this.settingsPath = Path.Combine(
                Path.GetFullPath(localApplicationData),
                ApplicationDirectoryName,
                SettingsFileName);
        }
    }

    public UserSettings Load()
    {
        if (settingsPath is null)
        {
            return UserSettings.Default;
        }

        try
        {
            var file = new FileInfo(settingsPath);
            file.Refresh();
            if (!file.Exists || file.Length > MaximumSettingsFileSize)
            {
                return UserSettings.Default;
            }

            var document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(settingsPath),
                SerializerOptions);
            return document?.SchemaVersion == CurrentSchemaVersion
                ? new UserSettings(NormalizePath(document.SaveFilePath))
                : UserSettings.Default;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"[JsonUserSettingsService] Settings load failed: {exception}");
            return UserSettings.Default;
        }
    }

    public bool TrySave(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settingsPath is null || Path.GetDirectoryName(settingsPath) is not string directoryPath)
        {
            return false;
        }

        var temporaryPath = Path.Combine(
            directoryPath,
            $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directoryPath);
            var document = new SettingsDocument(
                CurrentSchemaVersion,
                NormalizePath(settings.SaveFilePath));
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, SerializerOptions));
            File.Move(temporaryPath, settingsPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[JsonUserSettingsService] Settings save failed: {exception}");
            TryDeleteTemporaryFile(temporaryPath);
            return false;
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > short.MaxValue)
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[JsonUserSettingsService] Temporary file cleanup failed: {exception}");
        }
    }

    private sealed record SettingsDocument(int? SchemaVersion, string? SaveFilePath);
}
