using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Util.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Util
{
    [TestFixture]
    public class GameLoggerTests
    {
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _tempFile = Path.GetTempFileName();
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Debug);
        }

        [TearDown]
        public void TearDown()
        {
            GameLogger.Configure(enableFileLogging: false, addTimestamps: true);
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        // ── Log ──────────────────────────────────────────────────────────────

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

        [Test]
        public void Log_TimestampsEnabled_MessageIncludesTimestampPrefix()
        {
            GameLogger.Configure(filePath: _tempFile, enableFileLogging: true, addTimestamps: true);

            GameLogger.Log("test message");

            StringAssert.Contains("[20", File.ReadAllText(_tempFile));
        }

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

        [Test]
        public void Log_MessageContainsLevelTag()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            LogAssert.Expect(LogType.Warning, new Regex(".*"));

            GameLogger.Log("tag test", GameLogger.LogLevel.Warning);

            StringAssert.Contains("[Warning]", File.ReadAllText(_tempFile));
        }

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

        // ── Warning / Debug / Error ───────────────────────────────────────────

        [Test]
        public void Warning_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            LogAssert.Expect(LogType.Warning, new Regex(".*"));

            GameLogger.Warning("watch out");

            StringAssert.Contains("watch out", File.ReadAllText(_tempFile));
        }

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

        [Test]
        public void Error_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            LogAssert.Expect(LogType.Error, new Regex(".*"));

            GameLogger.Error("something went wrong");

            StringAssert.Contains("something went wrong", File.ReadAllText(_tempFile));
        }

        // ── LogFormat ─────────────────────────────────────────────────────────

        [Test]
        public void LogFormat_WritesFormattedMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.LogFormat(GameLogger.LogLevel.Info, "Player {0} scored {1}", "Alice", 42);

            StringAssert.Contains("Player Alice scored 42", File.ReadAllText(_tempFile));
        }

        // ── LogException ──────────────────────────────────────────────────────

        [Test]
        public void LogException_WritesExceptionTypeAndMessage()
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

        [Test]
        public void LogException_DefaultLevel_IsError()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );
            GameLogger.SetMinimumLevel(GameLogger.LogLevel.Error);
            File.WriteAllText(_tempFile, string.Empty);
            LogAssert.Expect(LogType.Error, new Regex(".*"));

            // Default level is Error — should still write when minimum is Error.
            GameLogger.LogException(new Exception("oops"));

            StringAssert.Contains("oops", File.ReadAllText(_tempFile));
        }

        // ── SetMinimumLevel ───────────────────────────────────────────────────

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

        // ── Configure ────────────────────────────────────────────────────────

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
