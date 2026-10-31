using System;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentPackEditorSessionTests
    {
        private ContentPackEditorRecord officer;

        /// <summary>
        /// Creates a detached officer record for navigation state checks.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "session-" + Guid.NewGuid().ToString("N") + ".xml"
            );
            officer = new ContentPackEditorCatalog(
                new ContentPackEditorDocument(
                    "Pack/officers.xml",
                    path,
                    path,
                    "<Officers><Officer><TypeID>ONE</TypeID><DisplayName>One</DisplayName></Officer></Officers>"
                )
            ).Records[0];
        }

        [Test]
        public void Select_ArtworkTab_StaysSelectedAcrossUnitSelections()
        {
            using ContentPackEditorSession session = new ContentPackEditorSession();
            session.SelectSection("Artwork");
            session.Select(officer, null, null);
            Assert.AreSame(officer, session.Selection.Record);
            Assert.AreEqual("Artwork", session.Selection.Section);
        }

        [TestCase("Advanced")]
        [TestCase("Rules")]
        public void Select_ObjectSpecificTab_ReturnsToGeneralForAnotherSelection(string section)
        {
            using ContentPackEditorSession session = new ContentPackEditorSession();
            session.SelectSection(section);
            session.Select(officer, null, null);
            Assert.AreEqual("General", session.Selection.Section);
        }

        [Test]
        public void Select_MissionWithoutArtwork_OpensItsRules()
        {
            using ContentPackEditorSession session = new ContentPackEditorSession();
            ContentEditorTreeNode page = new ContentEditorTreeNode(
                "mission/test",
                "Test mission",
                0,
                record: officer,
                fields: officer.GetFields(),
                missionID: "Test"
            );
            session.Select(officer, page, null);
            Assert.AreSame(page, session.Selection.Page);
            Assert.AreEqual("Rules", session.Selection.Section);
        }
    }
}
