using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    internal static class ContentEditorTestData
    {
        /// <summary>
        /// Installs a small campaign with fixture audio for export validation.
        /// </summary>
        /// <param name="contentRoot">The isolated content installation.</param>
        /// <returns>A derived project over the small installed pack.</returns>
        internal static ContentPackEditorProject CreateLoadableProject(string contentRoot)
        {
            string fixtures = Path.Combine(
                Application.dataPath,
                "Tests/EditMode/UnitTests/ContentEditor/Fixtures/EditorPack"
            );
            string basePack = Path.Combine(contentRoot, "Packs", "BasePack");
            foreach (
                string source in Directory.EnumerateFiles(
                    fixtures,
                    "*.xml",
                    SearchOption.AllDirectories
                )
            )
            {
                string target = Path.Combine(basePack, Path.GetRelativePath(fixtures, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                XDocument xml = XDocument.Load(source);
                foreach (XElement playable in xml.Descendants("PlayableFactionIDs"))
                    playable
                        .Elements()
                        .Where(element =>
                            element.Value != xml.Root.Element("DefaultPlayerFactionID").Value
                        )
                        .Remove();
                if (xml.Root.Name == "GameEvents")
                    xml.Root.RemoveNodes();
                if (xml.Root.Name == "MessageDefinitions")
                    xml.Root.ReplaceNodes(
                        XElement.Parse(
                            "<MessageDefinition><ResultType>FleetArrived</ResultType>"
                                + "<Subject>Fleet arrived</Subject><Body>The fleet arrived.</Body></MessageDefinition>"
                        )
                    );
                if (Path.GetFileName(source) != "pack.xml")
                    foreach (
                        XElement media in xml.Descendants()
                            .Where(element =>
                                !element.HasElements
                                && (
                                    element.Value.StartsWith("Pack/", StringComparison.Ordinal)
                                    || element.Value.StartsWith(
                                        "Application/",
                                        StringComparison.Ordinal
                                    )
                                )
                            )
                            .ToArray()
                    )
                        media.Value = string.Empty;
                xml.Save(target);
            }
            string application = Path.Combine(contentRoot, "Application");
            TestPackFixture.WriteApplicationFiles(contentRoot);
            string manifest = Path.Combine(basePack, "pack.xml");
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifest);
            definition.ID = "base-pack";
            definition.MainMenuMusicPath = "Application/Audio/tone";
            ContentDefinitionSerializer.Serialize(manifest, definition);
            string audio = Path.Combine(application, "Audio");
            Directory.CreateDirectory(audio);
            File.Copy(
                Path.Combine(
                    Application.dataPath,
                    "Tests/EditMode/UnitTests/Content/AudioFixtures/tone.wav"
                ),
                Path.Combine(audio, "tone.wav")
            );
            return ContentPackEditorStorage.Create(
                contentRoot,
                "base-pack",
                "sample-pack",
                "Sample pack",
                "1.0.0"
            );
        }

        /// <summary>
        /// Creates independent editable records over read-only fixture documents.
        /// </summary>
        /// <param name="destination">The fixture-owned working directory.</param>
        /// <param name="basePack">The installed base for cases exercising inherited pack assets.</param>
        /// <returns>The editable project.</returns>
        internal static ContentPackEditorProject CreateProject(
            string destination,
            ContentPack basePack = null
        )
        {
            string fixtures = Path.Combine(
                Application.dataPath,
                "Tests/EditMode/UnitTests/ContentEditor/Fixtures/EditorPack"
            );
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(fixtures, "pack.xml")
                );
            definition.BasePackID = basePack?.Definition.ID ?? definition.ID;
            definition.ID = "editor-test";
            Directory.CreateDirectory(destination);
            string manifest = Path.Combine(destination, "pack.xml");
            ContentDefinitionSerializer.Serialize(manifest, definition);
            ContentFileResolver resolver = new ContentFileResolver(
                basePack?.ContentRootPath ?? Path.Combine(destination, "Content"),
                destination,
                fallbackPackRootPaths: basePack == null
                    ? new[] { fixtures }
                    : new[] { fixtures, basePack.PackRootPath }
            );
            var documents = Directory
                .EnumerateFiles(fixtures, "*.xml", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "pack.xml")
                .Select(path =>
                {
                    string relative = Path.GetRelativePath(fixtures, path);
                    return new ContentPackEditorDocument(
                        "Pack/" + relative.Replace('\\', '/'),
                        path,
                        Path.Combine(destination, relative),
                        File.ReadAllText(path)
                    );
                })
                .Append(
                    new ContentPackEditorDocument(
                        "Pack/pack.xml",
                        manifest,
                        manifest,
                        File.ReadAllText(manifest)
                    )
                );
            return new ContentPackEditorProject(resolver, definition, documents);
        }
    }
}
