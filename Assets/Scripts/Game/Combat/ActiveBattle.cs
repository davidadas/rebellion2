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

        public List<CombatUnit> GetCombatants() => _combatants;
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
        public int FlightRandomSeed { get; set; }
        public long FlightRandomIndex { get; set; }

        public List<BattleParticipant> GetParticipants() => _participants;

        /// <summary>
        /// Adds an independently mutable copy of a strategic unit under its current owner.
        /// </summary>
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
            participant.GetCombatants().Add(combatant);
            return combatant;
        }

        public CombatUnit GetCombatant(string sourceUnitInstanceID)
        {
            if (string.IsNullOrEmpty(sourceUnitInstanceID))
                return null;
            foreach (BattleParticipant participant in _participants)
            foreach (CombatUnit combatant in participant.GetCombatants())
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
