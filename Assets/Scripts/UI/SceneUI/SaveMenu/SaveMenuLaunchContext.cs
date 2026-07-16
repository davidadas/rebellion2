/// <summary>
/// Carries save-menu navigation mode between scenes.
/// </summary>
public static class SaveMenuLaunchContext
{
    /// <summary>
    /// Identifies the main-menu scene.
    /// </summary>
    public const string MainMenuSceneName = "MainMenu";

    /// <summary>
    /// Identifies the save-menu scene.
    /// </summary>
    public const string SaveMenuSceneName = "SaveMenu";

    /// <summary>
    /// Identifies the strategy-view scene.
    /// </summary>
    public const string StrategyViewSceneName = "StrategyView";

    /// <summary>
    /// Gets whether the current navigation context permits saving.
    /// </summary>
    public static bool CanSave { get; private set; }

    /// <summary>
    /// Gets the scene that opened the save menu.
    /// </summary>
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
