using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Persistent state for a battle that can continue independently of the campaign scene graph.
    /// </summary>
    [PersistableObject]
    public sealed class ActiveBattle
    {
        [PersistableMember(Name = nameof(Combatants))]
        private List<CombatUnit> _combatants = new List<CombatUnit>();

        public BattleKind Kind { get; set; }
        public string PlanetInstanceID { get; set; }

        [PersistableCollectionItem(Name = "FactionID")]
        public List<string> AttackingFactionIDs { get; set; } = new();

        [PersistableCollectionItem(Name = "FactionID")]
        public List<string> DefendingFactionIDs { get; set; } = new();

        [PersistableIgnore]
        public IReadOnlyList<CombatUnit> Combatants => _combatants;

        // Older XML stored only one faction ID per side.
        [PersistableMember(Name = "AttackerOwnerInstanceID")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private string LegacyAttackerOwnerInstanceID
        {
            get => null;
            set
            {
                if (AttackingFactionIDs.Count == 0 && !string.IsNullOrEmpty(value))
                    AttackingFactionIDs.Add(value);
            }
        }

        [PersistableMember(Name = "DefenderOwnerInstanceID")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private string LegacyDefenderOwnerInstanceID
        {
            get => null;
            set
            {
                if (DefendingFactionIDs.Count == 0 && !string.IsNullOrEmpty(value))
                    DefendingFactionIDs.Add(value);
            }
        }

        // Older XML grouped combatants by owner.
        [PersistableMember(Name = "Participants")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private List<LegacyBattleParticipant> LegacyParticipants
        {
            get => null;
            set
            {
                foreach (LegacyBattleParticipant participant in value)
                    _combatants.AddRange(participant.Combatants);
            }
        }

        [PersistableObject(Name = "BattleParticipant")]
        private sealed class LegacyBattleParticipant
        {
            public List<CombatUnit> Combatants { get; set; } = new List<CombatUnit>();
            public string FactionInstanceID { get; set; }
        }

        /// <summary>
        /// Adds an independently mutable copy of a strategic unit.
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

            CombatUnit combatant = CombatUnit.Create(sourceUnit);
            _combatants.Add(combatant);
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
            foreach (CombatUnit combatant in _combatants)
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
