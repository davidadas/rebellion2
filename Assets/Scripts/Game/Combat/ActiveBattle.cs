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
        [PersistableMember(Name = "Combatants")]
        private List<CombatUnit> _combatants = new List<CombatUnit>();

        public string FactionInstanceID { get; set; }
        public string BattleMapSlotID { get; set; }

        /// <summary>
        /// Returns the battle units controlled by this participant.
        /// </summary>
        /// <returns>The participant's combatants.</returns>
        public List<CombatUnit> GetCombatants()
        {
            return _combatants;
        }
    }

    /// <summary>
    /// Persistent state of the tactical battle currently in progress.
    /// </summary>
    [PersistableObject]
    public sealed class ActiveBattle
    {
        [PersistableMember(Name = "Participants")]
        private List<BattleParticipant> _participants = new List<BattleParticipant>();

        public string BattleMapInstanceID { get; set; }
        public BattleKind Kind { get; set; }
        public string PlanetInstanceID { get; set; }

        /// <summary>
        /// Returns the factions participating in the battle.
        /// </summary>
        /// <returns>The battle participants.</returns>
        public List<BattleParticipant> GetParticipants()
        {
            return _participants;
        }

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
            if (Kind != BattleKind.Space)
                throw new InvalidOperationException(
                    "Capital ships and starfighters can only enter space battles."
                );

            int combatantCount = sourceUnit is Starfighter starfighter
                ? Math.Max(0, starfighter.CurrentSquadronSize)
                : 1;
            List<CombatUnit> addedCombatants = new List<CombatUnit>(combatantCount);
            if (combatantCount == 0)
                return addedCombatants.AsReadOnly();

            for (int combatantIndex = 0; combatantIndex < combatantCount; combatantIndex++)
                addedCombatants.Add(CombatUnit.Create(sourceUnit));

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

            participant.GetCombatants().AddRange(addedCombatants);

            return addedCombatants.AsReadOnly();
        }
    }
}
