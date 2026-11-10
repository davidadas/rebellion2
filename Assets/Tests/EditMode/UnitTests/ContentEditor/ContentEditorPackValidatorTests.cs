using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
            if (location == "weighted building")
                xml.Descendants("CoreFacilityTable")
                    .First()
                    .AddFirst(
                        new System.Xml.Linq.XElement(
                            "WeightedFacilityEntry",
                            new System.Xml.Linq.XElement("CumulativeWeight", 0),
                            new System.Xml.Linq.XElement("TypeID", "missing-generation-reference")
                        )
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

        [TestCase("Building", "ConstructionCost", -1)]
        [TestCase("CapitalShip", "ConstructionCost", -1)]
        [TestCase("Starfighter", "ConstructionCost", -1)]
        [TestCase("Regiment", "ConstructionCost", -1)]
        [TestCase("SpecialForce", "ConstructionCost", -1)]
        [TestCase("Building", "MaintenanceCost", -1)]
        [TestCase("CapitalShip", "MaintenanceCost", -1)]
        [TestCase("Starfighter", "MaintenanceCost", -1)]
        [TestCase("Regiment", "MaintenanceCost", -1)]
        [TestCase("SpecialForce", "MaintenanceCost", -1)]
        [TestCase("Building", "ProcessRate", -1)]
        [TestCase("CapitalShip", "StarfighterCapacity", -1)]
        [TestCase("CapitalShip", "RegimentCapacity", -1)]
        [TestCase("Officer", "JediProbability", -1)]
        [TestCase("Officer", "JediProbability", 101)]
        [TestCase("Officer", "ForceValue", -32769)]
        [TestCase("Officer", "ForceValue", 32768)]
        [TestCase("Officer", "ForceTrainingAdjustment", -32769)]
        [TestCase("Officer", "ForceTrainingAdjustment", 32768)]
        public async Task ValidateAsync_InvalidSavedNumericValue_RejectsTheFieldAsync(
            string kind,
            string member,
            int value
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord record = Records(project).First(item => item.Kind == kind);
            RewriteField(project, record, member, value);
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains(record.GetField(member).Name, error.Message);
            StringAssert.Contains(record.ID, error.Message);
            StringAssert.Contains(record.Catalog.Document.Address, error.Message);
        }

        [TestCase("Building", "ConstructionCost", 0)]
        [TestCase("Building", "MaintenanceCost", 0)]
        [TestCase("Building", "ProcessRate", 0)]
        [TestCase("CapitalShip", "RegimentCapacity", 0)]
        [TestCase("CapitalShip", "StarfighterCapacity", 0)]
        [TestCase("Officer", "JediProbability", 0)]
        [TestCase("Officer", "JediProbability", 100)]
        [TestCase("Officer", "ForceValue", -32768)]
        [TestCase("Officer", "ForceValue", 32767)]
        [TestCase("Officer", "ForceTrainingAdjustment", -32768)]
        [TestCase("Officer", "ForceTrainingAdjustment", 32767)]
        public void SetField_NumericBoundary_PreservesTheAuthoredValue(
            string kind,
            string member,
            int value
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = Records(project)
                .First(item => item.Kind == kind)
                .GetField(member);

            project.SetField(
                field,
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

            Assert.AreEqual(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                field.Value
            );
        }

        [Test]
        public void SetField_NegativeProductionRate_RejectsWithoutChangingHistory()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorField field = Records(project)
                .First(item => item.ID == "BDFA01")
                .GetField("ProcessRate");
            string previous = field.Value;

            Assert.Throws<ArgumentException>(() => project.SetField(field, "-1"));

            Assert.AreEqual(previous, field.Value);
            Assert.IsFalse(project.IsDirty);
        }

        [TestCase("FNALL1", "missing", "FNALL1")]
        [TestCase("FNALL1", "DEFAULT", "default theme")]
        [TestCase("FNEMP1", "FNALL1", "FNALL1")]
        [TestCase("DEFAULT", "missing", "DEFAULT")]
        public async Task ValidateAsync_InvalidThemeKeys_RejectsBeforeOpeningTheUiAsync(
            string previous,
            string replacement,
            string expected
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord theme = Records(project)
                .First(record =>
                    record.Kind == "FactionTheme"
                    && record.Element.Element("FactionInstanceID")?.Value == previous
                );
            project.SetField(theme.GetField("FactionInstanceID"), replacement);
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains(expected, error.Message);
        }

        [TestCase("Energy", "energy")]
        [TestCase("RawMaterials", "raw-material")]
        public async Task ValidateAsync_InvertedResourceRange_RejectsTheProfileAsync(
            string member,
            string expected
        )
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorDocument document = project
                .GetCatalogs()
                .First(catalog => catalog.Name == "Starting setup")
                .Document;
            XDocument xml = XDocument.Parse(document.Text);
            xml.Descendants(member + "Min").First().Value = "11";
            xml.Descendants(member + "Max").First().Value = "10";
            project.SetDocumentText(document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            Exception error = await GetValidationErrorAsync(project);

            StringAssert.Contains(expected, error.Message);
        }

        [Test]
        public async Task ValidateAsync_ZeroProducerRate_ReturnsAWarningAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            project.SetField(
                Records(project).First(item => item.ID == "BDFA01").GetField("ProcessRate"),
                "0"
            );
            ContentPackEditorStorage.Save(project);

            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project
            );

            Assert.That(
                warnings,
                Has.Some.Contains("Building 'BDFA01' has a zero production rate")
            );
        }

        [Test]
        public async Task ValidateAsync_EmptyManufacturingFactions_ReturnsAWarningAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            project.SetFieldValues(
                Records(project)
                    .First(item => item.ID == "BDFA01")
                    .GetField("ManufacturingFactionInstanceIDs"),
                Array.Empty<string>()
            );
            ContentPackEditorStorage.Save(project);

            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project
            );

            Assert.That(
                warnings,
                Has.Some.Contains("Product 'BDFA01' has no allowed manufacturing factions")
            );
        }

        [Test]
        public async Task ValidateAsync_UnrestrictedManufacturing_DoesNotWarnAboutMissingFactionsAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord building = Records(project).First(item => item.ID == "BDFA01");
            XDocument xml = XDocument.Parse(building.Catalog.Document.Text);
            xml.Descendants("Building")
                .First(item => item.Element("TypeID")?.Value == building.ID)
                .Element("ManufacturingFactionInstanceIDs")
                .Remove();
            project.SetDocumentText(building.Catalog.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project
            );

            Assert.That(warnings, Has.None.Contains("Product 'BDFA01'"));
        }

        [Test]
        public async Task ValidateAsync_AllMissionsDisabled_ReturnsAWarningAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord faction = Records(project)
                .First(item =>
                    item.Kind == "Faction" && item.Element.Element("InstanceID")?.Value == "FNALL1"
                );
            project.SetFieldValues(
                faction.GetField("DisallowedMissionTypeIDs"),
                ContentPackEditorFieldSchema.MissionIDs
            );
            ContentPackEditorStorage.Save(project);

            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project
            );

            Assert.That(
                warnings,
                Has.Some.Contains("Faction 'FNALL1' disables every normal mission type")
            );
        }

        [Test]
        public async Task ValidateAsync_ZeroBookmarkRows_ReturnsAWarningAsync()
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorRecord theme = Records(project)
                .First(item =>
                    item.Kind == "FactionTheme"
                    && item.Element.Element("FactionInstanceID")?.Value == "FNALL1"
                );
            XDocument xml = XDocument.Parse(theme.Catalog.Document.Text);
            xml.Descendants("FactionTheme")
                .First()
                .Add(
                    new XElement(
                        "StrategyBookmarkLayout",
                        new XElement("ItemHeight", 0),
                        new XElement("ListHeight", 72)
                    )
                );
            project.SetDocumentText(theme.Catalog.Document, xml.ToString());
            ContentPackEditorStorage.Save(project);

            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                project
            );

            Assert.That(
                warnings,
                Has.Some.Contains("Faction theme 'FNALL1' has no usable bookmark rows")
            );
        }

        /// <summary>
        /// Enumerates records from the effective inherited project.
        /// </summary>
        /// <param name="project">The independent editable project.</param>
        /// <returns>The authored catalog records.</returns>
        private static IEnumerable<ContentPackEditorRecord> Records(
            ContentPackEditorProject project
        ) => project.GetCatalogs().SelectMany(catalog => catalog.Records);

        /// <summary>
        /// Writes a raw scalar to exercise validation of saved XML independently of form guards.
        /// </summary>
        /// <param name="project">The independent editable project.</param>
        /// <param name="record">The owning definition.</param>
        /// <param name="member">The serialized member to replace.</param>
        /// <param name="value">The authored integer.</param>
        private static void RewriteField(
            ContentPackEditorProject project,
            ContentPackEditorRecord record,
            string member,
            int value
        )
        {
            XDocument xml = XDocument.Parse(record.Catalog.Document.Text);
            xml.Root.DescendantsAndSelf(record.Element.Name)
                .First(element =>
                    (
                        element.Element("TypeID")?.Value
                        ?? element.Element("InstanceID")?.Value
                        ?? element.Name.LocalName
                    ) == record.ID
                )
                .SetElementValue(member, value);
            project.SetDocumentText(record.Catalog.Document, xml.ToString());
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
