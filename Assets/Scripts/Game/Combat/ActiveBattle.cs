using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    public enum BattleKind
    {
        Space,
        Ground,
    }

    /// <summary>
    /// Persistent state of the tactical battle currently in progress.
    /// </summary>
    [PersistableObject]
    public sealed class ActiveBattle
    {
        public BattleKind Kind { get; set; }
        public string PlanetInstanceId { get; set; }
        public Dictionary<string, List<CombatUnit>> Combatants { get; set; } =
            new Dictionary<string, List<CombatUnit>>();

        /// <summary>
        /// Adds independently mutable battle copies of a strategic unit under its current owner.
        /// A capital ship produces one combatant and a starfighter squadron produces one combatant
        /// for each currently surviving fighter.
        /// </summary>
        /// <param name="sourceUnit">The strategic unit entering the battle.</param>
        /// <returns>The battle copies.</returns>
        public IReadOnlyList<CombatUnit> AddCombatants(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (string.IsNullOrWhiteSpace(sourceUnit.OwnerInstanceID))
                throw new ArgumentException(
                    "A combatant must have an owner before entering battle.",
                    nameof(sourceUnit)
                );

            int combatantCount = sourceUnit is Starfighter starfighter
                ? Math.Max(0, starfighter.CurrentSquadronSize)
                : 1;
            List<CombatUnit> addedCombatants = new List<CombatUnit>(combatantCount);
            if (combatantCount == 0)
                return addedCombatants.AsReadOnly();

            if (
                !Combatants.TryGetValue(
                    sourceUnit.OwnerInstanceID,
                    out List<CombatUnit> factionCombatants
                )
            )
            {
                factionCombatants = new List<CombatUnit>();
                Combatants.Add(sourceUnit.OwnerInstanceID, factionCombatants);
            }

            for (int combatantIndex = 0; combatantIndex < combatantCount; combatantIndex++)
            {
                CombatUnit combatUnit = CombatUnit.Create(sourceUnit);
                factionCombatants.Add(combatUnit);
                addedCombatants.Add(combatUnit);
            }

            return addedCombatants.AsReadOnly();
        }
    }
}
