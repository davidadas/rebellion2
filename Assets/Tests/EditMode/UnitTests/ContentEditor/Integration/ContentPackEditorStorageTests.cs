using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.ContentEditor.Integration
{
    [TestFixture]
    [Category("Integration")]
    public sealed class ContentPackEditorStorageTests : ContentPackEditorTestBase
    {
        [TestCase("Officers")]
        [TestCase("Starfighters")]
        [TestCase("Capital ships")]
        [TestCase("Troops")]
        [TestCase("Special forces")]
        [TestCase("Buildings")]
        public void Save_AddedUnit_LoadsInTheRuntimeCatalog(string category)
        {
            ContentPackEditorProject project = CreateLoadableProject();
            ContentPackEditorCatalog catalog = project
                .GetCatalogs()
                .First(item => item.Name == category);
            ContentPackEditorRecord unit = project.AddUnit(catalog);

            string member =
                category == "Officers"
                    ? "RecruitingFactionInstanceIDs"
                    : "ManufacturingFactionInstanceIDs";
            string faction = project.GetFactionOptions().Last().Key;
            project.SetFieldValues(unit.GetField(member), new[] { faction });

            ContentPackEditorStorage.Save(project);

            ContentFileResolver source = ContentPackLoader.CreatePackResolver(
                project.ContentRootPath,
                project.Definition.BasePackID
            );
            ContentFileResolver resolver = new ContentFileResolver(
                project.ContentRootPath,
                project.PackRootPath,
                fallbackPackRootPaths: new[] { source.PackRootPath }
            );
            ContentPack loaded = ContentPackLoader.LoadPack(
                resolver,
                project.Definition,
                project.Definition.DefaultScenarioID
            );
            var definitions = loaded
                .GameData.Buildings.Cast<ISceneNode>()
                .Concat(loaded.GameData.CapitalShips)
                .Concat(loaded.GameData.Starfighters)
                .Concat(loaded.GameData.Regiments)
                .Concat(loaded.GameData.SpecialForces)
                .Concat(loaded.GameData.Officers);
            ISceneNode created = definitions.Single(definition =>
                definition.TypeID == unit.ID && definition.DisplayName == unit.Name
            );
            CollectionAssert.AreEqual(
                new[] { faction },
                created is Rebellion.Game.Units.Officer officer
                    ? officer.RecruitingFactionInstanceIDs
                    : (
                        (Rebellion.Game.Units.IManufacturable)created
                    ).ManufacturingFactionInstanceIDs
            );
        }
    }
}
