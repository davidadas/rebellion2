using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests
{
    /// <summary>
    /// Requires the runtime loop and shares the existing test logging policy with Play Mode tests.
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeTestSetup
    {
        private readonly QuietGameLoggerSetup _logging = new();

        /// <summary>
        /// Verifies runtime test execution before suppressing routine gameplay logs.
        /// </summary>
        [OneTimeSetUp]
        public void SetUp()
        {
            Assert.IsTrue(Application.isPlaying, "Runtime interaction tests require Play Mode.");
            _logging.SuppressRoutineGameLogging();
        }

        /// <summary>
        /// Restores the logging level after every runtime fixture completes.
        /// </summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            _logging.RestoreGameLogging();
        }
    }
}
