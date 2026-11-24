using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Rebellion.Game;
using UnityEngine;

internal static class TestPackFixture
{
    internal static string SourceRoot =>
        Path.Combine(
            Application.dataPath,
            "Tests/EditMode/UnitTests/ContentEditor/Fixtures/EditorPack"
        );

    /// <summary>
    /// Loads a fresh generation configuration from the fixture pack.
    /// </summary>
    /// <returns>The test-owned generation configuration.</returns>
    internal static GameGenerationConfig CreateGenerationConfig()
    {
        return SerializationHelper.Deserialize<GameGenerationConfig>(
            File.ReadAllText(Path.Combine(SourceRoot, "Scenarios", "Standard", "generation.xml"))
        );
    }

    /// <summary>
    /// Installs the fixture pack and opens its configured scenario.
    /// </summary>
    /// <param name="contentRoot">The isolated content installation directory.</param>
    /// <returns>The loaded fixture pack.</returns>
    internal static ContentPack Install(string contentRoot)
    {
        const string packID = "editor-fixture";
        const string scenarioID = "standard";
        string fixtures = SourceRoot;
        string packRoot = Path.Combine(contentRoot, "Packs", packID);
        foreach (
            string source in Directory.EnumerateFiles(
                fixtures,
                "*.xml",
                SearchOption.AllDirectories
            )
        )
        {
            string target = Path.Combine(packRoot, Path.GetRelativePath(fixtures, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target);
        }

        WriteApplicationFiles(contentRoot);
        ContentDefinitionSerializer.Serialize(
            Path.Combine(contentRoot, "catalog.xml"),
            new ContentCatalogDefinition { ActivePackID = packID, ActiveScenarioID = scenarioID }
        );
        return ContentPackLoader.OpenExact(contentRoot, packID, scenarioID, null);
    }

    /// <summary>
    /// Writes the application rules, preload lists, and schemas required by the fixture.
    /// </summary>
    /// <param name="contentRoot">The isolated content installation directory.</param>
    internal static void WriteApplicationFiles(string contentRoot)
    {
        string application = Path.Combine(contentRoot, "Application");
        string rules = Path.Combine(application, "Rules");
        Directory.CreateDirectory(rules);
        GameConfig config = TestConfig.Create();
        config.AI.Garrison.ResourceFacilitiesPerGarrison = 2;
        File.WriteAllText(Path.Combine(rules, "game.xml"), SerializationHelper.Serialize(config));

        string preloads = Path.Combine(application, "Preload");
        Directory.CreateDirectory(preloads);
        foreach (string id in new[] { "main-menu", "strategy" })
        {
            ContentDefinitionSerializer.Serialize(
                Path.Combine(preloads, id + ".xml"),
                new ContentPreloadManifest { TexturesPerFrame = 1 }
            );
        }

        // Runtime fixtures use the typed loader. Schema validation has its own cases.
        string schemas = Path.Combine(application, "Schemas");
        Directory.CreateDirectory(schemas);
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        foreach (
            (string file, string element) in new Dictionary<string, string>
            {
                ["game-config.xsd"] = "GameConfig",
                ["generation-config.xsd"] = "GameGenerationConfig",
                ["game-events.xsd"] = "GameEvents",
                ["message-definitions.xsd"] = "MessageDefinitions",
            }
        )
        {
            new XElement(
                xs + "schema",
                new XAttribute(XNamespace.Xmlns + "xs", xs.NamespaceName),
                new XElement(
                    xs + "element",
                    new XAttribute("name", element),
                    new XAttribute("type", "xs:anyType")
                )
            ).Save(Path.Combine(schemas, file));
        }
    }
}
