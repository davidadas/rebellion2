using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Util
{
    [TestFixture]
    public class GameLoggerTests
    {
        private GameLogger.LogLevel _originalMinimumLevel;
        private string _tempFile;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _originalMinimumLevel = GameLogger.MinimumLevel;
            _tempFile = Path.GetTempFileName();
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Debug);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            GameLogger.Configure(enableFileLogging: false, addTimestamps: true);
            GameLogger.SetMinimumLevel(_originalMinimumLevel);
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        /// <summary>
        /// Verifies log minimum level none does not write to file.
        /// </summary>
        [Test]
        public void Log_MinimumLevelNone_DoesNotWriteToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            File.WriteAllText(_tempFile, string.Empty);
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.None);

            GameLogger.Log("silent");

            Assert.IsEmpty(File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log file logging enabled writes message to file.
        /// </summary>
        [Test]
        public void Log_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.Log("hello world");

            StringAssert.Contains("hello world", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log timestamps enabled message includes timestamp prefix.
        /// </summary>
        [Test]
        public void Log_TimestampsEnabled_MessageIncludesTimestampPrefix()
        {
            GameLogger.Configure(filePath: _tempFile, enableFileLogging: true, addTimestamps: true);

            GameLogger.Log("test message");

            StringAssert.Contains("[20", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log timestamps disabled message omits timestamp prefix.
        /// </summary>
        [Test]
        public void Log_TimestampsDisabled_MessageOmitsTimestampPrefix()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.Log("test message");

            StringAssert.DoesNotContain("[20", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log any level message contains level tag.
        /// </summary>
        [Test]
        public void Log_AnyLevel_MessageContainsLevelTag()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.Log("tag test", GameLogger.LogLevel.Warning);

            StringAssert.Contains("[Warning]", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log level above minimum does not write to file.
        /// </summary>
        [Test]
        public void Log_LevelAboveMinimum_DoesNotWriteToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
            File.WriteAllText(_tempFile, string.Empty);

            GameLogger.Log("silent", GameLogger.LogLevel.Info);

            Assert.IsEmpty(File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies warning file logging enabled writes message to file.
        /// </summary>
        [Test]
        public void Warning_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.Warning("watch out");

            StringAssert.Contains("watch out", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies debug file logging enabled writes message to file.
        /// </summary>
        [Test]
        public void Debug_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.Debug("debug info");

            StringAssert.Contains("debug info", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log format file logging enabled writes formatted message.
        /// </summary>
        [Test]
        public void LogFormat_FileLoggingEnabled_WritesFormattedMessage()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.LogFormat(GameLogger.LogLevel.Info, "Player {0} scored {1}", "Alice", 42);

            StringAssert.Contains("Player Alice scored 42", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies log exception with exception writes type and message.
        /// </summary>
        [Test]
        public void LogException_WithException_WritesTypeAndMessage()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.LogException(
                new InvalidOperationException("boom"),
                GameLogger.LogLevel.Info
            );

            string content = File.ReadAllText(_tempFile);
            StringAssert.Contains("InvalidOperationException", content);
            StringAssert.Contains("boom", content);
        }

        /// <summary>
        /// Verifies set minimum level to error suppresses info messages.
        /// </summary>
        [Test]
        public void SetMinimumLevel_ToError_SuppressesInfoMessages()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
            File.WriteAllText(_tempFile, string.Empty);

            GameLogger.Log("should not appear", GameLogger.LogLevel.Info);

            Assert.IsEmpty(File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies set minimum level to debug allows all messages.
        /// </summary>
        [Test]
        public void SetMinimumLevel_ToDebug_AllowsAllMessages()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Debug);
            File.WriteAllText(_tempFile, string.Empty);

            GameLogger.Log("should appear", GameLogger.LogLevel.Debug);

            StringAssert.Contains("should appear", File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies configure file logging enabled creates new file.
        /// </summary>
        [Test]
        public void Configure_FileLoggingEnabled_CreatesNewFile()
        {
            string newPath = Path.Combine(
                Path.GetTempPath(),
                $"gamelogger_test_{Guid.NewGuid()}.txt"
            );
            try
            {
                GameLogger.Configure(filePath: newPath, enableFileLogging: true);

                Assert.IsTrue(File.Exists(newPath), "Logger should create the file on configure");
            }
            finally
            {
                GameLogger.Configure(enableFileLogging: false, addTimestamps: true);
                if (File.Exists(newPath))
                    File.Delete(newPath);
            }
        }

        /// <summary>
        /// Verifies configure file logging disabled does not write to file.
        /// </summary>
        [Test]
        public void Configure_FileLoggingDisabled_DoesNotWriteToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: false,
                addTimestamps: false
            );
            File.WriteAllText(_tempFile, string.Empty);

            GameLogger.Log("should not write");

            Assert.IsEmpty(File.ReadAllText(_tempFile));
        }

        /// <summary>
        /// Verifies configure null file path retains existing path.
        /// </summary>
        [Test]
        public void Configure_NullFilePath_RetainsExistingPath()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            File.WriteAllText(_tempFile, string.Empty);

            // null filePath keeps the previously configured path.
            GameLogger.Configure(filePath: null, enableFileLogging: true, addTimestamps: false);
            GameLogger.Log("retained path");

            StringAssert.Contains("retained path", File.ReadAllText(_tempFile));
        }
    }
}
