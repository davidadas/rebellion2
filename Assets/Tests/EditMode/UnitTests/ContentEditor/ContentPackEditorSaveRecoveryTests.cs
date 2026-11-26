using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorSaveRecoveryTests : ContentPackEditorTestBase
    {
        [Test]
        public void Restore_MultiplePermissionFailures_AttemptsEveryFileAndRetainsBlockedRevisions()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Ignore(
                    "This test uses Unix directory permissions to block file replacement."
                );
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument first = CreateDocument(project, "first.xml", "<First />");
            string blocked = Path.Combine(project.PackRootPath, "blocked");
            Directory.CreateDirectory(blocked);
            ContentPackEditorDocument second = CreateDocument(
                project,
                "blocked/second.xml",
                "<Second />"
            );
            ContentPackEditorDocument third = CreateDocument(
                project,
                "blocked/third.xml",
                "<Third />"
            );
            ContentPackEditorSaveRecovery recovery = new(project, new[] { first, second, third });
            recovery.Write(first);
            recovery.Write(second);
            recovery.Write(third);
            Assert.AreEqual(0, SetDirectoryPermissions(blocked, 320));
            try
            {
                string probe = Path.Combine(blocked, "probe");
                try
                {
                    File.WriteAllText(probe, "probe");
                    File.Delete(probe);
                    Assert.Ignore("The filesystem permits writes despite the permission change.");
                }
                catch (UnauthorizedAccessException) { }

                IOException failure = Assert.Throws<IOException>(recovery.Restore);

                Assert.AreEqual(
                    2,
                    ((AggregateException)failure.InnerException).InnerExceptions.Count
                );
                Assert.AreEqual("<First />", File.ReadAllText(first.TargetPath));
                Assert.AreEqual(second.Text, File.ReadAllText(second.TargetPath));
                Assert.AreEqual(third.Text, File.ReadAllText(third.TargetPath));
                Assert.IsTrue(recovery.HasPendingRestores);
                Assert.IsTrue(Directory.Exists(recovery.DirectoryPath));
            }
            finally
            {
                Assert.AreEqual(0, SetDirectoryPermissions(blocked, 448));
            }

            recovery.Restore();

            Assert.AreEqual("<Second />", File.ReadAllText(second.TargetPath));
            Assert.AreEqual("<Third />", File.ReadAllText(third.TargetPath));
            Assert.IsFalse(recovery.HasPendingRestores);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Restore_PrecedingXmlUsesAnotherEncoding_RestoresItsExactBytes()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            string path = Path.Combine(project.PackRootPath, "encoded.xml");
            byte[] saved = Encoding
                .Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes("<Saved />"))
                .ToArray();
            File.WriteAllBytes(path, saved);
            ContentPackEditorDocument document = new(
                "Pack/encoded.xml",
                path,
                path,
                File.ReadAllText(path)
            );
            document.SetText("<Changed />");
            ContentPackEditorSaveRecovery recovery = new(project, new[] { document });
            recovery.Write(document);

            recovery.Restore();

            CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
        }

        [Test]
        public void Restore_ExternalConflict_RestoresOtherWritesAndRetainsRecoveryForRetry()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument first = CreateDocument(project, "first.xml", "<First />");
            ContentPackEditorDocument second = CreateDocument(project, "second.xml", "<Second />");
            ContentPackEditorSaveRecovery recovery = new(project, new[] { first, second });
            recovery.Write(first);
            recovery.Write(second);
            File.WriteAllText(second.TargetPath, "<External />");

            IOException failure = Assert.Throws<IOException>(recovery.Restore);

            StringAssert.Contains(recovery.DirectoryPath, failure.Message);
            Assert.AreEqual("<First />", File.ReadAllText(first.TargetPath));
            Assert.AreEqual("<External />", File.ReadAllText(second.TargetPath));
            Assert.IsTrue(recovery.HasPendingRestores);
            XElement manifest = XElement.Load(Path.Combine(recovery.DirectoryPath, "recovery.xml"));
            XElement entry = manifest
                .Elements()
                .Single(file => (string)file.Attribute("Path") == "second.xml");
            Assert.AreEqual(
                "<Second />",
                File.ReadAllText(
                    Path.Combine(recovery.DirectoryPath, (string)entry.Attribute("Previous"))
                )
            );
            File.WriteAllText(second.TargetPath, second.Text);

            recovery.Restore();

            Assert.AreEqual("<Second />", File.ReadAllText(second.TargetPath));
            Assert.IsFalse(recovery.HasPendingRestores);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Restore_AlreadyRestoredExternally_AcceptsThePrecedingBytes()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument document = CreateDocument(project, "first.xml", "<First />");
            ContentPackEditorSaveRecovery recovery = new(project, new[] { document });
            recovery.Write(document);
            File.WriteAllText(document.TargetPath, "<First />");

            recovery.Restore();

            Assert.AreEqual("<First />", File.ReadAllText(document.TargetPath));
            Assert.IsFalse(recovery.HasPendingRestores);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Restore_PreviouslyAbsentDocument_RemovesThePublishedFile()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            string path = Path.Combine(project.PackRootPath, "new.xml");
            ContentPackEditorDocument document = new("Pack/new.xml", path);
            document.SetText("<New />");
            ContentPackEditorSaveRecovery recovery = new(project, new[] { document });
            recovery.Write(document);

            recovery.Restore();

            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Commit_AcceptedDocuments_KeepsPublishedBytesAndRemovesRecovery()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument document = CreateDocument(project, "first.xml", "<First />");
            ContentPackEditorSaveRecovery recovery = new(project, new[] { document });
            recovery.Write(document);

            recovery.Commit();

            Assert.AreEqual(document.Text, File.ReadAllText(document.TargetPath));
            Assert.IsFalse(recovery.HasPendingRestores);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void DiscardChanges_UnresolvedDocumentSave_RemainsDirtyAfterUndoUntilRecoverySucceeds()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            (
                ContentPackEditorDocument document,
                ContentPackEditorSaveRecovery recovery,
                string saved
            ) = PublishManifest(project);
            string written = document.Text;
            File.WriteAllText(document.TargetPath, "<External />");
            Assert.IsTrue(project.Undo());

            Assert.Throws<IOException>(project.DiscardChanges);

            Assert.IsTrue(project.IsDirty);
            Assert.IsFalse(document.IsDirty);
            Assert.IsTrue(Directory.Exists(recovery.DirectoryPath));
            File.WriteAllText(document.TargetPath, written);
            project.DiscardChanges();
            Assert.AreEqual(saved, File.ReadAllText(document.TargetPath));
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Save_ResolvedIncompleteSave_RestoresThenPersistsTheCurrentDraft()
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            (ContentPackEditorDocument document, ContentPackEditorSaveRecovery recovery, string _) =
                PublishManifest(project);
            string written = document.Text;
            File.WriteAllText(document.TargetPath, "<External />");
            Assert.Throws<IOException>(() => ContentPackEditorStorage.Save(project));
            File.WriteAllText(document.TargetPath, written);

            ContentPackEditorStorage.Save(project);

            Assert.AreEqual(written, File.ReadAllText(document.TargetPath));
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(Directory.Exists(recovery.DirectoryPath));
        }

        [Test]
        public void Dispose_UnresolvedDocumentSave_RetainsDurablePrecedingBytes()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            (
                ContentPackEditorDocument document,
                ContentPackEditorSaveRecovery recovery,
                string saved
            ) = PublishManifest(project);
            File.WriteAllText(document.TargetPath, "<External />");

            project.Dispose();

            Assert.AreEqual("<External />", File.ReadAllText(document.TargetPath));
            Assert.IsTrue(Directory.Exists(recovery.DirectoryPath));
            Assert.IsTrue(
                Directory
                    .GetFiles(recovery.DirectoryPath)
                    .Any(path => File.ReadAllText(path) == saved)
            );
        }

        /// <summary>
        /// Prepares a standalone document with distinct preceding and published XML bytes.
        /// </summary>
        /// <param name="project">The writable owner of the fixture file.</param>
        /// <param name="name">The pack-relative document name.</param>
        /// <param name="saved">The exact preceding file contents.</param>
        /// <returns>The edited document whose preceding contents remain on disk.</returns>
        private static ContentPackEditorDocument CreateDocument(
            ContentPackEditorProject project,
            string name,
            string saved
        )
        {
            string path = Path.Combine(project.PackRootPath, name);
            File.WriteAllText(path, saved);
            ContentPackEditorDocument document = new("Pack/" + name, path, path, saved);
            document.SetText("<Changed />");
            return document;
        }

        /// <summary>
        /// Publishes the first document through the same recovery ownership used by Save.
        /// </summary>
        /// <param name="project">The editable pack owning the transaction.</param>
        /// <returns>The published manifest, retained transaction, and exact preceding bytes.</returns>
        private static (
            ContentPackEditorDocument,
            ContentPackEditorSaveRecovery,
            string
        ) PublishManifest(ContentPackEditorProject project)
        {
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .Single(item => item.Document.Address == "Pack/pack.xml");
            ContentPackEditorDocument document = catalog.Document;
            string saved = File.ReadAllText(document.TargetPath);
            project.SetField(catalog.DocumentRecord.GetField("DisplayName"), "Unsaved pack name");
            ContentPackEditorSaveRecovery recovery = project.BeginDocumentSave(new[] { document });
            recovery.Write(document);
            return (document, recovery, saved);
        }

        /// <summary>
        /// Applies real directory permissions to exercise incomplete filesystem rollback.
        /// </summary>
        /// <param name="path">The owned fixture directory.</param>
        /// <param name="mode">The Unix permission bits.</param>
        /// <returns>Zero when the permission change succeeds.</returns>
        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int SetDirectoryPermissions(string path, uint mode);
    }
}
