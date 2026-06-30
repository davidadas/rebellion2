using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Encyclopedia
{
    /// <summary>
    /// Provides the merged encyclopedia entries used by UI consumers.
    /// </summary>
    public sealed class EncyclopediaCatalog : IReadOnlyList<EncyclopediaEntry>
    {
        private readonly List<EncyclopediaEntry> _entries;

        /// <summary>
        /// Creates a catalog from encyclopedia entries.
        /// </summary>
        /// <param name="entries">The entries to include in the catalog.</param>
        public EncyclopediaCatalog(IEnumerable<EncyclopediaEntry> entries)
        {
            _entries =
                entries?.Where(entry => entry != null).ToList() ?? new List<EncyclopediaEntry>();
        }

        public int Count => _entries.Count;

        public EncyclopediaEntry this[int index] => _entries[index];

        /// <summary>
        /// Gets the entries visible in an encyclopedia tab for a faction.
        /// </summary>
        /// <param name="tab">The encyclopedia tab index.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entries.</param>
        /// <returns>The visible entries sorted by display name.</returns>
        public List<EncyclopediaEntry> GetRows(int tab, string factionInstanceId)
        {
            return GetRows(_entries, tab, factionInstanceId);
        }

        /// <summary>
        /// Finds an entry visible to a faction by type ID.
        /// </summary>
        /// <param name="typeId">The entry type ID.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>The matching entry, or null when no visible entry exists.</returns>
        public EncyclopediaEntry FindEntry(string typeId, string factionInstanceId)
        {
            return FindEntry(_entries, typeId, factionInstanceId);
        }

        /// <summary>
        /// Gets an enumerator over the catalog entries.
        /// </summary>
        /// <returns>An enumerator over the catalog entries.</returns>
        public IEnumerator<EncyclopediaEntry> GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        /// <summary>
        /// Gets a non-generic enumerator over the catalog entries.
        /// </summary>
        /// <returns>A non-generic enumerator over the catalog entries.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets entries visible in an encyclopedia tab for a faction.
        /// </summary>
        /// <param name="entries">The entries to filter.</param>
        /// <param name="tab">The encyclopedia tab index.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entries.</param>
        /// <returns>The visible entries sorted by display name.</returns>
        public static List<EncyclopediaEntry> GetRows(
            IEnumerable<EncyclopediaEntry> entries,
            int tab,
            string factionInstanceId
        )
        {
            List<EncyclopediaEntry> rows = new List<EncyclopediaEntry>();
            if (entries == null)
                return rows;

            foreach (EncyclopediaEntry entry in entries)
            {
                if (
                    entry != null
                    && IsInTab(entry, tab)
                    && IsVisibleToFaction(entry, factionInstanceId)
                )
                    rows.Add(entry);
            }

            rows.Sort(
                (left, right) =>
                    string.Compare(
                        left?.DisplayName,
                        right?.DisplayName,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
            return rows;
        }

        /// <summary>
        /// Finds an entry visible to a faction by type ID.
        /// </summary>
        /// <param name="entries">The entries to search.</param>
        /// <param name="typeId">The entry type ID.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>The matching entry, or null when no visible entry exists.</returns>
        public static EncyclopediaEntry FindEntry(
            IEnumerable<EncyclopediaEntry> entries,
            string typeId,
            string factionInstanceId
        )
        {
            if (entries == null || string.IsNullOrEmpty(typeId))
                return null;

            foreach (EncyclopediaEntry entry in entries)
            {
                if (
                    string.Equals(entry?.TypeID, typeId, StringComparison.Ordinal)
                    && IsVisibleToFaction(entry, factionInstanceId)
                )
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Checks whether an entry belongs in an encyclopedia tab.
        /// </summary>
        /// <param name="entry">The entry to check.</param>
        /// <param name="tab">The encyclopedia tab index.</param>
        /// <returns>True when the entry belongs in the tab.</returns>
        private static bool IsInTab(EncyclopediaEntry entry, int tab)
        {
            return tab == 0
                || tab == 1 && entry.Category == EncyclopediaEntryCategory.System
                || tab == 2 && entry.Category == EncyclopediaEntryCategory.Ship
                || tab == 3 && entry.Category == EncyclopediaEntryCategory.Facility
                || tab == 4 && entry.Category == EncyclopediaEntryCategory.Mission
                || tab == 5 && entry.Category == EncyclopediaEntryCategory.Troop
                || tab == 6 && entry.Category == EncyclopediaEntryCategory.Personnel;
        }

        /// <summary>
        /// Checks whether an entry can be viewed by a faction.
        /// </summary>
        /// <param name="entry">The entry to check.</param>
        /// <param name="factionInstanceId">The faction instance ID viewing the entry.</param>
        /// <returns>True when the entry can be viewed by the faction.</returns>
        private static bool IsVisibleToFaction(EncyclopediaEntry entry, string factionInstanceId)
        {
            return string.IsNullOrEmpty(entry.VisibleFactionInstanceID)
                || string.Equals(
                    entry.VisibleFactionInstanceID,
                    factionInstanceId,
                    StringComparison.Ordinal
                );
        }
    }

    /// <summary>
    /// Builds encyclopedia catalogs from authored entries and static game data.
    /// </summary>
    public sealed class EncyclopediaCatalogBuilder
    {
        /// <summary>
        /// Builds the encyclopedia catalog from resource data.
        /// </summary>
        /// <returns>The built encyclopedia catalog.</returns>
        public EncyclopediaCatalog Build()
        {
            return Build(
                ResourceManager.GetData<EncyclopediaEntries>(),
                ResourceManager.GetEntityData<PlanetSystem>(),
                ResourceManager.GetEntityData<Building>(),
                ResourceManager.GetEntityData<CapitalShip>(),
                ResourceManager.GetEntityData<Starfighter>(),
                ResourceManager.GetEntityData<Regiment>(),
                ResourceManager.GetEntityData<SpecialForces>(),
                ResourceManager.GetEntityData<Officer>()
            );
        }

        /// <summary>
        /// Builds the encyclopedia catalog from authored entries and static game data.
        /// </summary>
        /// <param name="authoredEntries">The authored encyclopedia entries.</param>
        /// <param name="planetSystems">The static planet system data.</param>
        /// <param name="buildings">The static building data.</param>
        /// <param name="capitalShips">The static capital ship data.</param>
        /// <param name="starfighters">The static starfighter data.</param>
        /// <param name="regiments">The static regiment data.</param>
        /// <param name="specialForces">The static special forces data.</param>
        /// <param name="officers">The static officer data.</param>
        /// <returns>The built encyclopedia catalog.</returns>
        public EncyclopediaCatalog Build(
            EncyclopediaEntries authoredEntries,
            IEnumerable<PlanetSystem> planetSystems,
            IEnumerable<Building> buildings,
            IEnumerable<CapitalShip> capitalShips,
            IEnumerable<Starfighter> starfighters,
            IEnumerable<Regiment> regiments,
            IEnumerable<SpecialForces> specialForces,
            IEnumerable<Officer> officers
        )
        {
            List<EncyclopediaEntry> generatedEntries = BuildGeneratedEntries(
                planetSystems,
                buildings,
                capitalShips,
                starfighters,
                regiments,
                specialForces,
                officers
            );
            Dictionary<string, EncyclopediaEntry> generatedByTypeId = generatedEntries
                .Where(entry => entry != null && !string.IsNullOrEmpty(entry.TypeID))
                .GroupBy(entry => entry.TypeID)
                .ToDictionary(group => group.Key, group => group.First());
            List<EncyclopediaEntry> mergedEntries = new List<EncyclopediaEntry>();

            if (authoredEntries != null)
            {
                foreach (EncyclopediaEntry authoredEntry in authoredEntries)
                {
                    if (authoredEntry == null)
                        continue;

                    if (
                        string.IsNullOrEmpty(authoredEntry.TypeID)
                        || !generatedByTypeId.ContainsKey(authoredEntry.TypeID)
                    )
                    {
                        mergedEntries.Add(Clone(authoredEntry));
                    }
                }
            }

            foreach (EncyclopediaEntry generatedEntry in generatedEntries)
            {
                if (generatedEntry != null)
                    mergedEntries.Add(generatedEntry);
            }

            return new EncyclopediaCatalog(mergedEntries);
        }

        /// <summary>
        /// Builds generated encyclopedia entries from static game data.
        /// </summary>
        /// <param name="planetSystems">The static planet system data.</param>
        /// <param name="buildings">The static building data.</param>
        /// <param name="capitalShips">The static capital ship data.</param>
        /// <param name="starfighters">The static starfighter data.</param>
        /// <param name="regiments">The static regiment data.</param>
        /// <param name="specialForces">The static special forces data.</param>
        /// <param name="officers">The static officer data.</param>
        /// <returns>The generated encyclopedia entries.</returns>
        private static List<EncyclopediaEntry> BuildGeneratedEntries(
            IEnumerable<PlanetSystem> planetSystems,
            IEnumerable<Building> buildings,
            IEnumerable<CapitalShip> capitalShips,
            IEnumerable<Starfighter> starfighters,
            IEnumerable<Regiment> regiments,
            IEnumerable<SpecialForces> specialForces,
            IEnumerable<Officer> officers
        )
        {
            List<EncyclopediaEntry> entries = new List<EncyclopediaEntry>();
            AddPlanets(entries, planetSystems);
            AddEntities(entries, buildings, EncyclopediaEntryCategory.Facility);
            AddEntities(entries, capitalShips, EncyclopediaEntryCategory.Ship);
            AddEntities(entries, starfighters, EncyclopediaEntryCategory.Ship);
            AddEntities(entries, regiments, EncyclopediaEntryCategory.Troop);
            AddEntities(entries, specialForces, EncyclopediaEntryCategory.Troop);
            AddEntities(entries, officers, EncyclopediaEntryCategory.Personnel);
            return entries;
        }

        /// <summary>
        /// Adds planet entries from planet system data.
        /// </summary>
        /// <param name="entries">The entry accumulator.</param>
        /// <param name="planetSystems">The static planet system data.</param>
        private static void AddPlanets(
            List<EncyclopediaEntry> entries,
            IEnumerable<PlanetSystem> planetSystems
        )
        {
            if (planetSystems == null)
                return;

            foreach (PlanetSystem planetSystem in planetSystems)
            {
                if (planetSystem?.Planets == null)
                    continue;

                foreach (Planet planet in planetSystem.Planets)
                {
                    EncyclopediaEntry entry = CreateEntry(planet, EncyclopediaEntryCategory.System);
                    if (entry != null)
                        entries.Add(entry);
                }
            }
        }

        /// <summary>
        /// Adds entity entries for one encyclopedia category.
        /// </summary>
        /// <typeparam name="T">The entity type being added.</typeparam>
        /// <param name="entries">The entry accumulator.</param>
        /// <param name="entities">The entities to add.</param>
        /// <param name="category">The encyclopedia category for the entities.</param>
        private static void AddEntities<T>(
            List<EncyclopediaEntry> entries,
            IEnumerable<T> entities,
            EncyclopediaEntryCategory category
        )
            where T : BaseGameEntity
        {
            if (entities == null)
                return;

            foreach (T entity in entities)
            {
                EncyclopediaEntry entry = CreateEntry(entity, category);
                if (entry != null)
                    entries.Add(entry);
            }
        }

        /// <summary>
        /// Creates an encyclopedia entry from a game entity.
        /// </summary>
        /// <param name="entity">The entity to convert.</param>
        /// <param name="category">The encyclopedia category for the entity.</param>
        /// <returns>The created entry, or null when no entity was provided.</returns>
        private static EncyclopediaEntry CreateEntry(
            BaseGameEntity entity,
            EncyclopediaEntryCategory category
        )
        {
            if (entity == null)
                return null;

            string imagePath = entity.DisplayImagePath;
            if (entity is Planet planet && string.IsNullOrEmpty(imagePath))
                imagePath = planet.PlanetIconPath;

            return new EncyclopediaEntry
            {
                TypeID = entity.TypeID,
                DisplayName = entity.DisplayName,
                Category = category,
                OwnerInstanceID = GetEncyclopediaOwnerInstanceID(entity),
                ImagePath = GetEncyclopediaImagePath(entity, imagePath),
                Stats = CloneStats(entity.EncyclopediaStats),
                Description = GetEncyclopediaDescription(entity),
            };
        }

        /// <summary>
        /// Gets the owner ID to associate with an entity encyclopedia entry.
        /// </summary>
        /// <param name="entity">The entity whose encyclopedia owner is being resolved.</param>
        /// <returns>The owner ID, or null when the entry is not faction-owned.</returns>
        private static string GetEncyclopediaOwnerInstanceID(BaseGameEntity entity)
        {
            if (entity is not ISceneNode node)
                return null;

            string ownerInstanceId = node.GetOwnerInstanceID();
            if (!string.IsNullOrEmpty(ownerInstanceId))
                return ownerInstanceId;

            return node.AllowedOwnerInstanceIDs?.Count == 1
                ? node.AllowedOwnerInstanceIDs[0]
                : null;
        }

        /// <summary>
        /// Gets the image path used by the encyclopedia for an entity.
        /// </summary>
        /// <param name="entity">The entity whose encyclopedia image is being resolved.</param>
        /// <param name="fallbackImagePath">The image path to use when the entity has no encyclopedia image.</param>
        /// <returns>The encyclopedia image path.</returns>
        private static string GetEncyclopediaImagePath(
            BaseGameEntity entity,
            string fallbackImagePath
        )
        {
            return string.IsNullOrEmpty(entity.EncyclopediaImagePath)
                ? fallbackImagePath
                : entity.EncyclopediaImagePath;
        }

        /// <summary>
        /// Gets the text body used by the encyclopedia for an entity.
        /// </summary>
        /// <param name="entity">The entity whose encyclopedia description is being resolved.</param>
        /// <returns>The encyclopedia description.</returns>
        private static string GetEncyclopediaDescription(BaseGameEntity entity)
        {
            return string.IsNullOrEmpty(entity.EncyclopediaDescription)
                ? entity.Description
                : entity.EncyclopediaDescription;
        }

        /// <summary>
        /// Creates a copy of an encyclopedia entry.
        /// </summary>
        /// <param name="entry">The entry to copy.</param>
        /// <returns>The copied entry, or null when no entry was provided.</returns>
        private static EncyclopediaEntry Clone(EncyclopediaEntry entry)
        {
            if (entry == null)
                return null;

            return new EncyclopediaEntry
            {
                TypeID = entry.TypeID,
                DisplayName = entry.DisplayName,
                Category = entry.Category,
                VisibleFactionInstanceID = entry.VisibleFactionInstanceID,
                OwnerInstanceID = entry.OwnerInstanceID,
                ImagePath = entry.ImagePath,
                Stats = CloneStats(entry.Stats),
                Description = entry.Description,
            };
        }

        /// <summary>
        /// Creates copies of encyclopedia stat rows.
        /// </summary>
        /// <param name="stats">The stat rows to copy.</param>
        /// <returns>The copied stat rows.</returns>
        private static List<EncyclopediaEntryStat> CloneStats(List<EncyclopediaEntryStat> stats)
        {
            List<EncyclopediaEntryStat> clonedStats = new List<EncyclopediaEntryStat>();
            if (stats == null)
                return clonedStats;

            foreach (EncyclopediaEntryStat stat in stats)
            {
                if (stat == null)
                    continue;

                clonedStats.Add(
                    new EncyclopediaEntryStat { Label = stat.Label, Value = stat.Value }
                );
            }

            return clonedStats;
        }
    }
}
