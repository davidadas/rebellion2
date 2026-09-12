using NUnit.Framework;
using Rebellion.Util.Common;

[SetUpFixture]
public sealed class QuietGameLoggerSetup
{
    private GameLogger.LogLevel _originalMinimumLevel;

    [OneTimeSetUp]
    public void SuppressRoutineGameLogging()
    {
        _originalMinimumLevel = GameLogger.MinimumLevel;
        GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
    }

    [OneTimeTearDown]
    public void RestoreGameLogging()
    {
        GameLogger.SetMinimumLevel(_originalMinimumLevel);
    }
}
