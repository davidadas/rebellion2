using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game;
using Rebellion.Util.Serialization;

/// <summary>
/// Creates independent starting-setup entries using their declared data models.
/// </summary>
internal static class ContentEditorGenerationFactory
{
    /// <summary>
    /// Creates an empty entry, retaining model defaults and usable creation quantities.
    /// </summary>
    /// <param name="type">The declared collection item type.</param>
    /// <returns>The detached serialized entry.</returns>
    internal static XElement Create(Type type)
    {
        object value = type.Name switch
        {
            nameof(StartingOfficerRule) => new StartingOfficerRule
            {
                OfficerInstanceID = string.Empty,
                GalaxySizes = null,
            },
            nameof(FactionSetup) => new FactionSetup
            {
                FactionID = string.Empty,
                NumStartingOfficers = new(),
                StartingPlanets = new(),
            },
            nameof(StartingPlanet) => new StartingPlanet
            {
                PlanetTypeID = null,
                VisibleToFactionIDs = null,
            },
            nameof(FactionBucketConfig) => new FactionBucketConfig
            {
                FactionID = string.Empty,
                Overrides = null,
            },
            nameof(FactionBucketOverride) => new FactionBucketOverride(),
            nameof(WeightedFacilityEntry) => new WeightedFacilityEntry(),
            nameof(HQFacilityLoadout) => new HQFacilityLoadout
            {
                FactionID = null,
                PlanetTypeID = GameGenerationConfig.FactionHqSentinel,
                FacilityTypeIDs = new(),
            },
            nameof(FixedGarrison) => new FixedGarrison
            {
                FactionID = string.Empty,
                PlanetTypeID = string.Empty,
                Units = new(),
            },
            nameof(FixedFleet) => new FixedFleet
            {
                FactionID = string.Empty,
                PlanetTypeID = string.Empty,
                SpawnChancePct = 100,
                TargetPlanets = null,
                ShipEntries = new(),
            },
            nameof(FixedFleetShip) => new FixedFleetShip
            {
                TypeID = string.Empty,
                Count = 1,
                Cargo = null,
            },
            nameof(FactionBudget) => new FactionBudget
            {
                FactionID = string.Empty,
                BudgetLevels = new(),
                UnitTable = new(),
                Bonuses = null,
            },
            nameof(WeightedUnitEntry) => new WeightedUnitEntry { Weight = 1, Units = new() },
            nameof(StartingUnitBudgetBonus) => new StartingUnitBudgetBonus { BudgetLevels = new() },
            nameof(BudgetLevel) => new BudgetLevel(),
            nameof(PlanetResourceProfile) => new PlanetResourceProfile
            {
                CoreEnergy = new(),
                RimEnergy = new(),
                CoreRawMaterials = new(),
                RimRawMaterials = new(),
            },
            nameof(UnitEntry) => new UnitEntry { TypeID = string.Empty, Count = 1 },
            _ => throw new ArgumentException(
                "This collection cannot be extended here.",
                nameof(type)
            ),
        };
        using StringWriter writer = new();
        new GameSerializer(
            type,
            new GameSerializerSettings { RootName = SerializationMetadata.GetElementName(type) }
        ).Serialize(writer, value);
        return XElement.Parse(writer.ToString());
    }

    /// <summary>
    /// Attaches an optional setup member in declaration order and replaces mutually exclusive destinations.
    /// </summary>
    /// <param name="owner">The containing setup object.</param>
    /// <param name="element">The authored member being attached.</param>
    /// <param name="type">The containing serialized type.</param>
    internal static void AttachMember(XElement owner, XElement element, Type type)
    {
        string name = element.Name.LocalName;
        if (
            owner.Name.LocalName == "StartingOfficerRule"
            && name is "DestinationTypeID" or "DestinationInstanceID"
        )
            owner
                .Element(
                    name == "DestinationTypeID" ? "DestinationInstanceID" : "DestinationTypeID"
                )
                ?.Remove();
        if (owner.Name.LocalName == "FixedFleet" && name is "PlanetTypeID" or "TargetPlanets")
            owner.Element(name == "PlanetTypeID" ? "TargetPlanets" : "PlanetTypeID")?.Remove();
        if (element.Parent == owner)
            return;
        if (element.Parent != null)
            element.Remove();
        string[] members = type.GetProperties()
            .Where(property =>
                SerializationMetadata.GetElementType(type, property.Name) == property.PropertyType
            )
            .Select(property => property.Name)
            .ToArray();
        int index = Array.IndexOf(members, name);
        XElement following = owner
            .Elements()
            .FirstOrDefault(child => Array.IndexOf(members, child.Name.LocalName) > index);
        if (following == null)
            owner.Add(element);
        else
            following.AddBeforeSelf(element);
    }
}
