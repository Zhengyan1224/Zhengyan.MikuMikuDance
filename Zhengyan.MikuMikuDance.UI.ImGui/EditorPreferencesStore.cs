using System.Runtime.InteropServices;
using System.Text.Json;

namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public EditorPreferencesStore(string? filePath = null)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath) ? GetDefaultFilePath() : filePath;
    }

    public string FilePath { get; }

    public EditorPreferences Load()
    {
        if (!File.Exists(FilePath))
        {
            return EditorPreferences.CreateDefault();
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            var preferences = JsonSerializer.Deserialize<EditorPreferences>(stream, JsonOptions) ?? EditorPreferences.CreateDefault();
            preferences.Normalize();
            return preferences;
        }
        catch (JsonException)
        {
            return EditorPreferences.CreateDefault();
        }
        catch (IOException)
        {
            return EditorPreferences.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return EditorPreferences.CreateDefault();
        }
    }

    public void Save(EditorPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        preferences.Normalize();
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(FilePath);
        JsonSerializer.Serialize(stream, preferences, JsonOptions);
    }

    public static string GetDefaultFilePath()
    {
        return Path.Combine(GetConfigRootDirectory(), "Zhengyan.MikuMikuDance", "editor-preferences.json");
    }

    private static string GetConfigRootDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(appData) ? GetHomeFallback(".config") : appData;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(GetHomeFallback("."), "Library", "Application Support");
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return string.IsNullOrWhiteSpace(xdgConfigHome)
            ? GetHomeFallback(".config")
            : xdgConfigHome;
    }

    private static string GetHomeFallback(string childPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = AppContext.BaseDirectory;
        }

        return childPath == "." ? home : Path.Combine(home, childPath);
    }
}
