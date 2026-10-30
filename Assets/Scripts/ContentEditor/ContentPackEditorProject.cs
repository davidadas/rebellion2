using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

/// <summary>
/// Owns the editable documents and validation state for one authored content pack.
/// </summary>
public sealed class ContentPackEditorProject
{
    private const string _packDefinitionAddress = "Pack/pack.xml";

    private List<ContentPackEditorDocument> documents;
    private ContentPackEditorHistory history;
    private IReadOnlyList<ContentPackEditorCatalog> catalogs;

    public string ContentRootPath { get; }

    public string PackRootPath { get; }

    public bool IsInstalled => IsInstalledPath(ContentRootPath, PackRootPath);

    public ContentPackDefinition Definition { get; private set; }

    internal IReadOnlyList<ContentPackEditorDocument> AllDocuments => documents;

    public IReadOnlyList<ContentPackEditorDocument> Documents =>
        documents.Where(document => !document.IsRemoved).ToArray();

    internal IEnumerable<string> RemovedDocumentPaths =>
        documents.Where(document => document.IsRemoved).Select(document => document.TargetPath);

    public bool IsDirty => documents.Any(document => document.IsDirty);
    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;
    public string UndoDescription => history.UndoDescription;
    public string RedoDescription => history.RedoDescription;

    /// <summary>
    /// Groups a continuous gesture or multi-field operation into one undoable edit.
    /// </summary>
    /// <param name="description">The operation shown in the edit menu.</param>
    /// <returns>The scope to dispose when the operation ends.</returns>
    public IDisposable BeginEdit(string description)
    {
        return history.BeginEdit(description);
    }

    /// <summary>
    /// Restores the preceding edit and rebuilds catalog wrappers from the restored documents.
    /// </summary>
    /// <returns>True when an edit was undone.</returns>
    public bool Undo()
    {
        if (!history.Undo())
            return false;
        catalogs = null;
        RefreshDefinition();
        return true;
    }

    /// <summary>
    /// Reapplies an undone edit and rebuilds catalog wrappers from the restored documents.
    /// </summary>
    /// <returns>True when an edit was reapplied.</returns>
    public bool Redo()
    {
        if (!history.Redo())
            return false;
        catalogs = null;
        RefreshDefinition();
        return true;
    }

