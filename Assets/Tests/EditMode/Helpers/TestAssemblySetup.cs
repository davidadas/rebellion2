using NUnit.Framework;
using Rebellion.Util.Common;

/// <summary>
/// Assembly-level setup: silences GameLogger so production-code Log calls don't
/// pollute test output on passing runs.
/// </summary>
[SetUpFixture]
public class TestAssemblySetup
{
    [OneTimeSetUp]
    public void SuppressLogging() => GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
}
