using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorUnitAudioTests : ContentPackEditorTestBase
    {
        [Test]
        public void GetFields_OfficerWithoutVoices_OffersEveryResponseWithoutChangingXml()
        {
            using ContentPackEditorProject project = CreateVoiceProject();
            ContentPackEditorRecord officer = Officer(project);
            string before = officer.Catalog.Document.Text;

            var fields = ContentEditorUnitAudio.GetFields(officer);

            CollectionAssert.AreEqual(
                Enum.GetNames(typeof(OfficerVoiceLineType)),
                fields.Select(field => field.MemberName)
            );
            Assert.IsTrue(
                fields.All(field =>
                    field.IsList && field.MediaType == ContentPackEditorMediaType.Audio
                )
            );
            Assert.IsTrue(fields.All(field => field.Values.Count == 0));
            Assert.IsNull(officer.Element.Element("VoiceSet"));
            Assert.AreEqual(before, officer.Catalog.Document.Text);
            Assert.IsFalse(project.IsDirty);
        }

        [Test]
        public void GetFields_NewOfficer_OffersEveryResponse()
        {
            using ContentPackEditorProject project = CreateVoiceProject();
            ContentPackEditorRecord officer = project.AddUnit(Officer(project).Catalog);

            var fields = ContentEditorUnitAudio.GetFields(officer);

            Assert.AreEqual(Enum.GetValues(typeof(OfficerVoiceLineType)).Length, fields.Count);
            Assert.IsTrue(fields.All(field => field.MediaType == ContentPackEditorMediaType.Audio));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetFields_ImportingFirstClip_AttachesOnlyItsCategoryAndSurvivesSave(
            bool newlyCreated
        )
        {
            using ContentPackEditorProject project = CreateVoiceProject();
            ContentPackEditorRecord officer = newlyCreated
                ? project.AddUnit(Officer(project).Catalog)
                : Officer(project);
            string[] previousCategories =
                officer
                    .Element.Element("VoiceSet")
                    ?.Elements()
                    .Select(item => item.Name.LocalName)
                    .ToArray()
                ?? Array.Empty<string>();
            ContentPackEditorField field = ContentEditorUnitAudio
                .GetFields(officer)
                .Single(item => item.MemberName == "MissionSuccess");
            string source = Path.Combine(
                Application.dataPath,
                "Tests/EditMode/UnitTests/Content/AudioFixtures/tone.wav"
            );
            string sourceBefore = File.ReadAllText(
                Path.Combine(contentRoot, "Packs/BasePack/Data/officers.xml")
            );
            string imported;
            using (project.BeginEdit("Import voice clip"))
            {
                imported = ContentPackEditorStorage.ImportAsset(project, source, field);
                project.SetFieldValues(field, new[] { imported });
            }

            Assert.IsTrue(File.Exists(Path.Combine(project.PackRootPath, imported[5..])));
            Assert.AreEqual(1, officer.Element.Elements("VoiceSet").Count());
            CollectionAssert.AreEqual(
                previousCategories.Append("MissionSuccess"),
                officer.Element.Element("VoiceSet").Elements().Select(item => item.Name.LocalName)
            );
            ContentPackEditorStorage.Save(project);
            using ContentPackEditorProject reopened = ContentPackEditorStorage.OpenFolder(
                contentRoot,
                project.PackRootPath
            );
            ContentPackEditorRecord saved = reopened
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records.Single(item => item.ID == officer.ID);
            using StringReader reader = new(saved.Element.ToString());
            var runtime = (Rebellion.Game.Units.Officer)
                new GameSerializer(typeof(Rebellion.Game.Units.Officer)).Deserialize(reader);
            CollectionAssert.AreEqual(
                new[] { imported },
                runtime.VoiceSet.GetPaths(OfficerVoiceLineType.MissionSuccess)
            );
            Assert.AreEqual(
                sourceBefore,
                File.ReadAllText(Path.Combine(contentRoot, "Packs/BasePack/Data/officers.xml"))
            );
        }

        [Test]
        public void GetFields_EditingTwoMissingCategories_UsesOneVoiceSetAndSupportsUndo()
        {
            using ContentPackEditorProject project = CreateVoiceProject();
            ContentPackEditorRecord officer = Officer(project);
            var fields = ContentEditorUnitAudio.GetFields(officer);
            WriteFixtureFile("Audio/one.wav", "fixture");
            project.SetFieldValues(
                fields.Single(field => field.MemberName == "Order"),
                new[] { "Pack/Audio/one.wav" }
            );
            project.SetFieldValues(
                fields.Single(field => field.MemberName == "Recovered"),
                new[] { "Pack/Audio/one.wav" }
            );

            Assert.AreEqual(1, officer.Element.Elements("VoiceSet").Count());
            Assert.AreEqual(2, officer.Element.Element("VoiceSet").Elements().Count());
            Assert.IsTrue(project.Undo());
            Assert.IsEmpty(
                ContentEditorUnitAudio
                    .GetFields(Officer(project))
                    .Single(field => field.MemberName == "Recovered")
                    .Values
            );
            CollectionAssert.AreEqual(
                new[] { "Pack/Audio/one.wav" },
                ContentEditorUnitAudio
                    .GetFields(Officer(project))
                    .Single(field => field.MemberName == "Order")
                    .Values
            );
        }

        /// <summary>
        /// Creates an inherited officer catalog with optional fixture voice data.
        /// </summary>
        /// <param name="voice">The existing authored voice set, when needed by a test.</param>
        /// <returns>The isolated editable project.</returns>
        private ContentPackEditorProject CreateVoiceProject(string voice = "")
        {
            WriteFixtureFile(
                "Data/officers.xml",
                "<Officers><Officer><TypeID>OFF1</TypeID><DisplayName>Officer</DisplayName>"
                    + voice
                    + "</Officer></Officers>"
            );
            UpdateBaseFactionDefinition(faction => faction.OfficersPath = "Data/officers.xml");
            return CreateWorkingProject();
        }

        /// <summary>
        /// Finds the fixture officer in the project's current document generation.
        /// </summary>
        /// <param name="project">The project owning the effective officer.</param>
        /// <returns>The fixture officer.</returns>
        private static ContentPackEditorRecord Officer(ContentPackEditorProject project) =>
            project
                .GetCatalogs()
                .Single(catalog => catalog.Name == "Officers")
                .Records.First(record => record.ID == "OFF1");
    }
}
