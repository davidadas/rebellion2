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
    }
}
