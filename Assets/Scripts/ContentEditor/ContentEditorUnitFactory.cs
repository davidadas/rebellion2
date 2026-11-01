using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

/// <summary>
/// Creates empty authored unit definitions without copying runtime state or existing units.
/// </summary>
internal static class ContentEditorUnitFactory
{
    /// <summary>
    /// Identifies the unit definitions supported by the authoring forms.
    /// </summary>
    /// <param name="kind">The serialized record name.</param>
    /// <returns>True for an editable unit category.</returns>
    internal static bool CanCreate(string kind)
    {
        return kind
            is "Officer"
                or "SpecialForce"
                or "Regiment"
                or "CapitalShip"
                or "Starfighter"
                or "Building";
    }

    /// <summary>
    /// Creates a definition with empty artwork, zero ratings, and no runtime assignments.
    /// </summary>
    /// <param name="kind">The serialized record name.</param>
    /// <returns>The detached unit definition.</returns>
    internal static XElement Create(string kind)
    {
        if (!CanCreate(kind))
            throw new ArgumentException("Choose a unit category.", nameof(kind));
        XElement unit = new XElement(
            kind,
            new XElement(
                "DisplayName",
                "New " + ContentPackEditorField.FormatName(kind).ToLowerInvariant()
            ),
            new[]
            {
                "DisplayImagePath",
                "SmallDisplayImagePath",
                "MessageImagePath",
                "EncyclopediaImagePath",
                "InTransitImagePath",
                "InTransitSmallImagePath",
                "DamagedImagePath",
                "DamagedSmallImagePath",
                "InjuredImagePath",
                "CapturedOverlayImagePath",
                "Description",
                "EncyclopediaDescription",
            }.Select(name => new XElement(name))
        );
        if (kind == "Officer")
        {
            unit.Add(
                new XElement("IsMain", false),
                new XElement("IsRecruitable", true),
                new XElement("CanBetray", true),
                new XElement("Loyalty", 100),
                new XElement("RecruitingFactionInstanceIDs"),
                Ratings(),
                new XElement("VoiceSet", new XElement("Order", new XElement("Path")))
            );
            return unit;
        }
        unit.Add(
            new XElement("ManufacturingFactionInstanceIDs"),
            Zeroes(
                "ConstructionCost",
                "MaintenanceCost",
                "BaseBuildSpeed",
                "ResearchOrder",
                "ResearchDifficulty"
            )
        );
        switch (kind)
        {
            case "Starfighter":
                unit.Add(
                    new XElement("MaxSquadronSize", 1),
                    new XElement("CurrentSquadronSize", 1),
                    Zeroes(
                        nameof(Starfighter.DetectionRating),
                        nameof(Starfighter.Bombardment),
                        nameof(Starfighter.ShieldStrength),
                        nameof(Starfighter.Hyperdrive),
                        nameof(Starfighter.SublightSpeed),
                        nameof(Starfighter.Agility),
                        nameof(Starfighter.LaserCannon),
                        nameof(Starfighter.IonCannon),
                        nameof(Starfighter.Torpedoes),
                        nameof(Starfighter.LaserRange),
                        nameof(Starfighter.IonRange),
                        nameof(Starfighter.TorpedoRange),
                        nameof(Starfighter.UprisingDefense)
                    )
                );
                break;
            case "CapitalShip":
                unit.Add(
                    new XElement("MaxHullStrength", 1),
                    new XElement("CurrentHullStrength", 1),
                    Zeroes(
                        nameof(CapitalShip.MaxShieldStrength),
                        nameof(CapitalShip.ShieldRechargeRate),
                        nameof(CapitalShip.DamageControl),
                        nameof(CapitalShip.Hyperdrive),
                        nameof(CapitalShip.SublightSpeed),
                        nameof(CapitalShip.Maneuverability),
                        nameof(CapitalShip.StarfighterCapacity),
                        nameof(CapitalShip.RegimentCapacity),
                        nameof(CapitalShip.DetectionRating),
                        nameof(CapitalShip.WeaponRecharge),
                        nameof(CapitalShip.Bombardment),
                        nameof(CapitalShip.TractorBeamPower),
                        nameof(CapitalShip.TractorBeamnRange),
                        nameof(CapitalShip.SectorSuppression),
                        nameof(CapitalShip.UprisingDefense)
                    ),
                    new XElement("HasGravityWell", false),
                    new XElement("CanDestroyPlanets", false),
                    new XElement("Roles"),
                    new XElement("ShipNamePoolID"),
                    Serialize("PrimaryWeapons", new CapitalShip().PrimaryWeapons)
                );
                break;
            case "Regiment":
                unit.Add(
                    Zeroes(
                        nameof(Regiment.AttackRating),
                        nameof(Regiment.DefenseRating),
                        nameof(Regiment.DetectionRating),
                        nameof(Regiment.BombardmentDefense),
                        nameof(Regiment.UprisingDefense)
                    )
                );
                break;
            case "SpecialForce":
                unit.Add(Ratings(), new XElement("AllowedMissionTypeIDs"));
                break;
            case "Building":
                unit.Add(
                    new XElement("BuildingType", BuildingType.None),
                    new XElement("ProductionType", ManufacturingType.None),
                    Zeroes(
                        nameof(Building.ProcessRate),
                        nameof(Building.Bombardment),
                        nameof(Building.WeaponStrength),
                        nameof(Building.ShieldStrength),
                        nameof(Building.WeaponPower),
                        nameof(Building.ProductionModifier)
                    ),
                    new XElement("DefenseWeaponEffect", DefenseWeaponEffect.HullDamage),
                    new XElement("IsDetectionBlocker", false),
                    new XElement("ProtectedUnitTypeIDs"),
                    new XElement("Upgrades")
                );
                break;
        }
        if (kind is "Starfighter" or "CapitalShip")
            unit.Add(
                new[]
                {
                    "BattleResultImagePath",
                    "BattleResultInTransitImagePath",
                    "BattleResultDamagedImagePath",
                }.Select(name => new XElement(name))
            );
        return unit;
    }

    /// <summary>
    /// Creates editable zero-valued scalar members.
    /// </summary>
    /// <param name="names">The serialized numeric member names.</param>
    /// <returns>The initialized numeric fields.</returns>
    private static IEnumerable<XElement> Zeroes(params string[] names)
    {
        return names.Select(name => new XElement(name, 0));
    }

    /// <summary>
    /// Creates the four mission skill controls without inheriting another unit's skills.
    /// </summary>
    /// <returns>The initialized rating dictionary.</returns>
    private static XElement Ratings()
    {
        return Serialize("Ratings", new SpecialForces().Ratings);
    }

    /// <summary>
    /// Uses the engine serializer for structured fields such as weapon arcs and ratings.
    /// </summary>
    /// <param name="name">The serialized member name.</param>
    /// <param name="value">The new, independent default value.</param>
    /// <returns>The authored member element.</returns>
    private static XElement Serialize(string name, object value)
    {
        using StringWriter writer = new StringWriter();
        new GameSerializer(
            value.GetType(),
            new GameSerializerSettings { RootName = name }
        ).Serialize(writer, value);
        return XElement.Parse(writer.ToString());
    }
}
