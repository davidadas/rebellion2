using System;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class FatalErrorReportTests
    {
        private string reportDirectory;

        [SetUp]
        public void SetUp()
        {
            reportDirectory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-fatal-error-{Guid.NewGuid():N}"
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(reportDirectory))
                Directory.Delete(reportDirectory, recursive: true);
            else if (File.Exists(reportDirectory))
                File.Delete(reportDirectory);
        }

        [Test]
        public void Create_LoadFailure_WritesDiagnosticReport()
        {
            InvalidOperationException inner = new InvalidOperationException("missing texture");
            Exception exception = new Exception("content failed", inner);
            DateTimeOffset timestamp = new DateTimeOffset(
                2026,
                9,
                1,
                8,
                30,
                0,
                TimeSpan.FromHours(-4)
            );

            FatalErrorReport report = FatalErrorReport.Create(
                exception,
                "Strategy content loading",
                reportDirectory,
                timestamp,
                "abc123"
            );

            Assert.AreEqual("LOAD-20260901-083000-ABC123", report.ErrorID);
            Assert.AreEqual("Strategy content loading", report.Stage);
            Assert.AreEqual("content failed", report.Message);
            Assert.AreEqual(reportDirectory, report.DirectoryPath);
            Assert.IsTrue(File.Exists(report.FilePath));
            Assert.IsNull(report.WriteFailure);
            StringAssert.Contains("Stage: Strategy content loading", report.Contents);
            StringAssert.Contains(
                "System.InvalidOperationException: missing texture",
                report.Contents
            );
            Assert.AreEqual(report.Contents, File.ReadAllText(report.FilePath));
        }

        [Test]
        public void Create_UnwritableDirectory_ReturnsReportWithWriteFailure()
        {
            File.WriteAllText(reportDirectory, "not a directory");

            FatalErrorReport report = FatalErrorReport.Create(
                new InvalidOperationException("failed"),
                "Application initialization",
                reportDirectory,
                DateTimeOffset.Now,
                "write"
            );

            Assert.IsNull(report.FilePath);
            Assert.IsNotEmpty(report.WriteFailure);
            StringAssert.Contains("InvalidOperationException: failed", report.Contents);
        }
    }
}
