using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorLocalProjectTests : ContentPackEditorTestBase
    {
        [TestCase(null)]
        [TestCase("<broken")]
        [TestCase(
            "<ContentPack xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />"
        )]
        public void Read_UnavailableManifest_RetainsProjectWithError(string xml)
        {
            using ContentPackEditorProject project = CreateLoadableProject();
            string manifest = Path.Combine(project.PackRootPath, "pack.xml");
            if (xml == null)
                File.Delete(manifest);
            else
                File.WriteAllText(manifest, xml);

            ContentEditorLocalProject local = ContentEditorLocalProject.Read(project.PackRootPath);

            Assert.That(local.Folder, Is.EqualTo(project.PackRootPath));
            Assert.That(local.Name, Is.EqualTo(Path.GetFileName(project.PackRootPath)));
            Assert.That(
                local.Error,
                Is.EqualTo(
                    xml == null ? "Project folder is unavailable" : "Couldn’t read this project"
                )
            );
            Assert.That(local.Version, Is.Empty);
            Assert.That(local.PackageID, Is.Null);
        }
    }
}
