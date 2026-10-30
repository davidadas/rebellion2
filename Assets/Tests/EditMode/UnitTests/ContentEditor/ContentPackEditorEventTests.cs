using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorEventTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentPackEditorCatalog events;

        /// <summary>
        /// Opens an isolated derived pack over the installed event definitions.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            string contentRoot = ContentPackLoader.ResolveContentRootPath();
            ContentPackDefinition source = ContentPackLoader
                .DiscoverPacks(contentRoot)
                .First(pack => string.IsNullOrEmpty(pack.BasePackID));
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-event-" + Guid.NewGuid().ToString("N")
            );
            project = ContentPackEditorProject.CreateInFolder(
                contentRoot,
                source.ID,
                folder,
                "event-test",
                "Event test",
                "1.0.0"
            );
            events = project.GetCatalogs().First(catalog => catalog.CanAddEvent);
        }

        /// <summary>
        /// Removes only this fixture's authored working folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Directory.Delete(folder, true);
        }

        [Test]
        public void SetEventXml_UnchangedInheritedEvent_PreservesTheDocumentAndCreatesNoOverride()
        {
            ContentPackEditorRecord original = events.Records.First();
            string before = events.Document.Text;
            ContentPackEditorRecord result = project.SetEventXml(
                original,
                original.Element.ToString()
            );
            Assert.AreSame(original, result);
            Assert.AreEqual(before, events.Document.Text);
            Assert.IsTrue(events.Document.IsInherited);
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
            Assert.IsFalse(File.Exists(events.Document.TargetPath));
        }

        [Test]
        public void SetEventXml_ValidNestedActions_ReplacesOnlyTheSelectedEventAndCanUndo()
        {
            ContentPackEditorRecord selected = events.Records.First();
            string before = events.Document.Text;
            string[] siblings = events
                .Records.Skip(1)
                .Select(record => record.Element.ToString())
                .ToArray();
            string replacement = EventXml(
                "editor-unique-event",
                "<Actions><SetEventVariable><Key>test.value</Key><Operand>3</Operand></SetEventVariable></Actions>"
            );
            ContentPackEditorRecord result = project.SetEventXml(selected, replacement);
            Assert.AreEqual("editor-unique-event", result.ID);
            CollectionAssert.AreEqual(
                siblings,
                events.Records.Skip(1).Select(record => record.Element.ToString())
            );
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, events.Document.Text);
            Assert.IsTrue(project.Redo());
            StringAssert.Contains("editor-unique-event", events.Document.Text);
        }

        [TestCase("<Actions><UnknownAction /></Actions>")]
        [TestCase("<Actions><SetEventVariable /></Actions>")]
        [TestCase("<Invalid />")]
        public void SetEventXml_InvalidConfiguration_LeavesAllContentAndHistoryUnchanged(
            string actions
        )
        {
            string before = events.Document.Text;
            Assert.Catch(() =>
                project.SetEventXml(events.Records.First(), EventXml("unique-event", actions))
            );
            Assert.AreEqual(before, events.Document.Text);
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.CanUndo);
        }

        [Test]
        public void SetEventXml_DuplicateIdentity_RejectsTheEdit()
        {
            Assert.Throws<InvalidDataException>(() =>
                project.SetEventXml(
                    events.Records.First(),
                    EventXml(events.Records.Skip(1).First().ID, "<Actions />")
                )
            );
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void AddEvent_BlankEvent_HasAUniqueIdentityAndEditableValidConfiguration()
        {
            ContentPackEditorRecord first = project.AddEvent(events);
            ContentPackEditorRecord second = project.AddEvent(events);
            Assert.AreNotEqual(first.ID, second.ID);
            Assert.IsEmpty(first.Element.Element("Actions").Elements());
            Assert.DoesNotThrow(() => project.SetEventXml(second, second.Element.ToString()));
        }

        /// <summary>
        /// Builds a scheduled fixture event containing the supplied action configuration.
        /// </summary>
        /// <param name="id">The fixture event identity.</param>
        /// <param name="actions">The action container to validate.</param>
        /// <returns>The complete fixture event XML.</returns>
        private static string EventXml(string id, string actions)
        {
            return "<GameEvent MaximumActivations=\"1\"><InstanceID>"
                + id
                + "</InstanceID><Schedule><Every Ticks=\"1\" /></Schedule>"
                + actions
                + "</GameEvent>";
        }
    }
}
