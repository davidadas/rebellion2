using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorNumericGridTests
    {
        private string folder;
        private ContentPackEditorProject project;
        private ContentEditorNumericGrid grid;

        /// <summary>
        /// Opens an isolated derived pack and its shared mission tables.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(
                Path.GetTempPath(),
                "editor-grid-" + Guid.NewGuid().ToString("N")
            );
            project = ContentEditorTestData.CreateProject(folder);
            ContentEditorNavigation navigation = new();
            navigation.Render(project.GetCatalogs());
            navigation.SetExpanded(true);
            grid = new ContentEditorNumericGrid(
                navigation
                    .GetRows(string.Empty)
                    .Single(page => page.Caption == "Shared mission rules")
                    .Fields
            );
        }

        /// <summary>
        /// Removes the fixture-owned pack after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void AddScore_UnfilledThreshold_StaysDetachedAndDoesNotChangeHistory()
        {
            Assert.IsTrue(grid.AddScore(1234));

            Assert.IsFalse(grid.AddScore(1234));
            CollectionAssert.IsOrdered(grid.Scores);
            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.Undo());
            for (int column = 0; column < grid.Columns.Count; column++)
            {
                Assert.AreEqual(string.Empty, grid.GetCell(1234, column).Value);
                Assert.IsFalse(grid.GetEntries(column).Any(entry => entry.Key == 1234));
            }
        }

        [Test]
        public void ClearFields_UnfilledThreshold_DoesNotCreateAnOverrideOrAnUndoEntry()
        {
            grid.AddScore(1234);

            project.ClearFields(
                Enumerable
                    .Range(0, grid.Columns.Count)
                    .Select(column => grid.GetCell(1234, column))
                    .ToArray()
            );

            Assert.IsFalse(project.IsDirty);
            Assert.IsFalse(project.Undo());
        }

        [Test]
        public void GetEntries_FillingADetachedThreshold_ReturnsTheAuthoredValueInScoreOrder()
        {
            grid.AddScore(1234);
            project.SetField(grid.GetCell(1234, 0), "43");

            var entries = grid.GetEntries(0);

            Assert.AreEqual(43, entries.Single(entry => entry.Key == 1234).Value);
            CollectionAssert.IsOrdered(entries.Select(entry => entry.Key));
            Assert.IsTrue(project.Undo());
        }

        [Test]
        public void ClearFields_WholeThreshold_RemovesAllColumnsInOneUndoableEdit()
        {
            int score = grid.Scores.First(candidate =>
                Enumerable
                    .Range(0, grid.Columns.Count)
                    .Count(column => grid.GetCell(candidate, column).Value.Length > 0) > 1
            );
            ContentPackEditorField[] cells = Enumerable
                .Range(0, grid.Columns.Count)
                .Select(column => grid.GetCell(score, column))
                .ToArray();
            ContentPackEditorDocument document = cells[0].Document;
            string before = document.Text;

            project.ClearFields(cells);

            Assert.IsTrue(cells.All(cell => cell.Value.Length == 0));
            Assert.AreNotEqual(before, document.Text);
            Assert.IsTrue(project.Undo());
            Assert.AreEqual(before, document.Text);
            Assert.IsFalse(project.Undo());
        }
    }
}
