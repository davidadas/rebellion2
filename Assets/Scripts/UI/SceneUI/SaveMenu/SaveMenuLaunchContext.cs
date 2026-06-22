public static class SaveMenuLaunchContext
{
    public const string MainMenuSceneName = "MainMenu";
    public const string SaveMenuSceneName = "SaveMenu";
    public const string StrategyViewSceneName = "StrategyView";

    private static string returnSceneName = MainMenuSceneName;

    public static bool CanSave { get; private set; }
    public static string ReturnSceneName => returnSceneName;

    public static void OpenFromMainMenu()
    {
        returnSceneName = MainMenuSceneName;
        CanSave = false;
    }

    public static void OpenFromStrategyView()
    {
        returnSceneName = StrategyViewSceneName;
        CanSave = true;
    }

    public static void Reset()
    {
        returnSceneName = MainMenuSceneName;
        CanSave = false;
    }
}
