using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines a typed scalar source for an event-local binding.
    /// </summary>
    [PersistableObject]
    public abstract class GameEventBindingSource { }

    /// <summary>
    /// Resolves one officer's effective authored rating.
    /// </summary>
    [PersistableObject(Name = "SkillRating")]
    public sealed class SkillRatingBindingSource : GameEventBindingSource
    {
        // Officer.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string OfficerBinding { get; set; }

        // Rating.
        [PersistableAttribute]
        public SkillRating Rating { get; set; }
    }

    /// <summary>
    /// Resolves one officer's current effective Force value.
    /// </summary>
    [PersistableObject(Name = "OfficerForce")]
    public sealed class OfficerForceBindingSource : GameEventBindingSource
    {
        // Officer.
        [PersistableAttribute]
        public string OfficerInstanceID { get; set; }

        [PersistableAttribute]
        public string OfficerBinding { get; set; }
    }

    /// <summary>
    /// Resolves one authored statistic from a planet.
    /// </summary>
    [PersistableObject(Name = "PlanetStat")]
    public sealed class PlanetStatBindingSource : GameEventBindingSource
    {
        // Planet.
        [PersistableAttribute]
        public string PlanetInstanceID { get; set; }

        [PersistableAttribute]
        public string PlanetBinding { get; set; }

        // Statistic.
        [PersistableAttribute]
        public PlanetStat Stat { get; set; }
    }

    /// <summary>
    /// Resolves the number of distinct scene nodes returned by authored selectors.
    /// </summary>
    [PersistableObject(Name = "SelectionCount")]
    public sealed class SelectionCountBindingSource : GameEventBindingSource
    {
        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
    }

    /// <summary>
    /// Assigns an explicitly selected value to an event-local binding name.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventBinding
    {
        // Binding Configuration.
        [PersistableAttribute]
        public string Argument { get; set; }

        [PersistableAttribute]
        public string As { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();
        public RollInteger RollInteger { get; set; }
        public RollDouble RollDouble { get; set; }

        [PersistableInlineCollection]
        public List<GameEventBindingSource> Sources { get; set; } =
            new List<GameEventBindingSource>();
    }
}
