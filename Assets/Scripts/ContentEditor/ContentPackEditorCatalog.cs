using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;

/// <summary>
/// Presents the records in one authored catalog without discarding unedited XML.
/// </summary>
public sealed class ContentPackEditorCatalog
{
    private readonly XDocument xml;
    private readonly string recordName;

    public ContentPackEditorDocument Document { get; }
    public string Name { get; }

    /// <summary>
    /// Gets the singular record name shared by creation menus and list actions.
    /// </summary>
    public string ItemName =>
        recordName switch
        {
            "SpecialForce" => "Special forces unit",
            "Trait" => "Officer trait",
            _ => recordName == null ? null : ContentPackEditorField.FormatName(recordName),
        };

    public string Faction { get; }
    public bool IsMap => recordName == "PlanetSector";
    public bool CanAddUnit => ContentEditorUnitFactory.CanCreate(recordName);
    public bool CanAddEvent => recordName == "GameEvent";
    public bool CanAddTrait => recordName == "Trait";
    public bool CanAddFaction => xml.Root.Name.LocalName == "ContentPack";
    public bool CanDuplicate =>
        recordName != null
        && recordName != "Faction"
        && Records.Any(record => GetIdentityValues(record.Element).Any());
    public IReadOnlyList<ContentPackEditorRecord> Records { get; private set; }
    public ContentPackEditorRecord DocumentRecord => new ContentPackEditorRecord(this, xml.Root);

    /// <summary>
    /// Opens a document as an editable catalog of named records.
    /// </summary>
    /// <param name="document">The working document.</param>
    public ContentPackEditorCatalog(ContentPackEditorDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        xml = XDocument.Parse(document.Text);
        recordName = GetRecordName(xml.Root.Name.LocalName);
        Name = GetName(xml.Root.Name.LocalName);
        string[] parts = document.Address.Split('/');
        Faction = parts.Length > 3 && parts[1] == "Factions" ? parts[2] : "Shared";
        RefreshRecords();
    }

    /// <summary>
    /// Creates a record from a template, assigning fresh identities to its owned map children too.
    /// </summary>
    /// <param name="source">The record to copy.</param>
    /// <param name="reservedIDs">Optional identities already used elsewhere in the working project.</param>
    /// <returns>The new editable record.</returns>
    public ContentPackEditorRecord Duplicate(
        ContentPackEditorRecord source,
        ISet<string> reservedIDs = null
    )
    {
        RequireAttachedRecord(source);
        if (!GetIdentityValues(source.Element).Any())
            throw new InvalidOperationException(
                "This entry has no independent identity to duplicate."
            );
        XElement clone = new XElement(source.Element);
        HashSet<string> used = new HashSet<string>(
            reservedIDs ?? new HashSet<string>(),
            StringComparer.Ordinal
        );
        foreach (XElement record in xml.Root.Elements())
        {
            foreach (XElement owned in GetOwnedRecords(record))
                used.UnionWith(GetIdentityValues(owned));
        }
        foreach (XElement record in GetOwnedRecords(clone))
        {
            foreach (
                XElement id in record
                    .Elements()
                    .Where(element => IsIdentityName(element.Name.LocalName))
            )
                id.Value = AllocateIdentity(id.Value + "_COPY", used);
            foreach (
                XAttribute id in record
                    .Attributes()
                    .Where(attribute => IsIdentityName(attribute.Name.LocalName))
            )
                id.Value = AllocateIdentity(id.Value + "_COPY", used);
            XElement name = record.Element("DisplayName") ?? record.Element("Name");
            if (name != null)
                name.Value += " copy";
        }
        source.Element.Parent.Add(clone);
        Commit();
        RefreshRecords();
        return Records.FirstOrDefault(record => record.Element == clone)
            ?? new ContentPackEditorRecord(this, clone);
    }

    /// <summary>
    /// Adds an empty unit definition with independently allocated identities.
    /// </summary>
    /// <param name="reservedIDs">The identities already present in the project.</param>
    /// <returns>The newly attached unit.</returns>
    internal ContentPackEditorRecord AddUnit(ISet<string> reservedIDs)
    {
        if (!CanAddUnit)
            throw new InvalidOperationException("Choose a unit category.");
        HashSet<string> used = new HashSet<string>(reservedIDs, StringComparer.Ordinal);
        XElement unit = ContentEditorUnitFactory.Create(recordName);
        unit.AddFirst(
            new XElement("TypeID", AllocateIdentity(recordName.ToUpperInvariant(), used))
        );
        if (recordName == "Officer")
            unit.Element("TypeID")
                .AddAfterSelf(new XElement("InstanceID", AllocateIdentity("NEW_OFFICER", used)));
        xml.Root.Add(unit);
        Commit();
        RefreshRecords();
        return Records.Single(record => record.Element == unit);
    }

