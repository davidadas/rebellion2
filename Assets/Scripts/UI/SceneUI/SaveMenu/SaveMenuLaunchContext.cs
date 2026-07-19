/// <summary>
/// Carries save-menu navigation mode between scenes.
/// </summary>
public static class SaveMenuLaunchContext
{
    public const string MainMenuSceneName = "MainMenu";

    public const string SaveMenuSceneName = "SaveMenu";

    public const string StrategyViewSceneName = "StrategyView";

    public static bool CanSave { get; private set; }

    public static string ReturnSceneName { get; private set; } = MainMenuSceneName;

    /// <summary>
    /// Configures the save menu for loading from the main menu.
    /// </summary>
    public static void OpenFromMainMenu()
    {
        ReturnSceneName = MainMenuSceneName;
        CanSave = false;
    }

    /// <summary>
    /// Configures the save menu for saving or loading from strategy gameplay.
    /// </summary>
    public static void OpenFromStrategyView()
    {
        ReturnSceneName = StrategyViewSceneName;
        CanSave = true;
    }

    /// <summary>
    /// Restores the default main-menu launch mode.
    /// </summary>
    public static void Reset()
    {
        ReturnSceneName = MainMenuSceneName;
        CanSave = false;
    }
}
