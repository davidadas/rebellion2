using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor
{
    /// <summary>
    /// Owns the isolated content installation shared by authoring integration fixtures.
    /// </summary>
    public abstract class ContentPackEditorTestBase
    {
        protected string contentRoot;

        /// <summary>
        /// Creates an isolated content root for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            contentRoot = Path.Combine(
                Path.GetTempPath(),
                "content-pack-editor-" + Guid.NewGuid().ToString("N"),
                "Content"
            );
            Directory.CreateDirectory(Path.Combine(contentRoot, "Packs", "BasePack", "Data"));
            WriteBasePack();
        }

        /// <summary>
        /// Removes the isolated content root after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(Directory.GetParent(contentRoot).FullName, true);
        }

        /// <summary>
        /// Captures an expected failure through the asynchronous authoring API.
        /// </summary>
        /// <param name="operation">The operation expected to fail.</param>
        /// <typeparam name="TException">The expected failure type.</typeparam>
        /// <returns>The captured failure.</returns>
        protected static async Task<TException> ExpectFailureAsync<TException>(Func<Task> operation)
            where TException : Exception
        {
            try
            {
                await operation();
            }
            catch (TException exception)
            {
                return exception;
            }
            Assert.Fail("The invalid authoring operation must fail.");
            return null;
        }

        /// <summary>
        /// Creates a derived pack without changing its inheritance source.
        /// </summary>
        /// <returns>The working project.</returns>
        protected ContentPackEditorProject CreateLoadableProject()
        {
            string liveContentRoot = ContentPackLoader.ResolveContentRootPath();
            ContentPackDefinition source = ContentPackLoader.DiscoverPacks(liveContentRoot).First();
            return ContentPackEditorStorage.CreateInFolder(
                liveContentRoot,
                source.ID,
                Path.Combine(Directory.GetParent(contentRoot).FullName, "LiveProject"),
                "export-test-" + Guid.NewGuid().ToString("N"),
                "Export test",
                "1.0.0"
            );
        }

        /// <summary>
        /// Creates an inherited working project over the isolated fixture catalogs.
        /// </summary>
        /// <returns>The editable project.</returns>
        protected ContentPackEditorProject CreateWorkingProject()
        {
            return ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
        }

        /// <summary>
        /// Writes an isolated inherited fixture document.
        /// </summary>
        /// <param name="relative">The pack-relative filename.</param>
        /// <param name="text">The fixture contents.</param>
        protected void WriteFixtureFile(string relative, string text)
        {
            string path = Path.Combine(contentRoot, "Packs", "BasePack", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        /// <summary>
        /// Writes the minimal installed inheritance source for each test.
        /// </summary>
        private void WriteBasePack()
        {
            string packRoot = Path.Combine(contentRoot, "Packs", "BasePack");
            ContentDefinitionSerializer.Serialize(
                Path.Combine(packRoot, "pack.xml"),
                new ContentPackDefinition
                {
                    ID = "base-pack",
                    Version = "1.0.0",
                    DisplayName = "Base pack",
                    DefaultScenarioID = "standard",
                    MainMenuMusicPath = "Application/Audio/menu",
                    PlanetSectorsPath = "Data/units.xml",
                    BuildingsPath = "Data/units.xml",
                    GameEventsPath = "Data/units.xml",
                    MessageDefinitionsPath = "Data/units.xml",
                    EncyclopediaEntriesPath = "Data/units.xml",
                    NeutralThemePath = "Data/units.xml",
                }
            );
            File.WriteAllText(Path.Combine(packRoot, "Data", "units.xml"), "<Units />");
        }
    }
}
