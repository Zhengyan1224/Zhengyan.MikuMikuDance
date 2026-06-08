namespace Zhengyan.MikuMikuDance.UI.ImGui;

public sealed class EditorPreferences
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";
    public const string ClassicTheme = "Classic";

    private static readonly string[] Themes = [DarkTheme, LightTheme, ClassicTheme];

    public int WindowWidth { get; set; } = 1440;

    public int WindowHeight { get; set; } = 900;

    public bool VSync { get; set; } = true;

    public string Theme { get; set; } = DarkTheme;

    public EditorColor ClearColor { get; set; } = new(0.07f, 0.075f, 0.08f, 1f);

    public bool ShowViewportGrid { get; set; } = true;

    public bool ShowPointedDebug { get; set; } = true;

    public EditorPanelPreferences Panels { get; set; } = new();

    public static IReadOnlyList<string> AvailableThemes => Themes;

    public static EditorPreferences CreateDefault() => new();

    public void Normalize()
    {
        WindowWidth = Math.Clamp(WindowWidth, 640, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 480, 4320);
        if (!Themes.Contains(Theme, StringComparer.OrdinalIgnoreCase))
        {
            Theme = DarkTheme;
        }
        else
        {
            Theme = Themes.First(theme => string.Equals(theme, Theme, StringComparison.OrdinalIgnoreCase));
        }

        ClearColor ??= new EditorColor(0.07f, 0.075f, 0.08f, 1f);
        ClearColor.Red = Math.Clamp(ClearColor.Red, 0f, 1f);
        ClearColor.Green = Math.Clamp(ClearColor.Green, 0f, 1f);
        ClearColor.Blue = Math.Clamp(ClearColor.Blue, 0f, 1f);
        ClearColor.Alpha = Math.Clamp(ClearColor.Alpha, 0f, 1f);
        Panels ??= new EditorPanelPreferences();
    }

    public void CopyFrom(EditorPreferences source)
    {
        ArgumentNullException.ThrowIfNull(source);
        WindowWidth = source.WindowWidth;
        WindowHeight = source.WindowHeight;
        VSync = source.VSync;
        Theme = source.Theme;
        ClearColor = new EditorColor(source.ClearColor.Red, source.ClearColor.Green, source.ClearColor.Blue, source.ClearColor.Alpha);
        ShowViewportGrid = source.ShowViewportGrid;
        ShowPointedDebug = source.ShowPointedDebug;
        Panels.CopyFrom(source.Panels);
        Normalize();
    }
}