    /// <summary>
    /// Adds an empty sector with an independent identity and an explicit map position.
    /// </summary>
    /// <param name="reservedIDs">The identities already used in the project.</param>
    /// <param name="x">The horizontal map coordinate.</param>
    /// <param name="y">The vertical map coordinate.</param>
    /// <returns>The attached sector definition.</returns>
    internal ContentPackEditorRecord AddSector(ISet<string> reservedIDs, int x, int y)
    {
        if (!IsMap)
            throw new ArgumentException("Choose the galaxy catalog.");
        XElement sector = new(
            "PlanetSector",
            new XElement(
                "TypeID",
                AllocateIdentity("SECTOR", new HashSet<string>(reservedIDs, StringComparer.Ordinal))
            ),
            new XElement("DisplayName", "New sector"),
            new XElement("SectorType", PlanetSectorType.OuterRim),
            new XElement("Visibility", GameSize.Small),
            new XElement("Importance", PlanetSectorImportance.Low),
            new XElement("PositionX", x),
            new XElement("PositionY", y),
            new XElement("Planets")
        );
        xml.Root.Add(sector);
        Commit();
        RefreshRecords();
        return Records.Single(record => record.Element == sector);
    }

    /// <summary>
    /// Adds a blank planet at an explicit position without copying another planet's definition.
    /// </summary>
    /// <param name="sector">The attached sector receiving the planet.</param>
    /// <param name="reservedIDs">The identities already used by the project.</param>
    /// <param name="x">The absolute horizontal coordinate.</param>
    /// <param name="y">The absolute vertical coordinate.</param>
    /// <returns>The attached planet definition.</returns>
    internal ContentPackEditorRecord AddPlanet(
        ContentPackEditorRecord sector,
        ISet<string> reservedIDs,
        int x,
        int y
    )
    {
        RequireAttachedRecord(sector);
        if (!IsMap || sector.Kind != "PlanetSector")
            throw new ArgumentException("Choose a sector first.", nameof(sector));
        HashSet<string> used = new(reservedIDs, StringComparer.Ordinal);
        XElement planet = new(
            "Planet",
            new XElement("TypeID", AllocateIdentity("PLANET", used)),
            new XElement("InstanceID", AllocateIdentity("NEW_PLANET", used)),
            new XElement("DisplayName", "New planet"),
            new XElement("Description"),
            new XElement("EncyclopediaDescription"),
            new XElement("IsColonized", false),
            new XElement("PositionX", x),
            new XElement("PositionY", y),
            new XElement("PlanetIconPath"),
            new XElement("EncyclopediaImagePath")
        );
        XElement planets = sector.Element.Element("Planets");
        if (planets == null)
        {
            planets = new XElement("Planets");
            sector.Element.Add(planets);
        }
        planets.Add(planet);
        Commit();
        RefreshRecords();
        return new ContentPackEditorRecord(this, planet);
    }

    /// <summary>
    /// Removes a named entry, including its owned planets when removing a sector.
    /// </summary>
    /// <param name="record">The record to remove.</param>
    public void Remove(ContentPackEditorRecord record)
    {
        RequireAttachedRecord(record);
        record.Element.Remove();
        Commit();
        RefreshRecords();
    }

    /// <summary>
    /// Adds an inactive-by-effect event draft with its own independent identity.
    /// </summary>
    /// <param name="reservedIDs">The identities already used by the project.</param>
    /// <returns>The new event definition.</returns>
    internal ContentPackEditorRecord AddEvent(ISet<string> reservedIDs)
    {
        if (!CanAddEvent)
            throw new InvalidOperationException("Choose the game events catalog.");
        XElement entry = new XElement(
            "GameEvent",
            new XAttribute("MaximumActivations", 1),
            new XElement(
                "InstanceID",
                AllocateIdentity(
                    "NEW_EVENT",
                    new HashSet<string>(reservedIDs, StringComparer.Ordinal)
                )
            ),
            new XElement("Schedule", new XElement("Every", new XAttribute("Ticks", 1))),
            new XElement("Actions")
        );
        xml.Root.Add(entry);
        Commit();
        RefreshRecords();
        return Records.Single(record => record.Element == entry);
    }

