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

        [Test]
        public async Task ValidateAsync_MissingStartingOfficer_RejectsContentThatCannotStartAGameAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog generation = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Starting setup");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(
                generation.Document.Text
            );
            xml.Descendants("OfficerInstanceID").First().Value = "missing-starting-officer";
            project.SetDocumentText(generation.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains("missing-starting-officer", error.Message);
        }

        [Test]
        public async Task ValidateAsync_PlanetWithMissingArtwork_RejectsTheNestedReferenceAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog map = project.GetCatalogs().First(catalog => catalog.IsMap);
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(map.Document.Text);
            xml.Descendants("Planet")
                .First()
                .SetElementValue("DisplayImagePath", "Pack/missing-planet-art.png");
            project.SetDocumentText(map.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains("File not found", error.Message);
        }

        [TestCase("weighted unit")]
        [TestCase("weighted building")]
        [TestCase("fleet cargo")]
        [TestCase("fleet ship")]
        [TestCase("fleet destination")]
        public async Task ValidateAsync_UnselectedGenerationReference_RejectsTheMissingTargetAsync(
            string location
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog generation = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Starting setup");
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(
                generation.Document.Text
            );
            System.Xml.Linq.XElement target = location switch
            {
                "weighted unit" => xml.Descendants("WeightedUnitEntry")
                    .First()
                    .Descendants("TypeID")
                    .First(),
                "weighted building" => xml.Descendants("WeightedFacilityEntry")
                    .First(entry => entry.Element("TypeID") != null)
                    .Element("TypeID"),
                "fleet cargo" => xml.Descendants("Cargo").First().Descendants("TypeID").First(),
                "fleet ship" => xml.Descendants("FixedFleetShip").First().Element("TypeID"),
                _ => xml.Descendants("FixedFleet").First().Element("PlanetTypeID"),
            };
            target.Value = "missing-generation-reference";
            project.SetDocumentText(generation.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains("missing-generation-reference", error.Message);
        }

        [Test]
        public async Task ValidateAsync_CrossCategoryDuplicateUnitType_RejectsTheAmbiguousFactoryLookupAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog fighters = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Starfighters");
            string shipType = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Capital ships")
                .Records.First()
                .ID;
            System.Xml.Linq.XDocument xml = System.Xml.Linq.XDocument.Parse(fighters.Document.Text);
            xml.Descendants("Starfighter").First().SetElementValue("TypeID", shipType);
            project.SetDocumentText(fighters.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains("Duplicate unit TypeID", error.Message);
        }

        /// <summary>
        /// Captures a failure from the asynchronous validation API.
        /// </summary>
        /// <param name="project">The invalid project.</param>
        /// <returns>The captured validation exception.</returns>
        private static async Task<Exception> GetValidationErrorAsync(
            ContentPackEditorProject project
        )
        {
            return await ExpectFailureAsync<Exception>(() =>
                ContentEditorPackValidator.ValidateAsync(project)
            );
        }
    }
}
