using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorTravelRestorationTests : ContentPackEditorTestBase
    {
        [Test]
        public void Restore_StrippedWorkingCopy_RestoresTheExactImageAndAssignment()
        {
            WriteFixtureFile(
                "Data/units.xml",
                "<Units><Building><TypeID>BDFA01</TypeID><InTransitSmallImagePath>Pack/Artwork/travel-card</InTransitSmallImagePath></Building></Units>"
            );
            WriteFixtureFile("Artwork/travel-card.png", "registered travel art");
            ContentPackEditorProject project = CreateStandaloneProject(
                "<Units><Building><TypeID>BDFA01</TypeID></Building></Units>"
            );

            Assert.That(ContentEditorTravelRestoration.Restore(project), Is.EqualTo((1, 1)));
            ContentPackEditorStorage.Save(project);

            string output = Path.Combine(project.PackRootPath, "Artwork", "travel-card.png");
            string source = Path.Combine(
                contentRoot,
                "Packs",
                "BasePack",
                "Artwork",
                "travel-card.png"
            );
            CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(output));
            var saved = XDocument.Load(Path.Combine(project.PackRootPath, "Data", "units.xml"));
            Assert.That(
                saved.Descendants("InTransitSmallImagePath").Single().Value,
                Is.EqualTo("Pack/Artwork/travel-card")
            );
        }

        [Test]
        public void Restore_AssignedTravelArtwork_PreservesTheWorkingCopy()
        {
            const string document =
                "<Units><Building><TypeID>BDFA01</TypeID><InTransitSmallImagePath>Pack/Artwork/facility-in-transit</InTransitSmallImagePath></Building></Units>";
            WriteFixtureFile("Data/units.xml", document);
            WriteFixtureFile("Artwork/facility-in-transit.png", "registered travel art");
            ContentPackEditorProject project = CreateStandaloneProject(document);
            string target = Path.Combine(
                project.PackRootPath,
                "Artwork",
                "facility-in-transit.png"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, "working travel art");

            Assert.That(ContentEditorTravelRestoration.Restore(project), Is.EqualTo((0, 0)));
            Assert.That(File.ReadAllText(target), Is.EqualTo("working travel art"));
        }

        private ContentPackEditorProject CreateStandaloneProject(string units)
        {
            string folder = Path.Combine(Directory.GetParent(contentRoot).FullName, "WorkingPack");
            Directory.CreateDirectory(Path.Combine(folder, "Data"));
            File.Copy(
                Path.Combine(contentRoot, "Packs", "BasePack", "pack.xml"),
                Path.Combine(folder, "pack.xml")
            );
            File.WriteAllText(Path.Combine(folder, "Data", "units.xml"), units);
            return ContentPackEditorStorage.OpenFolder(contentRoot, folder);
        }
    }
}
