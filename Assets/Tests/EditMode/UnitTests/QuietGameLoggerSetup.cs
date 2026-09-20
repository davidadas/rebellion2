using NUnit.Framework;
using Rebellion.Util.Logging;

[SetUpFixture]
public sealed class QuietGameLoggerSetup
{
    private GameLogger.LogLevel _originalMinimumLevel;

    /// <summary>
    /// Executes suppress routine game logging.
    /// </summary>
    [OneTimeSetUp]
    public void SuppressRoutineGameLogging()
    {
        _originalMinimumLevel = GameLogger.MinimumLevel;
        GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
    }

    /// <summary>
    /// Restores game logging.
    /// </summary>
    [OneTimeTearDown]
    public void RestoreGameLogging()
    {
        GameLogger.SetMinimumLevel(_originalMinimumLevel);
    }
}
