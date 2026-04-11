using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Util.Common;

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
        }

        [TearDown]
        public void TearDown()
        {
            GameLogger.Configure(enableFileLogging: false, addTimestamps: true);
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

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
        public void LogException_FileLoggingEnabled_WritesExceptionTypeAndMessage()
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
        public void Error_FileLoggingEnabled_WritesMessageToFile()
        {
            GameLogger.Configure(
                filePath: _tempFile,
                enableFileLogging: true,
                addTimestamps: false
            );

            GameLogger.Error("something went wrong");

            StringAssert.Contains("something went wrong", File.ReadAllText(_tempFile));
        }

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
    }
}
