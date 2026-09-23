using System;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// An independent copy of one strategic unit participating in an active battle.
    /// </summary>
    [PersistableObject]
    public sealed class CombatUnit : BaseGameEntity
    {
        public string SourceUnitId { get; set; }
        public bool HasRetreated { get; set; }

        [PersistableInclude(typeof(CapitalShip))]
        [PersistableInclude(typeof(Starfighter))]
        public BaseSceneNode Unit { get; set; }

        /// <summary>
        /// Creates an independently mutable battle copy of a strategic ship.
        /// </summary>
        /// <param name="sourceUnit">The strategic unit entering battle.</param>
        /// <returns>The battle unit.</returns>
        public static CombatUnit Create(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );

            BaseSceneNode battleCopy = (BaseSceneNode)sourceUnit.CreateCopy();
            if (battleCopy is Starfighter starfighter)
            {
                starfighter.MaxSquadronSize = 1;
                starfighter.CurrentSquadronSize = 1;
            }

            return new CombatUnit { SourceUnitId = sourceUnit.InstanceID, Unit = battleCopy };
        }
    }
}
