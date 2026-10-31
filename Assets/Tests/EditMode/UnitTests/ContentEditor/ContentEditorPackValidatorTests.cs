using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorPackValidatorTests : ContentPackEditorTestBase
    {
        [Test]
        public async Task ValidateAsync_UnknownFactionInSavedXml_RejectsThePackAsync()
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>one</TypeID><RecruitingFactionInstanceIDs><string>missing</string></RecruitingFactionInstanceIDs></Officer></Officers>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            ArgumentException error = await ExpectFailureAsync<ArgumentException>(() =>
                ContentEditorPackValidator.ValidateAsync(project)
            );
            Assert.AreEqual("Choose a listed faction.", error.Message);
        }

        [Test]
        public async Task ValidateAsync_CancelledBeforeStartup_ReturnsControlAndPreservesTheProjectAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task pending = ContentEditorPackValidator.ValidateAsync(
                project,
                cancellationToken: cancellation.Token
            );
            Assert.IsFalse(pending.IsCompleted);

            cancellation.Cancel();
            try
            {
                await pending;
                Assert.Fail("Validation must observe cancellation.");
            }
            catch (OperationCanceledException) { }

            Assert.IsFalse(project.IsDirty);
            Assert.AreEqual(
                1,
                Directory
                    .EnumerateFiles(project.PackRootPath, "*", SearchOption.AllDirectories)
                    .Count()
            );
        }
    }
}
