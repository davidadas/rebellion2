using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Content
{
    /// <summary>
    /// Provides the authored deployment and model geometry used by tactical battles.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatDefinition
    {
        public List<TacticalCombatFactionDefinition> Factions { get; set; } = new();
        public List<TacticalCombatHudImage> HudImages { get; set; } = new();
        public List<TacticalCombatUnitDefinition> Units { get; set; } = new();
        public List<TacticalCombatEffectDefinition> Effects { get; set; } = new();
        public List<TacticalCombatSoundDefinition> Sounds { get; set; } = new();
        public List<TacticalCombatNotificationDefinition> Notifications { get; set; } = new();
        public List<TacticalCombatEnvironmentDefinition> Environments { get; set; } = new();
        public List<string> CriticalPlanetInstanceIDs { get; set; } = new();
        public int WithdrawalDistancePercent { get; set; }
        public string BackgroundImagePath { get; set; }
        public string AttackRunSuccessMoviePath { get; set; }
        public string AttackRunFailureMoviePath { get; set; }
        public int FastSystemRepairTicks { get; set; }
        public int NormalSystemRepairTicks { get; set; }

        /// <summary>
        /// Finds the authored entry for a unit type.
        /// </summary>
        /// <param name="typeID">The unit's content type.</param>
        /// <returns>The matching entry, when present.</returns>
        public TacticalCombatUnitDefinition FindUnit(string typeID) =>
            Units?.Find(unit => unit.UnitTypeID == typeID);
    }

    /// <summary>
    /// Provides the sound variants associated with one combat event.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatSoundDefinition
    {
        public TacticalCombatSound Kind { get; set; }
        public List<TacticalCombatSoundClip> Variants { get; set; } = new();
    }

    /// <summary>
    /// Provides an addressed sound for battle audio playback.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatSoundClip
    {
        public string Path { get; set; }
    }

    /// <summary>
    /// Identifies an authored tactical impact animation.
    /// </summary>
    public enum TacticalCombatEffectKind
    {
        ShieldHit,
        HullHit,
        IonShieldHit,
        IonHullHit,
        TorpedoHullHit,
        Destruction,
        TractorRestraint,
        GravityLock,
    }

    /// <summary>
    /// Provides animation frames, drawing size, and the image's normalized anchor.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatEffectDefinition
    {
        public TacticalCombatEffectKind Kind { get; set; }
        public List<string> FramePaths { get; set; } = new();
        public float Size { get; set; }
        public float OriginX { get; set; }
        public float OriginY { get; set; }
    }

    /// <summary>
    /// Provides planet scenery and palette-dependent model textures for one planetary appearance.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatEnvironmentDefinition
    {
        public string PlanetIconPath { get; set; }
        public string PlanetImagePath { get; set; }
        public List<TacticalCombatTextureOverride> TextureOverrides { get; set; } = new();
    }

    /// <summary>
    /// Replaces one embedded model texture with its environment-specific palette variant.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatTextureOverride
    {
        public string TextureName { get; set; }
        public string ImagePath { get; set; }
    }

    /// <summary>
    /// Defines a faction's deployment orientation and battle presentation.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatFactionDefinition
    {
        public string FactionInstanceID { get; set; }
        public List<TacticalCombatHudImage> HudImages { get; set; } = new();
        public float ForwardZ { get; set; }
        public int FactionIndex { get; set; }
    }

    /// <summary>
    /// Provides the three fighter sprite detail levels, each ordered by friendly group and enemy.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatFighterSprites
    {
        public List<string> NearImages { get; set; } = new();
        public List<string> MiddleImages { get; set; } = new();
        public List<string> FarImages { get; set; } = new();
    }

    /// <summary>
    /// Associates a unit type with tactical geometry, model assets, and fighter grouping.
    /// These shared definitions are loaded from content rather than copied into saved units.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatUnitDefinition
    {
        public string UnitTypeID { get; set; }
        public int PresentationIndex { get; set; }
        public string ModelPath { get; set; }
        public List<string> ModelDetailPaths { get; set; } = new();
        public string BillboardImagePath { get; set; }
        public string ConstructionBillboardImagePath { get; set; }
        public TacticalCombatFighterSprites FighterSprites { get; set; }
        public float ModelScale { get; set; }
        public float HullWidth { get; set; }
        public float HullHeight { get; set; }
        public float HullDepth { get; set; }
        public float MaximumTurnRate { get; set; }
        public int BackupHyperdrive { get; set; }
        public int FighterGroup { get; set; }
        public bool SplitTurbolaserBeams { get; set; }

        // Earlier packs supplied equipment and physical defaults in the tactical catalog.
        /// <summary>
        /// Supplies ship-owned defaults from older tactical catalogs.
        /// </summary>
        /// <param name="ship">The capital ship receiving migrated defaults.</param>
        internal void MigrateCapitalShip(CapitalShip ship)
        {
            ship.MigrateBackupHyperdrive(BackupHyperdrive);
            bool largeHull = PresentationIndex == 21;
            ship.PhysicalGeometry ??= new CapitalShipGeometry
            {
                CollisionRadius = largeHull ? 19 : CapitalShipGeometry.DefaultCollisionRadius,
                MaximumTurnRate = largeHull ? (float)((double)1.5707964f * 0.033333335f) : 0,
                HullWidth = HullWidth,
                HullHeight = HullHeight,
                HullDepth = HullDepth,
            };
        }
    }

    /// <summary>
    /// Associates one semantic tactical image with a private content address.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatHudImage
    {
        public string Key { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    /// Addresses the ordered playback variants for one tactical notification and optional faction.
    /// An empty variant path records a resource that is unavailable in that faction's sound bank.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatNotificationDefinition
    {
        public int MessageID { get; set; }
        public string FactionInstanceID { get; set; }
        public List<TacticalCombatSoundClip> Variants { get; set; } = new();
    }

    /// <summary>
    /// Identifies a combat sound requested by a weapon, impact, or unit transition.
    /// </summary>
    public enum TacticalCombatSound
    {
        CapitalLaser,
        HeavyWeapon,
        CapitalIon,
        AllianceFighterLaser,
        EmpireFighterLaser,
        FighterIon,
        TractorEngaged,
        TractorReleased,
        TargetLocked,
        SmallExplosion,
        MediumExplosion,
        LargeExplosion,
        HeavyShieldHit,
        LaserShieldHit,
        IonShieldHit,
        HeavyHullHit,
        LaserHullHit,
        IonHullHit,
        Superlaser,
        FighterLaunch,
    }
}
