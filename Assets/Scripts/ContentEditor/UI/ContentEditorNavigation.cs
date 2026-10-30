using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Missions;

/// <summary>
/// Builds and filters the content hierarchy without creating an intermediate document list.
/// </summary>
public sealed class ContentEditorNavigation
{
    private static readonly string[] _unitCategories =
    {
        "Officers",
        "Special forces",
        "Troops",
        "Capital ships",
        "Starfighters",
    };
    private readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ContentEditorTreeNode> roots = new List<ContentEditorTreeNode>();
    private readonly Dictionary<string, string> factionNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Clears expansion state when changing projects.
    /// </summary>
    public void Reset()
    {
        expanded.Clear();
        roots.Clear();
        factionNames.Clear();
    }

    /// <summary>
    /// Rebuilds the content hierarchy while retaining the author's expanded branches.
    /// </summary>
    /// <param name="catalogs">The project's editable catalogs.</param>
    public void Render(IReadOnlyList<ContentPackEditorCatalog> catalogs)
    {
        roots.Clear();
        factionNames.Clear();
        foreach (
            ContentPackEditorRecord faction in catalogs
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "Faction")
        )
        {
            string id = faction.Element.Element("InstanceID")?.Value;
            if (!string.IsNullOrWhiteSpace(id))
                factionNames[id] = faction.Name;
        }
        HashSet<string> entities = new HashSet<string>(
            catalogs
                .SelectMany(catalog =>
                    catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
                )
                .Where(ContentEditorContentSections.HasEncyclopedia)
                .Select(record => record.ID),
            StringComparer.Ordinal
        );
        ContentEditorTreeNode missions = CreateMissions(catalogs);
        ContentPackEditorRecord[] referenceArticles = catalogs
            .SelectMany(catalog => catalog.Records)
            .Where(record =>
                record.Kind == "EncyclopediaEntry"
                && record.Element.Element("Category")?.Value != "Mission"
                && !entities.Contains(record.ID)
            )
            .ToArray();
        ContentEditorTreeNode units = new ContentEditorTreeNode("units", "Units", 0);
        foreach (string category in _unitCategories)
        {
            ContentPackEditorCatalog[] matches = catalogs
                .Where(catalog => catalog.Name == category)
                .ToArray();
            if (matches.Length > 0)
                units.Children.Add(CreateCatalogGroup("units/" + category, category, 1, matches));
        }
        if (units.Children.Count > 0)
            roots.Add(units);
        foreach (
            IGrouping<string, ContentPackEditorCatalog> category in catalogs
                .Where(catalog =>
                    !_unitCategories.Contains(catalog.Name)
                    && catalog.Name is not "Scenarios" and not "Interface" and not "Asset preloads"
                )
                .GroupBy(catalog => catalog.Name)
                .OrderBy(group => GetCategoryOrder(group.Key))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
        )
        {
            if (category.Key == "Galaxy map")
            {
                ContentEditorTreeNode galaxy = new ContentEditorTreeNode(
                    "galaxy",
                    "Galaxy",
                    0,
                    catalog: category.First()
                );
                foreach (ContentPackEditorCatalog catalog in category)
                {
                    foreach (ContentPackEditorRecord record in catalog.Records)
                    {
                        ContentEditorTreeNode sector = RecordNode(record, 1);
                        foreach (ContentPackEditorRecord planet in record.Planets)
                            sector.Children.Add(RecordNode(planet, 2));
                        sector.Children.Add(
                            new ContentEditorTreeNode(
                                record.Key + "/add-planet",
                                "+ Add planet",
                                2,
                                creationCatalog: catalog,
                                creationParent: record
                            )
                        );
                        galaxy.Children.Add(sector);
                    }
                }
                galaxy.Children.Add(
                    new ContentEditorTreeNode(
                        "galaxy/add-sector",
                        "+ Add sector",
                        1,
                        creationCatalog: category.First()
                    )
                );
                roots.Add(galaxy);
            }
            else if (category.Key == "Factions")
                roots.Add(
                    CreateFactions(
                        category.ToArray(),
                        referenceArticles,
                        catalogs
                            .Where(catalog =>
                                catalog.Records.Any(record => record.Kind == "FactionTheme")
                            )
                            .ToArray()
                    )
                );
            else if (category.Key == "Encyclopedia entries")
            {
                ContentEditorTreeNode reference = new ContentEditorTreeNode(
                    "reference",
                    "Reference",
                    0
                );
                foreach (
                    ContentPackEditorCatalog catalog in category.Where(catalog =>
                        catalog.Faction == "Shared"
                    )
                )
                {
                    ContentEditorTreeNode faction = new ContentEditorTreeNode(
                        "reference/" + catalog.Document.Address,
                        catalog.Faction,
                        1
                    );
                    foreach (
                        ContentPackEditorRecord article in referenceArticles.Where(record =>
                            record.Catalog == catalog
                        )
                    )
                        faction.Children.Add(RecordNode(article, 2));
                    if (faction.Children.Count > 0)
                        reference.Children.Add(faction);
                }
                if (reference.Children.Count > 0)
                    roots.Add(reference);
            }
            else if (category.Key == "Rules")
            {
                foreach (ContentPackEditorCatalog catalog in category)
                foreach (ContentPackEditorRecord record in catalog.Records)
                    roots.Add(CreateRules(record));
            }
            else
                roots.Add(
                    CreateCatalogGroup(
                        "category/" + category.Key,
                        category.Key,
                        0,
                        category.ToArray()
                    )
                );
        }
        ContentPackEditorCatalog pack = catalogs.FirstOrDefault(catalog => catalog.CanAddFaction);
        if (pack != null)
        {
            ContentEditorTreeNode factions = roots.FirstOrDefault(node => node.Key == "factions");
            if (factions == null)
            {
                factions = new ContentEditorTreeNode("factions", "Factions", 0);
                int settings = roots.FindIndex(node =>
                    node.Key is "category/Starting setup" or "category/Pack settings"
                );
                roots.Insert(settings < 0 ? roots.Count : settings, factions);
            }
            factions.Children.Add(
                new ContentEditorTreeNode("factions/add", "+ Add faction", 1, creationCatalog: pack)
            );
        }
        if (missions.Children.Count > 0)
        {
            int traits = roots.FindIndex(node => node.Key == "category/Officer traits");
            int index =
                traits >= 0 ? traits + 1
                : units.Children.Count > 0 ? 1
                : 0;
            roots.Insert(Math.Min(roots.Count, index), missions);
        }
    }

    /// <summary>
    /// Creates focused pages for shared rules without duplicating mission-specific controls.
    /// </summary>
    /// <param name="record">The rules document.</param>
    /// <returns>The expandable rules branch.</returns>
    private static ContentEditorTreeNode CreateRules(ContentPackEditorRecord record)
    {
        string key = record.Catalog.Document.Address + "#rules";
        ContentEditorTreeNode root = new(key, "Game rules", 0);
        foreach (
            var group in record
                .GetFields()
                .Where(field =>
                    field.Group != "AI"
                    && (
                        !ContentEditorContentSections.IsMissionField(field)
                        || ContentEditorContentSections.MissionFor(field) == null
                    )
                )
                .GroupBy(ContentEditorContentSections.RuleCategory)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
        )
            root.Children.Add(
                new ContentEditorTreeNode(
                    key + "/" + group.Key,
                    group.Key,
                    1,
                    record,
                    record.Catalog,
                    group.ToArray()
                )
            );
        return root;
    }

    /// <summary>
    /// Groups a faction's gameplay properties and manifest paths under one faction heading.
    /// </summary>
    /// <param name="catalogs">The faction settings and manifests.</param>
    /// <param name="articles">The remaining faction-owned encyclopedia entries.</param>
    /// <param name="themes">The effective faction presentation catalogs.</param>
    /// <returns>The combined faction navigation branch.</returns>
    private static ContentEditorTreeNode CreateFactions(
        IReadOnlyList<ContentPackEditorCatalog> catalogs,
        IReadOnlyList<ContentPackEditorRecord> articles,
        IReadOnlyList<ContentPackEditorCatalog> themes
    )
    {
        ContentEditorTreeNode root = new ContentEditorTreeNode("factions", "Factions", 0);
        foreach (
            IGrouping<string, ContentPackEditorCatalog> group in catalogs.GroupBy(catalog =>
                catalog.Faction
            )
        )
        {
            ContentPackEditorRecord[] records = group
                .SelectMany(catalog => catalog.Records)
                .OrderBy(record => record.Element.Element("FactionDataPath") != null)
                .Concat(
                    articles
                        .Where(record => record.Catalog.Faction == group.Key)
                        .OrderBy(record => record.Name, StringComparer.Ordinal)
                )
                .ToArray();
            if (records.Length == 0)
                continue;
            string themePath = records
                .Select(record => record.Element.Element("ThemePath")?.Value)
                .FirstOrDefault(path => !string.IsNullOrEmpty(path));
            ContentPackEditorRecord theme = themes
                .Where(catalog => catalog.Document.Address == "Pack/" + themePath)
                .SelectMany(catalog => catalog.Records)
                .FirstOrDefault();
            ContentPackEditorField[] fields = records
                .SelectMany(ContentEditorInspectorLayout.GetFields)
                .Concat(ContentEditorContentSections.FactionArtworkFields(theme))
                .ToArray();
            ContentEditorTreeNode faction = new ContentEditorTreeNode(
                "factions/" + group.Key,
                records[0].Name,
                1,
                records[0],
                records[0].Catalog,
                fields,
                theme == null ? records : records.Append(theme).ToArray()
            );
            root.Children.Add(faction);
        }
        return root;
    }

    /// <summary>
    /// Places each mission's execution settings and faction articles in the same branch.
    /// </summary>
    /// <param name="catalogs">The project's authored catalogs.</param>
    /// <returns>The mission navigation branch.</returns>
    private static ContentEditorTreeNode CreateMissions(
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    )
    {
        ContentEditorTreeNode root = new ContentEditorTreeNode("missions", "Missions", 0);
        ContentPackEditorRecord[] rules = catalogs
            .Where(catalog => catalog.Name == "Rules")
            .SelectMany(catalog => catalog.Records)
            .ToArray();
        Dictionary<ContentPackEditorRecord, ILookup<string, ContentPackEditorField>> fields =
            rules.ToDictionary(
                record => record,
                record =>
                    record
                        .GetFields()
                        .Where(ContentEditorContentSections.IsMissionField)
                        .ToLookup(ContentEditorContentSections.MissionFor, StringComparer.Ordinal)
            );
        ContentPackEditorRecord[] missionArticles = catalogs
            .SelectMany(catalog => catalog.Records)
            .Where(record =>
                record.Kind == "EncyclopediaEntry"
                && record.Element.Element("Category")?.Value == "Mission"
            )
            .ToArray();
        string[] missionIDs = ContentPackEditorFieldSchema
            .MissionIDs.Where(id =>
                id != MissionTypeIDs.Research
                || !missionArticles.Any(article =>
                    article.ID
                        is MissionIconKeys.ResearchShipDesign
                            or MissionIconKeys.ResearchFacilityDesign
                            or MissionIconKeys.ResearchTroopTraining
                )
            )
            .Concat(missionArticles.Select(article => article.ID))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        foreach (string id in missionIDs)
        {
            ContentPackEditorRecord[] articles = missionArticles
                .Where(article => article.ID == id)
                .ToArray();
            string ruleID = id
                is MissionIconKeys.ResearchShipDesign
                    or MissionIconKeys.ResearchFacilityDesign
                    or MissionIconKeys.ResearchTroopTraining
                ? MissionTypeIDs.Research
                : id;
            ContentPackEditorField[] specific = rules
                .SelectMany(record => fields[record][ruleID])
                .ToArray();
            ContentPackEditorRecord primary = articles.FirstOrDefault() ?? rules.FirstOrDefault();
            if (primary != null)
                root.Children.Add(
                    new ContentEditorTreeNode(
                        "missions/" + id,
                        articles.FirstOrDefault()?.Name ?? ContentPackEditorField.FormatName(id),
                        1,
                        primary,
                        primary.Catalog,
                        articles
                            .SelectMany(article => article.GetFields())
                            .Concat(specific)
                            .ToArray(),
                        articles,
                        id
                    )
                );
        }
        return root;
    }

    /// <summary>
    /// Finds a logical page after history replaces its underlying XML records.
    /// </summary>
    /// <param name="key">The stable navigation key.</param>
    /// <returns>The current page, or null if it was removed.</returns>
    public ContentEditorTreeNode Find(string key)
    {
        return GetRowsForAll().FirstOrDefault(node => node.Key == key);
    }

    /// <summary>
    /// Enumerates all pages independently of expansion and search state.
    /// </summary>
    /// <returns>All navigation nodes.</returns>
    private IEnumerable<ContentEditorTreeNode> GetRowsForAll()
    {
        Stack<ContentEditorTreeNode> pending = new Stack<ContentEditorTreeNode>(roots);
        while (pending.Count > 0)
        {
            ContentEditorTreeNode node = pending.Pop();
            yield return node;
            foreach (ContentEditorTreeNode child in node.Children)
                pending.Push(child);
        }
    }

    /// <summary>
    /// Gets visible rows, revealing matching descendants automatically during a search.
    /// </summary>
    /// <param name="filter">The case-insensitive record name or identifier filter.</param>
    /// <returns>The visible hierarchy rows.</returns>
    public IReadOnlyList<ContentEditorTreeNode> GetRows(string filter)
    {
        List<ContentEditorTreeNode> rows = new List<ContentEditorTreeNode>();
        string text = filter?.Trim() ?? string.Empty;
        foreach (ContentEditorTreeNode root in roots)
            AddVisibleRows(root, text, false, rows);
        return rows;
    }

    /// <summary>
    /// Toggles one branch without changing its selected record.
    /// </summary>
    /// <param name="node">The branch to toggle.</param>
    public void Toggle(ContentEditorTreeNode node)
    {
        if (node.Children.Count == 0)
            return;
        if (!expanded.Remove(node.Key))
            expanded.Add(node.Key);
    }

    /// <summary>
    /// Gets whether a branch is expanded outside an active search.
    /// </summary>
    /// <param name="node">The branch to inspect.</param>
    /// <returns>True when its descendants are visible.</returns>
    public bool IsExpanded(ContentEditorTreeNode node)
    {
        return expanded.Contains(node.Key);
    }

    /// <summary>
    /// Changes every branch's disclosure state without changing the selected record.
    /// </summary>
    /// <param name="expand">Whether all branches should be expanded.</param>
    public void SetExpanded(bool expand)
    {
        expanded.Clear();
        if (!expand)
            return;
        Stack<ContentEditorTreeNode> pending = new Stack<ContentEditorTreeNode>(roots);
        while (pending.Count > 0)
        {
            ContentEditorTreeNode node = pending.Pop();
            if (node.Children.Count == 0)
                continue;
            expanded.Add(node.Key);
            foreach (ContentEditorTreeNode child in node.Children)
                pending.Push(child);
        }
    }

    /// <summary>
    /// Expands ancestors of the selected record while preserving unrelated branch state.
    /// </summary>
    /// <param name="record">The record to reveal.</param>
    /// <param name="key">The optional logical page to reveal when several pages share a record.</param>
    public void Reveal(ContentPackEditorRecord record, string key = null)
    {
        foreach (ContentEditorTreeNode root in roots)
            RevealInBranch(root, record, key);
    }

    /// <summary>
    /// Builds category and faction branches with named records as their leaves.
    /// </summary>
    /// <param name="key">The stable branch key.</param>
    /// <param name="caption">The category caption.</param>
    /// <param name="depth">The category depth.</param>
    /// <param name="catalogs">The catalogs in the category.</param>
    /// <returns>The completed branch.</returns>
    private ContentEditorTreeNode CreateCatalogGroup(
        string key,
        string caption,
        int depth,
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    )
    {
        if (catalogs.Any(catalog => catalog.CanAddUnit))
            return CreateUnitGroup(key, caption, depth, catalogs);
        if (
            catalogs.Count == 1
            && catalogs[0].Faction == "Shared"
            && !catalogs[0].CanDuplicate
            && catalogs[0].Records.Count == 1
        )
            return new ContentEditorTreeNode(
                key,
                caption,
                depth,
                catalogs[0].Records[0],
                catalogs[0]
            );
        ContentEditorTreeNode group = new ContentEditorTreeNode(key, caption, depth);
        bool factions = catalogs.Any(catalog => catalog.Faction != "Shared");
        foreach (
            ContentPackEditorCatalog catalog in catalogs.OrderBy(
                catalog => catalog.Faction,
                StringComparer.Ordinal
            )
        )
        {
            ContentEditorTreeNode target = group;
            if (factions)
            {
                target = new ContentEditorTreeNode(
                    key + "/" + catalog.Document.Address,
                    catalog.Faction,
                    depth + 1
                );
                group.Children.Add(target);
            }
            foreach (
                ContentPackEditorRecord record in catalog.Records.OrderBy(
                    record => record.Name,
                    StringComparer.Ordinal
                )
            )
                target.Children.Add(RecordNode(record, target.Depth + 1));
            if (catalog.Records.Count == 0)
                target.Children.Add(
                    new ContentEditorTreeNode(
                        catalog.Document.Address + "#empty",
                        "No entries",
                        target.Depth + 1,
                        catalog.DocumentRecord,
                        catalog
                    )
                );
            if (catalog.CanAddUnit || catalog.CanAddEvent || catalog.CanAddTrait)
                target.Children.Add(
                    new ContentEditorTreeNode(
                        catalog.Document.Address + "#add",
                        "+ Add " + catalog.ItemName.ToLowerInvariant(),
                        target.Depth + 1,
                        creationCatalog: catalog
                    )
                );
        }
        return group;
    }

    /// <summary>
    /// Groups shared unit definitions without copying them or changing their owning document.
    /// </summary>
    /// <param name="key">The category identity.</param>
    /// <param name="caption">The category caption.</param>
    /// <param name="depth">The category depth.</param>
    /// <param name="catalogs">The effective unit catalogs.</param>
    /// <returns>The unit category with exclusive and shared branches.</returns>
    private ContentEditorTreeNode CreateUnitGroup(
        string key,
        string caption,
        int depth,
        IReadOnlyList<ContentPackEditorCatalog> catalogs
    )
    {
        ContentEditorTreeNode root = new(key, caption, depth);
        ContentPackEditorRecord[] records = catalogs
            .SelectMany(catalog => catalog.Records)
            .ToArray();
        ContentPackEditorCatalog destinationCatalog = catalogs.First(catalog => catalog.CanAddUnit);
        string[] factions = catalogs
            .Where(catalog => catalog.Records.Count == 0)
            .Select(catalog => catalog.Faction)
            .Concat(records.Select(GetUnitGroup))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(faction => faction == "Shared")
            .ThenBy(faction => faction, StringComparer.Ordinal)
            .ToArray();
        foreach (string faction in factions)
        {
            ContentPackEditorCatalog[] destinations = catalogs
                .Where(catalog => catalog.Faction == faction)
                .ToArray();
            ContentEditorTreeNode branch = new(
                key + "/" + (destinations.FirstOrDefault()?.Document.Address ?? faction),
                faction,
                depth + 1
            );
            foreach (
                ContentPackEditorRecord record in records
                    .Where(record => GetUnitGroup(record) == faction)
                    .OrderBy(record => record.Name, StringComparer.Ordinal)
            )
                branch.Children.Add(RecordNode(record, depth + 2));
            foreach (ContentPackEditorCatalog destination in destinations)
            {
                if (destination.Records.Count == 0)
                    branch.Children.Add(
                        new ContentEditorTreeNode(
                            destination.Document.Address + "#empty",
                            "No entries",
                            depth + 2,
                            destination.DocumentRecord,
                            destination
                        )
                    );
            }
            branch.Children.Add(
                new ContentEditorTreeNode(
                    branch.Key + "/add",
                    "+ Add " + destinationCatalog.ItemName.ToLowerInvariant(),
                    depth + 2,
                    creationCatalog: destinationCatalog
                )
            );
            root.Children.Add(branch);
        }
        root.Children.Add(
            new ContentEditorTreeNode(
                key + "#add",
                "+ Add " + destinationCatalog.ItemName.ToLowerInvariant(),
                depth + 1,
                creationCatalog: destinationCatalog
            )
        );
        return root;
    }

    /// <summary>
    /// Groups units by their authored faction choices independently of file placement.
    /// </summary>
    /// <param name="record">The selected definition.</param>
    /// <returns>The faction label, Shared, or Unassigned for an empty faction selection.</returns>
    internal string GetUnitGroup(ContentPackEditorRecord record)
    {
        if (
            record.Kind
            is not (
                "Officer"
                or "CapitalShip"
                or "Starfighter"
                or "Regiment"
                or "SpecialForce"
                or "Building"
            )
        )
            return record.Catalog.Faction;
        string member =
            record.Kind == "Officer"
                ? "RecruitingFactionInstanceIDs"
                : "ManufacturingFactionInstanceIDs";
        string[] factions =
            record
                .Element.Element(member)
                ?.Elements()
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            ?? Array.Empty<string>();
        if (factions.Length > 1)
            return "Shared";
        string faction =
            factions.FirstOrDefault() ?? record.Element.Element("OwnerInstanceID")?.Value;
        if (!string.IsNullOrWhiteSpace(faction))
            return factionNames.TryGetValue(faction, out string label) ? label : faction;
        return record.Element.Element(member) == null ? record.Catalog.Faction : "Unassigned";
    }

    /// <summary>
    /// Creates a named record row with an identity distinct from every other catalog's records.
    /// </summary>
    /// <param name="record">The represented record.</param>
    /// <param name="depth">Its hierarchy depth.</param>
    /// <returns>The record node.</returns>
    private static ContentEditorTreeNode RecordNode(ContentPackEditorRecord record, int depth)
    {
        return new ContentEditorTreeNode(record.Key, record.Name, depth, record, record.Catalog);
    }

    /// <summary>
    /// Orders the authoring categories around units, maps, setup, and presentation.
    /// </summary>
    /// <param name="name">The category caption.</param>
    /// <returns>The stable category order.</returns>
    private static int GetCategoryOrder(string name)
    {
        return name switch
        {
            "Officer traits" => -1,
            "Buildings" => 0,
            "Galaxy map" => 1,
            "Starting setup" => 19,
            "Scenarios" => 3,
            "Rules" => 4,
            "Game events" => 5,
            "Interface" => 7,
            "Pack settings" => 20,
            _ => 10,
        };
    }

    /// <summary>
    /// Appends visible branches and search matches in hierarchy order.
    /// </summary>
    /// <param name="node">The current branch.</param>
    /// <param name="filter">The active search.</param>
    /// <param name="ancestorMatches">Whether a matching ancestor exposes this whole branch.</param>
    /// <param name="rows">The accumulated rows.</param>
    private void AddVisibleRows(
        ContentEditorTreeNode node,
        string filter,
        bool ancestorMatches,
        List<ContentEditorTreeNode> rows
    )
    {
        bool searching = filter.Length > 0;
        bool matches = ancestorMatches || Matches(node, filter);
        if (searching && !matches && !node.Children.Any(child => ContainsMatch(child, filter)))
            return;
        rows.Add(node);
        if (searching || expanded.Contains(node.Key))
            foreach (ContentEditorTreeNode child in node.Children)
                AddVisibleRows(child, filter, searching && matches, rows);
    }

    /// <summary>
    /// Checks a branch and its descendants for a search match.
    /// </summary>
    /// <param name="node">The branch to inspect.</param>
    /// <param name="filter">The search text.</param>
    /// <returns>True when a matching row exists.</returns>
    private static bool ContainsMatch(ContentEditorTreeNode node, string filter)
    {
        return Matches(node, filter) || node.Children.Any(child => ContainsMatch(child, filter));
    }

    /// <summary>
    /// Checks one row's caption and authored identifier.
    /// </summary>
    /// <param name="node">The row to inspect.</param>
    /// <param name="filter">The search text.</param>
    /// <returns>True when the row matches.</returns>
    private static bool Matches(ContentEditorTreeNode node, string filter)
    {
        return node.Caption.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || node.Record?.Kind == "GameConfig"
                && node.Fields?.Any(field =>
                    field.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                ) == true
            || node.Record?.ID.Contains(filter, StringComparison.OrdinalIgnoreCase) == true
            || node.RelatedRecords.Any(record =>
                record.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || record.ID.Contains(filter, StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>
    /// Reveals a record and records the expansion state along its ancestor path.
    /// </summary>
    /// <param name="node">The current branch.</param>
    /// <param name="record">The selected record.</param>
    /// <param name="key">The optional logical page identity.</param>
    /// <returns>True when the branch contains that record.</returns>
    private bool RevealInBranch(
        ContentEditorTreeNode node,
        ContentPackEditorRecord record,
        string key
    )
    {
        if (record == null)
            return false;
        if (
            (
                node.Record?.Element == record.Element
                || node.RelatedRecords.Any(related => related.Element == record.Element)
            ) && (key == null || key == node.Key)
        )
            return true;
        bool contains = false;
        foreach (ContentEditorTreeNode child in node.Children)
            contains |= RevealInBranch(child, record, key);
        if (contains)
            expanded.Add(node.Key);
        return contains;
    }
}

/// <summary>
/// Describes a category, named record, or map overview in the content tree.
/// </summary>
public sealed class ContentEditorTreeNode
{
    public string Key { get; }
    public string Caption { get; }
    public int Depth { get; }
    public ContentPackEditorRecord Record { get; }
    public ContentPackEditorCatalog Catalog { get; }
    public IReadOnlyList<ContentPackEditorField> Fields { get; }
    public IReadOnlyList<ContentPackEditorRecord> RelatedRecords { get; }
    public ContentPackEditorCatalog CreationCatalog { get; }
    public ContentPackEditorRecord CreationParent { get; }
    public string MissionID { get; }
    public List<ContentEditorTreeNode> Children { get; } = new List<ContentEditorTreeNode>();

    /// <summary>
    /// Creates one hierarchy row with optional record or map content.
    /// </summary>
    /// <param name="key">The stable row key.</param>
    /// <param name="caption">The user-facing label.</param>
    /// <param name="depth">The indentation depth.</param>
    /// <param name="record">The optional named record.</param>
    /// <param name="catalog">The optional catalog owning a record or map.</param>
    /// <param name="fields">The optional subset of fields represented by this logical page.</param>
    /// <param name="relatedRecords">The related definitions presented together on this page.</param>
    /// <param name="missionID">The optional mission represented by this page.</param>
    /// <param name="creationCatalog">The optional destination of a create action.</param>
    /// <param name="creationParent">The optional parent receiving a nested record.</param>
    public ContentEditorTreeNode(
        string key,
        string caption,
        int depth,
        ContentPackEditorRecord record = null,
        ContentPackEditorCatalog catalog = null,
        IReadOnlyList<ContentPackEditorField> fields = null,
        IReadOnlyList<ContentPackEditorRecord> relatedRecords = null,
        string missionID = null,
        ContentPackEditorCatalog creationCatalog = null,
        ContentPackEditorRecord creationParent = null
    )
    {
        Key = key;
        Caption = caption;
        Depth = depth;
        Record = record;
        Catalog = catalog;
        Fields = fields;
        RelatedRecords = relatedRecords ?? Array.Empty<ContentPackEditorRecord>();
        MissionID = missionID;
        CreationCatalog = creationCatalog;
        CreationParent = creationParent;
    }
}
