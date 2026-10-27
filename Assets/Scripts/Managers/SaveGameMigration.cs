using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

/// <summary>
/// Upgrades older saved battle records before the current game model is deserialized.
/// </summary>
internal static class SaveGameMigration
{
    /// <summary>
    /// Reads current saves directly and upgrades the first save format's weapon and battle-unit records.
    /// </summary>
    /// <param name="stream">The seekable saved-game stream.</param>
    /// <param name="serializer">The configured game-root deserializer.</param>
    /// <returns>The restored game state.</returns>
    public static GameRoot Deserialize(Stream stream, GameSerializer serializer)
    {
        long position = stream.Position;
        int version = ReadVersion(stream);
        stream.Position = position;
        if (version >= 2)
            return (GameRoot)serializer.Deserialize(stream);

        XmlDocument document = new XmlDocument();
        document.Load(stream);
        UpgradeWeapons(document);
        UpgradeProjectiles(document);
        UpgradeCombatants(document);
        XmlNode versionNode = document.SelectSingleNode("/Game/Metadata/SaveVersion");
        if (versionNode != null)
            versionNode.InnerText = GameMetadata.CurrentSaveVersion.ToString(
                CultureInfo.InvariantCulture
            );
        return (GameRoot)serializer.Deserialize(document);
    }