    /// <summary>
    /// Moves an owned map definition, preserving a sector's relative planet positions.
    /// </summary>
    /// <param name="record">The project-owned map record.</param>
    /// <param name="x">The horizontal source coordinate.</param>
    /// <param name="y">The vertical source coordinate.</param>
    public void Move(ContentPackEditorRecord record, int x, int y)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        RequireOwnedRecord(record);
        record.Catalog.Move(record, x, y);
    }

    /// <summary>
    /// Applies a field edit together with any manifest link needed for a local rules override.
    /// </summary>
    /// <param name="field">The project-owned field.</param>
    /// <param name="value">The proposed value.</param>
    public void SetField(ContentPackEditorField field, string value)
    {
        RequireOwnedRecord(field.Record);
        ValidateFieldValues(field, new[] { value });
        using (BeginEdit(field.Name))
        {
            field.SetValue(value);
            LinkRulesOverride(field.Document);
        }
    }

    /// <summary>
    /// Applies a scalar-list edit as one reversible project operation.
    /// </summary>
    /// <param name="field">The project-owned collection.</param>
    /// <param name="values">The complete ordered values.</param>
    public void SetFieldValues(ContentPackEditorField field, IEnumerable<string> values)
    {
        RequireOwnedRecord(field.Record);
        string[] replacement = values.ToArray();
        ValidateFieldValues(field, replacement);
        using (BeginEdit(field.Name))
        {
            field.SetListValues(replacement);
            LinkRulesOverride(field.Document);
        }
    }

    /// <summary>
    /// Checks media address scope and containment through the production resolver before editing.
    /// </summary>
    /// <param name="field">The edited member.</param>
    /// <param name="values">The proposed scalar or collection values.</param>
    internal void ValidateFieldValues(ContentPackEditorField field, IEnumerable<string> values)
    {
        ValidateFieldValues(
            field,
            values,
            field.IsMedia
                ? ContentPackLoader.CreateFolderResolver(ContentRootPath, PackRootPath, Definition)
                : null,
            field.IsFactionReference ? GetFactionOptions() : null,
            field.IsTraitReference ? GetTraitOptions() : null
        );
    }

    /// <summary>
    /// Checks an existing field without changing its document or edit history.
    /// </summary>
    /// <param name="field">The project-owned field.</param>
    public void ValidateField(ContentPackEditorField field)
    {
        RequireOwnedRecord(field.Record);
        ValidateFieldValues(field, field.Values);
    }

    /// <summary>
    /// Validates authored values against one shared set of content layers and faction choices.
    /// </summary>
    /// <param name="field">The edited field.</param>
    /// <param name="values">The proposed values.</param>
    /// <param name="resolver">The media resolver, when needed.</param>
    /// <param name="factions">The faction choices, when needed.</param>
    /// <param name="traits">The trait choices, when needed.</param>
    internal void ValidateFieldValues(
        ContentPackEditorField field,
        IEnumerable<string> values,
        ContentFileResolver resolver,
        IReadOnlyDictionary<string, string> factions,
        IReadOnlyDictionary<string, string> traits
    )
    {
        string[] authored = values.ToArray();
        field.ValidateValues(authored);
        string[] nonempty = authored.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (field.IsFactionReference)
        {
            if (nonempty.Any(value => !factions.ContainsKey(value)))
                throw new ArgumentException("Choose a listed faction.");
        }
        if (field.IsTraitReference && nonempty.Any(value => !traits.ContainsKey(value)))
            throw new ArgumentException("Choose a listed trait.");
        if (
            field.MemberName == "DisallowedMissionTypeIDs"
            && nonempty.Any(value => !ContentPackEditorFieldSchema.MissionIDs.Contains(value))
        )
            throw new ArgumentException("Choose a listed mission.");
        if (
            field.IsIdentity
            && nonempty.Any(value => value != field.Value)
            && GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Element != field.Record.Element)
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element))
                .Any(nonempty.Contains)
        )
            throw new ArgumentException("ID already in use.");
        if (!field.IsMedia)
            return;
        foreach (string value in nonempty)
        {
            string address = field.GetMediaAddress(value);
            try
            {
                string file = field.IsMediaDirectory
                    ? null
                    : resolver.ResolveFile(address, field.MediaExtensions);
                bool exists = field.IsMediaDirectory
                    ? resolver.DirectoryExists(address)
                    : file != null;
                if (
                    file != null
                    && !field.MediaExtensions.Contains(
                        Path.GetExtension(file),
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                    throw new ArgumentException("Unsupported file type.");
                if (!exists)
                    throw new ArgumentException(
                        field.IsMediaDirectory ? "Folder not found." : "File not found. Use Import."
                    );
            }
            catch (ArgumentException exception) when (exception.ParamName != null)
            {
                throw new ArgumentException("Use a Pack/ or Application/ path, or Import.");
            }
        }
    }

    /// <summary>
    /// Gets faction identities and labels from the current authored data.
    /// </summary>
    /// <returns>The faction choices keyed by instance ID.</returns>
    public IReadOnlyDictionary<string, string> GetFactionOptions()
    {
        IReadOnlyList<ContentPackEditorCatalog> current = GetCatalogs();
        HashSet<string> factionData = new HashSet<string>(
            Definition
                .FactionPaths.Select(path =>
                    current.FirstOrDefault(catalog =>
                        catalog.Document.Address == "Pack/" + path.Replace('\\', '/')
                    )
                )
                .Where(catalog => catalog != null)
                .Select(catalog => catalog.DocumentRecord.Element.Element("FactionDataPath")?.Value)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => "Pack/" + path.Replace('\\', '/')),
            StringComparer.Ordinal
        );
        return GetCatalogs()
            .Where(catalog => factionData.Contains(catalog.Document.Address))
            .SelectMany(catalog => catalog.Records)
            .Where(record =>
                record.Kind == "Faction"
                && !string.IsNullOrWhiteSpace(record.Element.Element("InstanceID")?.Value)
            )
            .ToDictionary(
                record => record.Element.Element("InstanceID").Value,
                record => record.Name,
                StringComparer.Ordinal
            );
    }

    /// <summary>
    /// Gets traits from the effective inherited and locally edited definitions.
    /// </summary>
    /// <returns>The selection labels keyed by the exact trait IDs stored on officers.</returns>
    public IReadOnlyDictionary<string, string> GetTraitOptions() =>
        GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "Trait" && !string.IsNullOrWhiteSpace(record.ID))
            .ToDictionary(
                record => record.ID,
                record => ContentPackEditorField.FormatName(record.Name.Replace('-', ' ')),
                StringComparer.Ordinal
            );

    /// <summary>
    /// Enumerates the inherited and local files available to a directory media field.
    /// </summary>
    /// <param name="field">The directory field.</param>
    /// <returns>The matching content addresses in stable order.</returns>
    public IReadOnlyList<string> GetMediaFiles(ContentPackEditorField field)
    {
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            ContentRootPath,
            PackRootPath,
            Definition
        );
        return field
            .Values.Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => resolver.EnumerateFileAddresses(field.GetMediaAddress(value)))
            .Where(address =>
                field.MediaExtensions.Contains(
                    Path.GetExtension(address),
                    StringComparer.OrdinalIgnoreCase
                )
            )
            .OrderBy(address => address, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Adds a blank unit with optional faction eligibility as one undoable edit.
    /// </summary>
    /// <param name="catalog">The destination unit catalog.</param>
    /// <param name="factionInstanceID">The optional recruiting or manufacturing faction.</param>
    /// <returns>The newly created definition.</returns>
    public ContentPackEditorRecord AddUnit(
        ContentPackEditorCatalog catalog,
        string factionInstanceID = null
    )
    {
        RequireOwnedCatalog(catalog);
        if (factionInstanceID != null && !GetFactionOptions().ContainsKey(factionInstanceID))
            throw new ArgumentException("Choose an existing faction.", nameof(factionInstanceID));
        HashSet<string> identities = new HashSet<string>(
            GetCatalogs()
                .SelectMany(item =>
                    item.Records.Concat(item.Records.SelectMany(record => record.Planets))
                )
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Add unit"))
        {
            ContentPackEditorRecord unit = catalog.AddUnit(identities);
            if (factionInstanceID != null)
                SetFieldValues(
                    unit.GetFields().Single(field => field.IsFactionReference),
                    new[] { factionInstanceID }
                );
            return unit;
        }
    }

    /// <summary>
    /// Adds an independent faction and empty unit catalogs without changing scenario membership.
    /// </summary>
    /// <returns>The new faction's editable gameplay record.</returns>
    public ContentPackEditorRecord AddFaction()
    {
        VerifyWorkingFolder(ContentRootPath, PackRootPath);
        HashSet<string> identities = GetCatalogs()
            .SelectMany(catalog =>
                catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
            )
            .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element))
            .ToHashSet(StringComparer.Ordinal);
        int suffix = 1;
        string id;
        string folder;
        do
        {
            id = "NEW_FACTION" + (suffix == 1 ? "" : "_" + suffix);
            folder = "Factions/NewFaction" + (suffix == 1 ? "" : suffix.ToString());
            suffix++;
        } while (
            identities.Contains(id)
            || Documents.Any(document =>
                document.Address.StartsWith("Pack/" + folder + "/", StringComparison.Ordinal)
            )
            || Directory.Exists(Path.Combine(PackRootPath, folder))
        );

        ContentPackEditorDocument manifest = Documents.Single(document =>
            document.Address == _packDefinitionAddress
        );
        XDocument xml = XDocument.Parse(manifest.Text);
        string neutralPath = xml.Root.Element("NeutralThemePath")?.Value;
        XElement neutral = GetCatalogs()
            .FirstOrDefault(catalog => catalog.Document.Address == "Pack/" + neutralPath)
            ?.Records.FirstOrDefault(record => record.Kind == "FactionTheme")
            ?.Element;
        IReadOnlyDictionary<string, XElement> additions = ContentEditorFactionFactory.Create(
            id,
            folder,
            neutral
        );
        using (BeginEdit("Add faction"))
        {
            foreach (var addition in additions)
            {
                string address = "Pack/" + addition.Key;
                ContentPackEditorDocument document = documents.FirstOrDefault(candidate =>
                    candidate.Address == address
                );
                if (document == null)
                {
                    document = new ContentPackEditorDocument(
                        address,
                        Path.Combine(PackRootPath, addition.Key)
                    );
                    documents.Add(document);
                    history.Track(document);
                }
                document.SetText(addition.Value.ToString());
            }
            XElement paths = xml.Root.Element("FactionPaths");
            if (paths == null)
                xml.Root.Add(paths = new XElement("FactionPaths"));
            paths.Add(new XElement("Path", folder + "/faction.xml"));
            manifest.SetText(xml.ToString());
        }
        catalogs = null;
        RefreshDefinition();
        return GetCatalogs()
            .Single(catalog => catalog.Document.Address == "Pack/" + folder + "/Data/faction.xml")
            .Records.Single();
    }

    /// <summary>
    /// Refreshes manifest references after a structural edit or its undo.
    /// </summary>
    private void RefreshDefinition() =>
        Definition = ContentDefinitionSerializer.DeserializeText<ContentPackDefinition>(
            documents.Single(document => document.Address == _packDefinitionAddress).Text
        );

    /// <summary>
    /// Applies advanced XML edits without bypassing project ownership or edit history.
    /// </summary>
    /// <param name="document">The project-owned document.</param>
    /// <param name="text">The proposed complete XML.</param>
    public void SetDocumentText(ContentPackEditorDocument document, string text)
    {
        RequireOwnedDocument(document);
        XDocument.Parse(text);
        using (BeginEdit(document.DisplayName))
        {
            document.SetText(text);
            LinkRulesOverride(document);
            catalogs = null;
        }
    }

    /// <summary>
    /// Validates and replaces one object-local XML block while preserving all sibling definitions.
    /// </summary>
    /// <param name="scope">The block selected in the inspector.</param>
    /// <param name="text">The proposed replacement element.</param>
    /// <returns>The current owning record after replacement.</returns>
    public ContentPackEditorRecord SetXml(ContentEditorXmlScope scope, string text)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        ContentPackEditorRecord source = scope.Record;
        RequireOwnedRecord(source);
        if (source.Kind == "GameEvent")
            return SetEventXml(source, text);
        XElement replacement = XElement.Parse(text);
        if (replacement.Name != scope.Element.Name)
            throw new InvalidDataException(
                "Enter one " + scope.Element.Name.LocalName + " element."
            );
        if (XNode.DeepEquals(scope.Element, replacement))
            return source;
        XDocument candidate = XDocument.Parse(source.Catalog.Document.Text);
        XElement owner = ContentEditorXmlScope.FindInCopy(source.Element, candidate);
        XElement target = ContentEditorXmlScope.FindInCopy(scope.Element, candidate);
        if (owner == target)
            owner = replacement;
        target.ReplaceWith(replacement);
        Type type = ContentPackEditorFieldSchema.GetRecordType(owner);
        if (type != null)
        {
            using StringReader reader = new StringReader(owner.ToString());
            if (type.IsDefined(typeof(PersistableObjectAttribute), true))
                new GameSerializer(
                    type,
                    new GameSerializerSettings { RootName = owner.Name.LocalName }
                ).Deserialize(reader);
            else
            {
                System.Xml.Serialization.XmlSerializer serializer =
                    new System.Xml.Serialization.XmlSerializer(type);
                serializer.UnknownElement += (_, args) =>
                    throw new InvalidDataException("Unknown field: " + args.Element.Name);
                serializer.UnknownAttribute += (_, args) =>
                    throw new InvalidDataException("Unknown field: " + args.Attr.Name);
                serializer.Deserialize(reader);
            }
        }
        foreach (string identity in ContentPackEditorCatalog.GetIdentityValues(owner))
            if (
                !ContentPackEditorCatalog.GetIdentityValues(source.Element).Contains(identity)
                && GetCatalogs()
                    .SelectMany(catalog => catalog.Records)
                    .Where(record => record.Element != source.Element)
                    .SelectMany(record =>
                        ContentPackEditorCatalog.GetIdentityValues(record.Element)
                    )
                    .Contains(identity)
            )
                throw new InvalidDataException("ID already in use.");
        SetDocumentText(source.Catalog.Document, candidate.ToString());
        ContentPackEditorCatalog current = GetCatalogs()
            .Single(catalog => catalog.Document == source.Catalog.Document);
        return new ContentPackEditorRecord(
            current,
            ContentEditorXmlScope.FindInCopy(owner, current.DocumentRecord.Element.Document)
        );
    }

    /// <summary>
    /// Creates a blank officer trait using the project's normal edit history.
    /// </summary>
    /// <param name="catalog">The project-owned traits catalog.</param>
    /// <returns>The attached new trait.</returns>
    public ContentPackEditorRecord AddTrait(ContentPackEditorCatalog catalog)
    {
        RequireOwnedCatalog(catalog);
        HashSet<string> identities = new HashSet<string>(
            GetCatalogs()
                .SelectMany(item => item.Records)
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Add trait"))
            return catalog.AddTrait(identities);
    }

    /// <summary>
    /// Creates a starting faction message through the project's normal reversible history.
    /// </summary>
    /// <param name="faction">The project-owned faction definition.</param>
    /// <param name="type">The category in the initial message list.</param>
    /// <returns>The title field to focus in the inspector.</returns>
    public ContentPackEditorField AddFactionMessage(
        ContentPackEditorRecord faction,
        MessageType type
    )
    {
        RequireOwnedRecord(faction);
        if (!Enum.IsDefined(typeof(MessageType), type))
            throw new ArgumentOutOfRangeException(nameof(type));
        using (BeginEdit("Add starting message"))
            return faction.Catalog.AddFactionMessage(faction, type);
    }

    /// <summary>
    /// Adds a blank sector through the project's reversible edit history.
    /// </summary>
    /// <param name="catalog">The project-owned galaxy catalog.</param>
    /// <param name="x">The horizontal map coordinate.</param>
    /// <param name="y">The vertical map coordinate.</param>
    /// <returns>The attached sector definition.</returns>
    public ContentPackEditorRecord AddSector(ContentPackEditorCatalog catalog, int x, int y)
    {
        RequireOwnedCatalog(catalog);
        HashSet<string> identities = new(
            GetCatalogs()
                .SelectMany(item =>
                    item.Records.Concat(item.Records.SelectMany(record => record.Planets))
                )
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Add sector"))
            return catalog.AddSector(identities, x, y);
    }

    /// <summary>
    /// Creates a blank planet in an owned sector through the project's reversible edit history.
    /// </summary>
    /// <param name="sector">The project-owned destination sector.</param>
    /// <param name="x">The absolute horizontal coordinate.</param>
    /// <param name="y">The absolute vertical coordinate.</param>
    /// <returns>The attached new planet.</returns>
    public ContentPackEditorRecord AddPlanet(ContentPackEditorRecord sector, int x, int y)
    {
        if (sector == null)
            throw new ArgumentNullException(nameof(sector));
        RequireOwnedRecord(sector);
        HashSet<string> identities = new(
            GetCatalogs()
                .SelectMany(catalog =>
                    catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
                )
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Add planet"))
            return sector.Catalog.AddPlanet(sector, identities, x, y);
    }

    /// <summary>
    /// Creates a blank event through the same ownership and undo history as other authored records.
    /// </summary>
    /// <param name="catalog">The project-owned events catalog.</param>
    /// <returns>The newly attached event.</returns>
    public ContentPackEditorRecord AddEvent(ContentPackEditorCatalog catalog)
    {
        RequireOwnedCatalog(catalog);
        HashSet<string> identities = new HashSet<string>(
            GetCatalogs()
                .SelectMany(item => item.Records)
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Add event"))
            return catalog.AddEvent(identities);
    }

    /// <summary>
    /// Validates one complete event with the runtime serializer and schema before replacing its XML.
    /// </summary>
    /// <param name="source">The event being edited.</param>
    /// <param name="text">The replacement GameEvent element.</param>
    /// <returns>The validated, attached replacement event.</returns>
    public ContentPackEditorRecord SetEventXml(ContentPackEditorRecord source, string text)
    {
        RequireOwnedRecord(source);
        if (!source.Catalog.CanAddEvent)
            throw new ArgumentException("Choose a current game event.", nameof(source));
        XElement replacement = XElement.Parse(text);
        if (replacement.Name != "GameEvent")
            throw new InvalidDataException("Enter one GameEvent element.");
        XmlSchemaSet schemas = new XmlSchemaSet();
        string schema = Path.Combine(ContentRootPath, "Application", "Schemas", "game-events.xsd");
        using (XmlReader reader = XmlReader.Create(schema))
            schemas.Add(null, reader);
        using StringReader document = new StringReader("<GameEvents>\n" + text + "\n</GameEvents>");
        new GameSerializer(
            typeof(GameEvent[]),
            new GameSerializerSettings { RootName = "GameEvents", Schemas = schemas }
        ).Deserialize(document);
        string identity = replacement.Element("InstanceID")?.Value;
        if (
            GetCatalogs()
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Element != source.Element)
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element))
                .Contains(identity, StringComparer.Ordinal)
        )
            throw new InvalidDataException("ID already in use.");
        using (BeginEdit("Edit event"))
            return source.Catalog.Replace(source, replacement);
    }

    /// <summary>
    /// Rejects a catalog from another project or a discarded document revision.
    /// </summary>
    /// <param name="catalog">The catalog to edit.</param>
    private void RequireOwnedCatalog(ContentPackEditorCatalog catalog)
    {
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        RequireOwnedDocument(catalog.Document);
        if (!GetCatalogs().Contains(catalog))
            throw new ArgumentException("Choose a current object.", nameof(catalog));
    }

    /// <summary>
    /// Rejects detached or stale record handles before they can overwrite restored documents.
    /// </summary>
    /// <param name="record">The record to edit.</param>
    internal void RequireOwnedRecord(ContentPackEditorRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        RequireOwnedCatalog(record.Catalog);
        if (record.Element.Document != record.Catalog.DocumentRecord.Element.Document)
            throw new ArgumentException("Choose a current object.", nameof(record));
    }

    /// <summary>
    /// Removes one owned definition as a single reversible edit.
    /// </summary>
    /// <param name="record">The attached definition to remove.</param>
    public void RemoveRecord(ContentPackEditorRecord record)
    {
        RequireOwnedRecord(record);
        if (record.Kind == "Faction")
        {
            RemoveFaction(record);
            return;
        }
        using (BeginEdit("Delete " + record.Name))
            record.Catalog.Remove(record);
    }

    /// <summary>
    /// Unregisters a faction and removes only XML documents not referenced by other definitions.
    /// </summary>
    /// <param name="record">The faction's gameplay entry or declaration.</param>
    private void RemoveFaction(ContentPackEditorRecord record)
    {
        if (
            record.Element != record.Catalog.DocumentRecord.Element
            && record.Catalog.Records.Count > 1
        )
        {
            using (BeginEdit("Delete " + record.Name))
                record.Catalog.Remove(record);
            return;
        }
        ContentPackEditorCatalog manifest = GetCatalogs()
            .Single(catalog => catalog.Document.Address == _packDefinitionAddress);
        ContentPackEditorCatalog[] declarations = GetCatalogs()
            .Where(catalog =>
                catalog.DocumentRecord.Kind == "Faction"
                && catalog.DocumentRecord.Element.Element("FactionDataPath") != null
            )
            .ToArray();
        ContentPackEditorCatalog[] removed = declarations
            .Where(catalog =>
                catalog.Document.Address == record.Catalog.Document.Address
                || "Pack/"
                    + catalog
                        .DocumentRecord.Element.Element("FactionDataPath")
                        .Value.Replace('\\', '/')
                    == record.Catalog.Document.Address
            )
            .ToArray();
        if (removed.Length == 0)
        {
            if (record.Element == record.Catalog.DocumentRecord.Element)
                throw new InvalidOperationException("This faction is not registered in the pack.");
            using (BeginEdit("Delete " + record.Name))
                record.Catalog.Remove(record);
            return;
        }
        HashSet<string> retained = declarations
            .Except(removed)
            .Append(manifest)
            .SelectMany(GetReferencedDocumentAddresses)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> removedAddresses = removed
            .SelectMany(GetReferencedDocumentAddresses)
            .Where(address => !retained.Contains(address))
            .ToHashSet(StringComparer.Ordinal);
        XDocument xml = XDocument.Parse(manifest.Document.Text);
        using (BeginEdit("Delete " + record.Name))
        {
            xml.Root.Element("FactionPaths")
                ?.Elements()
                .Where(path =>
                    removed.Any(catalog =>
                        catalog.Document.Address == "Pack/" + path.Value.Replace('\\', '/')
                    )
                )
                .Remove();
            if (
                !removedAddresses.Contains(record.Catalog.Document.Address)
                && record.Element != record.Catalog.DocumentRecord.Element
            )
                record.Catalog.Remove(record);
            foreach (
                ContentPackEditorDocument document in Documents.Where(document =>
                    removedAddresses.Contains(document.Address)
                )
            )
                document.RestoreText(null);
            manifest.Document.SetText(xml.ToString());
        }
        catalogs = null;
        RefreshDefinition();
    }

    /// <summary>
    /// Enumerates a declaration and its referenced files while leaving faction registration explicit.
    /// </summary>
    /// <param name="catalog">The pack or faction declaration.</param>
    /// <returns>The normalized content addresses referenced by the declaration.</returns>
    private static IEnumerable<string> GetReferencedDocumentAddresses(
        ContentPackEditorCatalog catalog
    )
    {
        yield return catalog.Document.Address;
        foreach (
            XElement path in catalog
                .DocumentRecord.Element.Elements()
                .Where(element => element.Name.LocalName != "FactionPaths")
                .SelectMany(element => element.DescendantsAndSelf())
                .Where(element =>
                    !element.HasElements
                    && element.Name.LocalName.EndsWith("Path", StringComparison.Ordinal)
                )
        )
        {
            string value = path.Value.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(value))
                yield return value.StartsWith("Pack/", StringComparison.Ordinal)
                || value.StartsWith("Application/", StringComparison.Ordinal)
                    ? value
                    : "Pack/" + value;
        }
    }

    /// <summary>
    /// Rejects an edit directed at another open project's document.
    /// </summary>
    /// <param name="document">The document to verify.</param>
    private void RequireOwnedDocument(ContentPackEditorDocument document)
    {
        VerifyWorkingFolder(ContentRootPath, PackRootPath);
        if (!documents.Contains(document) || document.IsRemoved)
            throw new ArgumentException(
                "The edited field is not part of this project.",
                nameof(document)
            );
    }

    /// <summary>
    /// Links the application-default rules document only when the author edits its local override.
    /// </summary>
    /// <param name="document">The edited document.</param>
    private void LinkRulesOverride(ContentPackEditorDocument document)
    {
        if (
            !document.IsDirty
            || document.Address != "Pack/Shared/Data/game-config.xml"
            || !document.SourcePath.StartsWith(
                Path.Combine(ContentRootPath, "Application") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return;
        ContentPackEditorDocument manifest = documents.Single(candidate =>
            candidate.Address == _packDefinitionAddress
        );
        XDocument xml = XDocument.Parse(manifest.Text);
        if (!string.IsNullOrWhiteSpace(xml.Root.Element("GameConfigPath")?.Value))
            return;
        xml.Root.SetElementValue("GameConfigPath", "Shared/Data/game-config.xml");
        manifest.SetText(xml.ToString());
    }

    /// <summary>
    /// Opens the pack's catalogs as named-record editors, including inherited data.
    /// </summary>
    /// <returns>The catalogs ordered by editor category and faction.</returns>
    public IReadOnlyList<ContentPackEditorCatalog> GetCatalogs()
    {
        return catalogs ??= CreateCatalogs();
    }

    /// <summary>
    /// Keeps unregistered inherited faction files out of the effective authoring catalogs.
    /// </summary>
    /// <returns>The active catalogs ordered by category and faction.</returns>
    private IReadOnlyList<ContentPackEditorCatalog> CreateCatalogs()
    {
        ContentPackEditorCatalog[] all = Documents
            .Select(document => new ContentPackEditorCatalog(document))
            .ToArray();
        ContentPackEditorCatalog[] declarations = all.Where(catalog =>
                catalog.DocumentRecord.Kind == "Faction"
                && catalog.DocumentRecord.Element.Element("FactionDataPath") != null
            )
            .ToArray();
        HashSet<string> registered = Definition
            .FactionPaths.Select(path => "Pack/" + path.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> active = declarations
            .Where(catalog => registered.Contains(catalog.Document.Address))
            .Concat(all.Where(catalog => catalog.Document.Address == _packDefinitionAddress))
            .SelectMany(GetReferencedDocumentAddresses)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> inactive = declarations
            .SelectMany(GetReferencedDocumentAddresses)
            .Where(address => !active.Contains(address))
            .ToHashSet(StringComparer.Ordinal);
        return all.Where(catalog =>
                !inactive.Contains(catalog.Document.Address)
                && (
                    !declarations.Contains(catalog) || registered.Contains(catalog.Document.Address)
                )
            )
            .OrderBy(catalog => catalog.Name, StringComparer.Ordinal)
            .ThenBy(catalog => catalog.Faction, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Creates a named entry without colliding with identities elsewhere in the working pack.
    /// </summary>
    /// <param name="source">The project-owned entry to use as a template.</param>
    /// <returns>The new editable entry.</returns>
    public ContentPackEditorRecord DuplicateRecord(ContentPackEditorRecord source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        RequireOwnedRecord(source);
        if (source.Kind == "Faction")
            throw new InvalidOperationException("Use Add faction to create a faction.");
        HashSet<string> identities = new HashSet<string>(
            GetCatalogs()
                .SelectMany(catalog =>
                    catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
                )
                .SelectMany(record => ContentPackEditorCatalog.GetIdentityValues(record.Element)),
            StringComparer.Ordinal
        );
        using (BeginEdit("Duplicate " + source.Name))
            return source.Catalog.Duplicate(source, identities);
    }

    /// <summary>
    /// Creates a project from a selected pack and its resolver.
    /// </summary>
    /// <param name="resolver">The selected pack's layered resolver.</param>
    /// <param name="definition">The selected pack definition.</param>
    /// <param name="documents">The already resolved editable documents.</param>
    internal ContentPackEditorProject(
        ContentFileResolver resolver,
        ContentPackDefinition definition,
        IEnumerable<ContentPackEditorDocument> documents
    )
    {
        ContentRootPath = resolver.ContentRootPath;
        PackRootPath = resolver.PackRootPath;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        this.documents = documents.ToList();
        history = new ContentPackEditorHistory(this.documents);
    }

    /// <summary>
    /// Identifies managed pack and application data directories that authoring must preserve.
    /// </summary>
    /// <param name="contentRootPath">The installed content root.</param>
    /// <param name="folder">The proposed project folder.</param>
    /// <returns>Whether the folder overlaps installed content.</returns>
    internal static bool IsInstalledPath(string contentRootPath, string folder)
    {
        string root = Path.GetFullPath(contentRootPath);
        string target = Path.GetFullPath(folder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string name in new[] { "Packs", "Application" })
        {
            string protectedRoot = Path.Combine(root, name);
            if (
                string.Equals(target, protectedRoot, StringComparison.OrdinalIgnoreCase)
                || target.StartsWith(
                    protectedRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return true;
        }
        return string.Equals(
            target,
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase
        );
    }

    /// <summary>
    /// Prevents a working project from modifying installed pack or application content.
    /// </summary>
    /// <param name="contentRootPath">The installed content root.</param>
    /// <param name="folder">The proposed project folder.</param>
    internal static void VerifyWorkingFolder(string contentRootPath, string folder)
    {
        if (IsInstalledPath(contentRootPath, folder))
            throw new IOException(
                "Installed content is read-only. Create a new content pack in your own folder."
            );
    }

    /// <summary>
    /// Accepts a persisted manifest and an optional replacement document collection.
    /// </summary>
    /// <param name="definition">The manifest written successfully.</param>
    /// <param name="refreshedDocuments">Resolved documents when inheritance changed, otherwise null.</param>
    internal void AcceptSave(
        ContentPackDefinition definition,
        IReadOnlyList<ContentPackEditorDocument> refreshedDocuments
    )
    {
        Definition = definition;
        if (refreshedDocuments == null)
            return;
        documents = refreshedDocuments.ToList();
        catalogs = null;
        history = new ContentPackEditorHistory(documents);
    }
}
