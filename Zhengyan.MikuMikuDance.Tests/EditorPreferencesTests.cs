using Zhengyan.MikuMikuDance.UI.ImGui;

namespace Zhengyan.MikuMikuDance.Tests;

public sealed class EditorPreferencesTests
{
    [Fact]
    public void StoreRoundTripsPreferences()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zymmd-prefs-{Guid.NewGuid():N}.json");
        try
        {
            var store = new EditorPreferencesStore(path);
            var preferences = EditorPreferences.CreateDefault();
            preferences.WindowWidth = 1920;
            preferences.WindowHeight = 1080;
            preferences.VSync = false;
            preferences.Theme = EditorPreferences.LightTheme;
            preferences.ClearColor = new EditorColor(0.1f, 0.2f, 0.3f, 1f);
            preferences.ShowViewportGrid = false;
            preferences.ShowPointedDebug = false;
            preferences.Panels.Scene = false;
            preferences.Panels.Preferences = true;

            store.Save(preferences);
            var loaded = store.Load();

            Assert.Equal(1920, loaded.WindowWidth);
            Assert.Equal(1080, loaded.WindowHeight);
            Assert.False(loaded.VSync);
            Assert.Equal(EditorPreferences.LightTheme, loaded.Theme);
            Assert.Equal(0.1f, loaded.ClearColor.Red, precision: 3);
            Assert.Equal(0.2f, loaded.ClearColor.Green, precision: 3);
            Assert.Equal(0.3f, loaded.ClearColor.Blue, precision: 3);
            Assert.False(loaded.ShowViewportGrid);
            Assert.False(loaded.ShowPointedDebug);
            Assert.False(loaded.Panels.Scene);
            Assert.True(loaded.Panels.Preferences);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadReturnsDefaultsForInvalidJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zymmd-prefs-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ invalid json");
            var preferences = new EditorPreferencesStore(path).Load();

            Assert.Equal(1440, preferences.WindowWidth);
            Assert.Equal(900, preferences.WindowHeight);
            Assert.True(preferences.VSync);
            Assert.Equal(EditorPreferences.DarkTheme, preferences.Theme);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NormalizeClampsInvalidValues()
    {
        var preferences = new EditorPreferences
        {
            WindowWidth = 10,
            WindowHeight = 99999,
            Theme = "unknown",
            ClearColor = new EditorColor(-1f, 2f, 0.5f, 3f)
        };

        preferences.Normalize();

        Assert.Equal(640, preferences.WindowWidth);
        Assert.Equal(4320, preferences.WindowHeight);
        Assert.Equal(EditorPreferences.DarkTheme, preferences.Theme);
        Assert.Equal(0f, preferences.ClearColor.Red);
        Assert.Equal(1f, preferences.ClearColor.Green);
        Assert.Equal(0.5f, preferences.ClearColor.Blue);
        Assert.Equal(1f, preferences.ClearColor.Alpha);
    }
}
