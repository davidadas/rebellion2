using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Stores one faction's units participating in an active battle.
    /// </summary>
    [PersistableObject]
    public sealed class BattleParticipant
    {
        [PersistableMember(Name = nameof(Combatants))]
        private List<CombatUnit> _combatants = new List<CombatUnit>();

        public string FactionInstanceID { get; set; }

        [PersistableIgnore]
        public IReadOnlyList<CombatUnit> Combatants => _combatants;

        /// <summary>
        /// Adds a detached combat unit to this participant.
        /// </summary>
        /// <param name="combatant">The combat unit to add.</param>
        internal void AddCombatant(CombatUnit combatant) => _combatants.Add(combatant);
    }

    /// <summary>
    /// Persistent state for a battle that can continue independently of the campaign scene graph.
    /// </summary>
    [PersistableObject]
    public sealed class ActiveBattle
    {
        [PersistableMember(Name = nameof(Participants))]
        private List<BattleParticipant> _participants = new List<BattleParticipant>();

        public BattleKind Kind { get; set; }
        public string PlanetInstanceID { get; set; }

        [PersistableIgnore]
        public IReadOnlyList<BattleParticipant> Participants => _participants;

        /// <summary>
        /// Adds an independently mutable copy of a strategic unit under its current owner.
        /// </summary>
        /// <param name="sourceUnit">The campaign unit entering battle.</param>
        /// <returns>The detached combat unit, or null for a destroyed squadron.</returns>
        public CombatUnit AddCombatant(BaseSceneNode sourceUnit)
        {
            if (sourceUnit == null)
                throw new ArgumentNullException(nameof(sourceUnit));
            if (string.IsNullOrWhiteSpace(sourceUnit.OwnerInstanceID))
                throw new ArgumentException(
                    "A combatant must have an owner before entering battle.",
                    nameof(sourceUnit)
                );
            if (Kind != BattleKind.Space)
                throw new InvalidOperationException(
                    "Capital ships and starfighters can only enter space battles."
                );
            if (sourceUnit is not CapitalShip && sourceUnit is not Starfighter)
                throw new ArgumentException(
                    "Only capital ships and starfighters can enter a space battle.",
                    nameof(sourceUnit)
                );
            if (sourceUnit is Starfighter { CurrentSquadronSize: <= 0 })
                return null;

            BattleParticipant participant = _participants.Find(candidate =>
                candidate.FactionInstanceID == sourceUnit.OwnerInstanceID
            );
            if (participant == null)
            {
                participant = new BattleParticipant
                {
                    FactionInstanceID = sourceUnit.OwnerInstanceID,
                };
                _participants.Add(participant);
            }

            CombatUnit combatant = CombatUnit.Create(sourceUnit);
            participant.AddCombatant(combatant);
            return combatant;
        }

        /// <summary>
        /// Finds a detached combatant by the stable campaign unit that produced it.
        /// </summary>
        /// <param name="sourceUnitInstanceID">The campaign unit identity.</param>
        /// <returns>The matching combatant, or null when it is not in this battle.</returns>
        public CombatUnit GetCombatant(string sourceUnitInstanceID)
        {
            if (string.IsNullOrEmpty(sourceUnitInstanceID))
                return null;
            foreach (BattleParticipant participant in _participants)
            foreach (CombatUnit combatant in participant.Combatants)
                if (
                    string.Equals(
                        combatant.SourceUnitInstanceID,
                        sourceUnitInstanceID,
                        StringComparison.Ordinal
                    )
                )
                    return combatant;
            return null;
        }
    }
}
