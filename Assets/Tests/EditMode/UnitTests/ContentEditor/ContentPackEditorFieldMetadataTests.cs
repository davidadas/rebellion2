using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentPackEditorFieldMetadataTests
    {
        [Test]
        public void GetDescription_StartingSetupFields_ReturnsGuidanceForEveryAuthoredValue()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Tests/EditMode/UnitTests/ContentEditor/Fixtures/EditorPack/Scenarios/Standard/generation.xml"
            );
            ContentPackEditorRecord record = new ContentPackEditorCatalog(
                new ContentPackEditorDocument(
                    "Pack/Scenarios/Standard/generation.xml",
                    path,
                    path,
                    File.ReadAllText(path)
                )
            ).Records.Single();

            string[] undocumented = record
                .GetFields()
                .Where(field => string.IsNullOrWhiteSpace(field.HelpText))
                .Select(field => field.Name)
                .Distinct()
                .ToArray();

            Assert.IsEmpty(undocumented, string.Join(", ", undocumented));
        }

        [Test]
        [Category("RequiresMedia")]
        public void GetExpectedImageDimensions_UnitArtwork_ReturnsEstablishedSizes()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                "editor-artwork-sizes-" + Guid.NewGuid().ToString("N")
            );
            try
            {
                ContentPackEditorProject project = ContentEditorTestData.CreateProject(folder);
                (string Kind, string Member, string Expected)[] cases =
                {
                    ("Officer", "BackgroundImagePath", "1800 × 900"),
                    ("Officer", "DisplayImagePath", "360 × 360"),
                    ("Officer", "SmallDisplayImagePath", "275 × 113"),
                    ("SpecialForce", "DisplayImagePath", "549 × 225"),
                    ("Regiment", "DisplayImagePath", "1373 × 563"),
                    ("Building", "DisplayImagePath", "1373 × 563"),
                    ("CapitalShip", "DisplayImagePath", "1485 × 563"),
                    ("CapitalShip", "BackgroundImagePath", "1800 × 900"),
                    ("CapitalShip", "DamagedImagePath", "549 × 225"),
                    ("Starfighter", "InTransitSmallImagePath", "297 × 113"),
                    ("Starfighter", "BackgroundImagePath", "1800 × 900"),
                    ("SpecialForce", "InTransitImagePath", "549 × 225"),
                    ("Regiment", "SmallDisplayImagePath", "275 × 113"),
                };
                foreach (var item in cases)
                {
                    ContentPackEditorRecord record = project
                        .GetCatalogs()
                        .SelectMany(catalog => catalog.Records)
                        .First(candidate =>
                            candidate.Kind == item.Kind
                            && (
                                item.Kind != "Building"
                                || candidate.Element.Element("BuildingType")?.Value
                                    != "Headquarters"
                            )
                        );
                    Assert.AreEqual(
                        item.Expected,
                        ContentPackEditorFieldMetadata.GetExpectedImageDimensions(
                            record.GetField(item.Member)
                        ),
                        item.Kind + "." + item.Member
                    );
                }
                ContentPackEditorRecord headquarters = project
                    .GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Single(candidate =>
                        ContentEditorUnitArtworkLayout.IsHeadquartersIcon(candidate)
                    );
                Assert.AreEqual(
                    "77 × 77",
                    ContentPackEditorFieldMetadata.GetExpectedImageDimensions(
                        headquarters.GetField("DisplayImagePath")
                    )
                );
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
