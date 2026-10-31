using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorPresentationTests : ContentPackEditorTestBase
    {
        [TestCase("Strategy/UI/theme.xml")]
        [TestCase("Strategy\\UI\\theme.xml")]
        public void GetNeutralTheme_InheritedThemePath_ResolvesBothSeparators(string path)
        {
            WriteFixtureFile(
                "Strategy/UI/theme.xml",
                "<FactionThemes><FactionTheme><FactionInstanceID>neutral</FactionInstanceID></FactionTheme></FactionThemes>"
            );
            ContentPackEditorProject project = CreateWorkingProject();
            project.SetField(
                project
                    .GetCatalogs()
                    .Single(catalog => catalog.Document.Address == "Pack/pack.xml")
                    .DocumentRecord.GetField("NeutralThemePath"),
                path
            );

            Assert.AreEqual(
                "neutral",
                ContentEditorPresentation.GetNeutralTheme(project).FactionInstanceID
            );
        }

        [TestCase("Strategy/UI/theme.xml")]
        [TestCase("Strategy\\UI\\theme.xml")]
        public void GetFactionThemes_InheritedThemePath_ResolvesBothSeparators(string path)
        {
            WriteFixtureFile(
                "Strategy/UI/theme.xml",
                "<FactionThemes><FactionTheme><FactionInstanceID>sample</FactionInstanceID></FactionTheme></FactionThemes>"
            );
            WriteFixtureFile(
                "Factions/Sample/faction.xml",
                "<Faction><ID>sample</ID><DisplayName>Sample</DisplayName><ThemePath>"
                    + path
                    + "</ThemePath></Faction>"
            );
            string manifest = Path.Combine(contentRoot, "Packs", "BasePack", "pack.xml");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifest);
            definition.FactionPaths.Add("Factions/Sample/faction.xml");
            ContentDefinitionSerializer.Serialize(manifest, definition);
            ContentPackEditorProject project = CreateWorkingProject();

            Assert.AreEqual(
                "sample",
                ContentEditorPresentation.GetFactionThemes(project).Single().Theme.FactionInstanceID
            );
        }
    }
}