    /// <summary>
    /// Adds an independent trait with neutral modifiers and a unique identity.
    /// </summary>
    /// <param name="reservedIDs">The existing authored identities.</param>
    /// <returns>The attached trait definition.</returns>
    internal ContentPackEditorRecord AddTrait(ISet<string> reservedIDs)
    {
        if (!CanAddTrait)
            throw new InvalidOperationException("Choose the officer traits catalog.");
        XElement trait = new XElement(
            "Trait",
            new XAttribute(
                "ID",
                AllocateIdentity(
                    "new-trait",
                    new HashSet<string>(reservedIDs, StringComparer.Ordinal)
                )
            ),
            new XElement("PersonnelTravelTimePercent", 100),
            new XElement("SharesPersonnelTravelTimeModifier", false),
            new XElement("LeadershipBonusPercent", 0)
        );
        xml.Root.Add(trait);
        Commit();
        RefreshRecords();
        return Records.Single(record => record.Element == trait);
    }

    /// <summary>
    /// Replaces one validated record while preserving its siblings and document ownership.
    /// </summary>
    /// <param name="source">The attached record to replace.</param>
    /// <param name="replacement">The already validated replacement definition.</param>
    /// <returns>The attached replacement record.</returns>
    internal ContentPackEditorRecord Replace(ContentPackEditorRecord source, XElement replacement)
    {
        RequireAttachedRecord(source);
        if (XNode.DeepEquals(source.Element, replacement))
            return source;
        XElement value = new XElement(replacement);
        source.Element.ReplaceWith(value);
        Commit();
        RefreshRecords();
        return Records.Single(record => record.Element == value);
    }

    /// <summary>
    /// Verifies that a record is still attached to this collection or one of its sectors.
    /// </summary>
    /// <param name="record">The record to verify.</param>
    private void RequireAttachedRecord(ContentPackEditorRecord record)
    {
        if (
            record == null
            || record.Catalog != this
            || recordName == null
            || record.Element.Document != xml
            || record.Element.Name.LocalName != recordName && !(IsMap && record.Kind == "Planet")
        )
            throw new ArgumentException(
                "Select an attached record in this catalog first.",
                nameof(record)
            );
    }

    /// <summary>
    /// Enumerates a sector's owned planets without treating payload elements as independent records.
    /// </summary>
    /// <param name="record">The root record.</param>
    /// <returns>The root and its owned map entries.</returns>
    private static IEnumerable<XElement> GetOwnedRecords(XElement record)
    {
        return new[] { record }.Concat(
            record.Name.LocalName == "PlanetSector"
                ? record.Element("Planets")?.Elements("Planet") ?? Enumerable.Empty<XElement>()
                : Enumerable.Empty<XElement>()
        );
    }

    /// <summary>
    /// Gets the primary identities declared directly on a named entry.
    /// </summary>
    /// <param name="record">The entry to inspect.</param>
    /// <returns>Its element and attribute identities.</returns>
    internal static IEnumerable<string> GetIdentityValues(XElement record)
    {
        return record
            .Elements()
            .Where(element => IsIdentityName(element.Name.LocalName))
            .Select(element => element.Value)
            .Concat(
                record
                    .Attributes()
                    .Where(attribute => IsIdentityName(attribute.Name.LocalName))
                    .Select(attribute => attribute.Value)
            );
    }

    /// <summary>
    /// Recognizes identity members without rewriting identifiers inside an entry's behavior.
    /// </summary>
    /// <param name="name">The serialized member name.</param>
    /// <returns>True for primary record identity members.</returns>
    internal static bool IsIdentityName(string name)
    {
        return name is "TypeID" or "InstanceID" or "ID";
    }

    /// <summary>
    /// Allocates an unused identity for one copied record.
    /// </summary>
    /// <param name="source">The template identity.</param>
    /// <param name="used">The identities already reserved.</param>
    /// <returns>The newly reserved identity.</returns>
    private static string AllocateIdentity(string source, HashSet<string> used)
    {
        string prefix = source;
        string value = prefix;
        int suffix = 2;
        while (!used.Add(value))
            value = prefix + suffix++;
        return value;
    }

    /// <summary>
    /// Moves a map record, keeping a sector's planets at their existing relative offsets.
    /// </summary>
    /// <param name="record">The sector or planet to move.</param>
    /// <param name="x">The new horizontal world coordinate.</param>
    /// <param name="y">The new vertical world coordinate.</param>
    public void Move(ContentPackEditorRecord record, int x, int y)
    {
        if (!IsMap || record == null || record.Catalog != this)
            throw new InvalidOperationException("Select a map item first.");
        int dx = x - record.X;
        int dy = y - record.Y;
        if (record.Element.Name.LocalName == "PlanetSector")
        {
            foreach (
                XElement planet in record.Element.Element("Planets")?.Elements("Planet")
                    ?? Enumerable.Empty<XElement>()
            )
            {
                planet.SetElementValue("PositionX", ReadCoordinate(planet, "PositionX") + dx);
                planet.SetElementValue("PositionY", ReadCoordinate(planet, "PositionY") + dy);
            }
        }
        record.Element.SetElementValue("PositionX", x);
        record.Element.SetElementValue("PositionY", y);
        Commit();
    }

