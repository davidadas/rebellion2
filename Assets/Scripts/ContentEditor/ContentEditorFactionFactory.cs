using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Rebellion.Game.Factions;
using Rebellion.Util.Serialization;

/// <summary>
/// Creates the linked documents required to author an independent faction.
/// </summary>
internal static class ContentEditorFactionFactory
{
    /// <summary>
    /// Creates blank gameplay catalogs and a neutral starting theme that reuses existing assets.
    /// </summary>
    /// <param name="id">The new faction identity.</param>
    /// <param name="folder">The pack-relative faction directory.</param>
    /// <param name="neutralTheme">The pack's optional neutral theme.</param>
    /// <returns>The new documents keyed by pack-relative path.</returns>
    internal static IReadOnlyDictionary<string, XElement> Create(
        string id,
        string folder,
        XElement neutralTheme
    )
    {
        using StringWriter settings = new StringWriter();
        new GameSerializer(
            typeof(FactionSettings),
            new GameSerializerSettings { RootName = "Settings" }
        ).Serialize(settings, new FactionSettings { ResourceProcessingPointsPerFacility = 1 });
        var files = new Dictionary<string, XElement>();
        XElement manifest = new XElement(
            "Faction",
            new XElement("ID", id),
            new XElement("DisplayName", "New faction")
        );
        files.Add(folder + "/faction.xml", manifest);
        files.Add(
            folder + "/Data/faction.xml",
            new XElement(
                "Factions",
                new XElement(
                    "Faction",
                    new XElement("InstanceID", id),
                    new XElement("DisplayName", "New faction"),
                    new XElement("HQInstanceID"),
                    new XElement("GarrisonTroopTypeID"),
                    new XElement("DisallowedMissionTypeIDs"),
                    new XElement("ShipNamePools"),
                    new XElement("Messages"),
                    XElement.Parse(settings.ToString())
                )
            )
        );
        manifest.Add(new XElement("FactionDataPath", folder + "/Data/faction.xml"));
        foreach (
            var catalog in new (string Root, string FileName)[]
            {
                ("CapitalShips", "capital-ships"),
                ("Starfighters", "starfighters"),
                ("Regiments", "regiments"),
                ("SpecialForces", "special-forces"),
                ("Officers", "officers"),
                ("EncyclopediaEntries", "encyclopedia-entries"),
            }
        )
        {
            string path = folder + "/Data/" + catalog.FileName + ".xml";
            manifest.Add(new XElement(catalog.Root + "Path", path));
            files.Add(path, new XElement(catalog.Root));
        }
        XElement theme =
            neutralTheme == null ? new XElement("FactionTheme") : new XElement(neutralTheme);
        theme.SetElementValue("FactionInstanceID", id);
        string themePath = folder + "/Strategy/UI/theme.xml";
        manifest.Add(new XElement("ThemePath", themePath));
        files.Add(themePath, new XElement("FactionThemes", theme));
        return files;
    }
}