    /// <summary>
    /// Reads only the saved metadata before choosing whether a document upgrade is required.
    /// </summary>
    /// <param name="stream">The saved-game stream whose position the caller will restore.</param>
    /// <returns>The recorded format version, or the first format for saves without version metadata.</returns>
    private static int ReadVersion(Stream stream)
    {
        using XmlReader reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                CloseInput = false,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            }
        );
        if (!reader.ReadToFollowing("Metadata"))
            return 1;
        using XmlReader metadata = reader.ReadSubtree();
        return metadata.ReadToFollowing("SaveVersion") ? metadata.ReadElementContentAsInt() : 1;
    }

    /// <summary>
    /// Moves shot properties out of the older flat weapon definition.
    /// </summary>
    /// <param name="document">The saved-game document being upgraded.</param>
    private static void UpgradeWeapons(XmlDocument document)
    {
        foreach (
            XmlElement weapon in document
                .GetElementsByTagName("Weapon")
                .Cast<XmlElement>()
                .ToArray()
        )
        {
            if (weapon["Projectile"] != null)
                continue;
            XmlElement projectile = document.CreateElement("Projectile");
            foreach (
                (string oldName, string newName) in new[]
                {
                    ("DeliveryMode", "DeliveryMode"),
                    ("Damage", "Damage"),
                    ("ProjectileSpeed", "Speed"),
                    ("ProjectileLength", "Length"),
                    ("ProjectileWidth", "Width"),
                    ("EffectDurationSeconds", "EffectDurationSeconds"),
                    ("ImpactSoundPaths", "ImpactSoundPaths"),
                }
            )
            {
                XmlElement oldValue = weapon[oldName];
                if (oldValue == null)
                    continue;
                XmlElement newValue = document.CreateElement(newName);
                newValue.InnerXml = oldValue.InnerXml;
                projectile.AppendChild(newValue);
                weapon.RemoveChild(oldValue);
            }
            string color = "#";
            foreach (string channel in new[] { "Red", "Green", "Blue" })
            {
                XmlElement value = weapon[channel];
                double amount =
                    value == null
                        ? 0d
                        : double.Parse(value.InnerText, CultureInfo.InvariantCulture);
                int component = Math.Clamp(
                    (int)Math.Round(amount * 255d, MidpointRounding.AwayFromZero),
                    0,
                    255
                );
                color += component.ToString("X2", CultureInfo.InvariantCulture);
                if (value != null)
                    weapon.RemoveChild(value);
            }
            XmlElement colorValue = document.CreateElement("ColorHex");
            colorValue.InnerText = color;
            projectile.AppendChild(colorValue);
            weapon.AppendChild(projectile);
        }
    }

    /// <summary>
    /// Replaces an emitted shot's copied gun with the shot properties and range it requires.
    /// </summary>
    /// <param name="document">The saved-game document being upgraded.</param>
    private static void UpgradeProjectiles(XmlDocument document)
    {
        foreach (
            XmlElement shot in document
                .GetElementsByTagName("BattleProjectile")
                .Cast<XmlElement>()
                .ToArray()
        )
        {
            XmlElement lifetime = shot["RemainingEffectSeconds"];
            if (lifetime != null)
                shot.RemoveChild(lifetime);
            XmlElement weapon = shot["Weapon"];
            if (weapon == null)
                continue;
            shot.AppendChild(weapon["Projectile"].CloneNode(true));
            if (weapon["Range"] != null)
                shot.AppendChild(weapon["Range"].CloneNode(true));
            shot.RemoveChild(weapon);
            if (shot["Projectile"]?["DeliveryMode"]?.InnerText == "Hitscan")
                shot.ParentNode.RemoveChild(shot);
        }
    }

    /// <summary>
    /// Rebuilds older battle copies as combat-only capabilities and grouped mutable state.
    /// </summary>
    /// <param name="document">The saved-game document being upgraded.</param>
    private static void UpgradeCombatants(XmlDocument document)
    {
        foreach (
            XmlElement oldUnit in document
                .GetElementsByTagName("CombatUnit")
                .Cast<XmlElement>()
                .ToArray()
        )
        {
            XmlElement ship = oldUnit["CapitalShip"] ?? oldUnit["Starfighter"];
            if (ship == null)
                continue;
            XmlDocument shipDocument = new XmlDocument();
            shipDocument.AppendChild(shipDocument.ImportNode(ship, true));
            Type type = ship.Name == "CapitalShip" ? typeof(CapitalShip) : typeof(Starfighter);
            BaseSceneNode source = (BaseSceneNode)
                new GameSerializer(type).Deserialize(shipDocument);
            CombatUnit combatant = CombatUnit.Create(source);
            combatant.InstanceID = oldUnit["InstanceID"]?.InnerText ?? combatant.InstanceID;
            combatant.SourceUnitInstanceID =
                oldUnit["SourceUnitInstanceID"]?.InnerText ?? source.InstanceID;
            combatant.Condition.CurrentShieldStrength = null;
            XmlDocument upgraded = new XmlDocument();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                new GameSerializer(typeof(CombatUnit)).Serialize(output, combatant);
                upgraded.LoadXml(output.ToString());
            }
            XmlElement replacement = (XmlElement)
                document.ImportNode(upgraded.DocumentElement, true);
            foreach (string name in new[] { "HasRetreated", "Position", "Forward" })
                CopyValue(oldUnit, replacement, name, name);
            foreach (
                string name in new[]
                {
                    "CurrentShieldStrength",
                    "ShieldRechargeRemainder",
                    "HullDamageRemainder",
                    "ShieldDamageRemainder",
                }
            )
                CopyValue(oldUnit, replacement["Condition"], name, name);
            CopyValue(oldUnit, replacement["MoveOrder"], "MoveDestination", "Destination");
            CopyValue(oldUnit, replacement["MoveOrder"], "MoveSpeedLimit", "SpeedLimit");
            CopyValue(
                oldUnit,
                replacement["Weapons"],
                "AttackTargetInstanceID",
                "AttackTargetInstanceID"
            );
            CopyValue(oldUnit, replacement["Weapons"], "WeaponBanks", "Banks");
            CopyValue(oldUnit, replacement["Flight"], "FlightState", "Phase");
            foreach (
                string name in new[]
                {
                    "CarrierSourceUnitInstanceID",
                    "FlightBayIndex",
                    "FlightWaypointIndex",
                    "FlightSegmentDistance",
                    "FlightDelay",
                    "RecallRequested",
                    "LaunchFormationOffset",
                }
            )
                CopyValue(oldUnit, replacement["Flight"], name, name);
            foreach (string name in new[] { "LaunchQueue", "LaunchCountdown" })
                CopyValue(oldUnit, replacement["Carrier"], name, name);
            oldUnit.ParentNode.ReplaceChild(replacement, oldUnit);
        }
    }

    /// <summary>
    /// Preserves one recorded value under its current containing state and element name.
    /// </summary>
    /// <param name="source">The older containing element.</param>
    /// <param name="destination">The current containing state element.</param>
    /// <param name="oldName">The older element name.</param>
    /// <param name="newName">The current element name.</param>
    private static void CopyValue(
        XmlElement source,
        XmlElement destination,
        string oldName,
        string newName
    )
    {
        XmlElement value = source[oldName];
        if (value == null)
            return;
        XmlElement existing = destination[newName];
        if (existing != null)
            destination.RemoveChild(existing);
        XmlElement copy = destination.OwnerDocument.CreateElement(newName);
        copy.InnerXml = value.InnerXml;
        destination.AppendChild(copy);
    }
}