    /// <summary>
    /// Appends a blank starting message to a faction's existing message category.
    /// </summary>
    /// <param name="faction">The faction receiving the message.</param>
    /// <param name="type">The starting message category.</param>
    /// <returns>The new editable title.</returns>
    internal ContentPackEditorField AddFactionMessage(
        ContentPackEditorRecord faction,
        MessageType type
    )
    {
        RequireAttachedRecord(faction);
        if (faction.Kind != "Faction" || faction.Element.Element("InstanceID") == null)
            throw new ArgumentException("Choose a faction.", nameof(faction));
        XElement messages = faction.Element.Element("Messages");
        if (messages == null)
        {
            messages = new XElement("Messages");
            faction.Element.Add(messages);
        }
        XElement entry = messages
            .Elements("Entry")
            .FirstOrDefault(item => item.Element("Key")?.Value == type.ToString());
        if (entry == null)
        {
            entry = new XElement(
                "Entry",
                new XElement("Key", type),
                new XElement("Value", new XElement("ArrayOfMessage"))
            );
            messages.Add(entry);
        }
        XElement list =
            entry.Element("Value")?.Element("ArrayOfMessage")
            ?? throw new InvalidOperationException(
                "The message list is invalid. Check Advanced XML."
            );
        XElement message = new XElement(
            "Message",
            new XElement("Type", type),
            new XElement("Title", "New message"),
            new XElement("Body"),
            new XElement("BackgroundImageKey"),
            new XElement("OverlayImagePath"),
            new XElement("BackgroundAudioPath"),
            new XElement("Read", false)
        );
        list.Add(message);
        Commit();
        return faction.GetFields().Single(field => field.XmlElement == message.Element("Title"));
    }

    /// <summary>
    /// Copies the working tree back into the document's unsaved state.
    /// </summary>
    internal void Commit()
    {
        Document.SetText(xml.ToString());
    }

    /// <summary>
    /// Rebuilds stable record wrappers after adding or removing an entry.
    /// </summary>
    private void RefreshRecords()
    {
        IEnumerable<XElement> entries =
            recordName == null ? new[] { xml.Root } : xml.Root.Elements(recordName);
        Records = entries.Select(element => new ContentPackEditorRecord(this, element)).ToArray();
    }

    /// <summary>
    /// Reads a map coordinate using the content format's invariant integers.
    /// </summary>
    /// <param name="element">The map record.</param>
    /// <param name="name">The coordinate member.</param>
    /// <returns>The authored coordinate.</returns>
    internal static int ReadCoordinate(XElement element, string name)
    {
        return int.TryParse(
            element.Element(name)?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value
        )
            ? value
            : 0;
    }

    /// <summary>
    /// Maps supported data roots to their authored entry names.
    /// </summary>
    /// <param name="root">The root element name.</param>
    /// <returns>The collection entry name, or null for a single definition.</returns>
    private static string GetRecordName(string root)
    {
        return root switch
        {
            "Officers" => "Officer",
            "SpecialForces" => "SpecialForce",
            "CapitalShips" => "CapitalShip",
            "Starfighters" => "Starfighter",
            "Regiments" => "Regiment",
            "Buildings" => "Building",
            "PlanetSectors" => "PlanetSector",
            "GameEvents" => "GameEvent",
            "OfficerTraits" => "Trait",
            "MessageDefinitions" => "MessageDefinition",
            "Factions" => "Faction",
            "EncyclopediaEntries" => "EncyclopediaEntry",
            "FactionThemes" => "FactionTheme",
            _ => null,
        };
    }

    /// <summary>
    /// Resolves the user-facing editor name for a data root.
    /// </summary>
    /// <param name="root">The root element name.</param>
    /// <returns>The category caption.</returns>
    private static string GetName(string root)
    {
        return root switch
        {
            "PlanetSectors" => "Galaxy map",
            "SpecialForces" => "Special forces",
            "CapitalShips" => "Capital ships",
            "Regiments" => "Troops",
            "ContentPack" => "Pack settings",
            "GameGenerationConfig" => "Starting setup",
            "GameConfig" => "Rules",
            "FactionThemes" => "Interface",
            "Scenario" => "Scenarios",
            "ContentPreload" => "Asset preloads",
            "Faction" or "Factions" => "Factions",
            _ => ContentPackEditorField.FormatName(root),
        };
    }
}
