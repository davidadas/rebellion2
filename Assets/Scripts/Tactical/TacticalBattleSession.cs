using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Owns the execution services and fixed-step movement of one loaded tactical battle.
    /// </summary>
    public sealed class TacticalBattleSession
    {
        private SpaceCombatUnit[] _units = Array.Empty<SpaceCombatUnit>();

        public TacticalBattleClock Clock { get; } = new TacticalBattleClock();
        public ActiveBattle Battle { get; private set; }
        public TacticalCombat Combat { get; private set; }
        public TacticalCarrierOperations Carriers { get; private set; }

        /// <summary>
        /// Rebuilds runtime indexes from saved battle state and prepared model-local geometry.
        /// </summary>
        /// <param name="battle">The authoritative battle state.</param>
        /// <param name="mounts">The resolved local weapon mounts.</param>
        /// <param name="carrierPaths">The resolved local carrier flight paths.</param>
        public void Initialize(
            ActiveBattle battle,
            IReadOnlyDictionary<Hardpoint, TacticalCombat.Mount> mounts,
            IReadOnlyDictionary<SpaceCombatUnit, Vector3[][]> carrierPaths
        )
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            TacticalCarrierOperations carriers = new TacticalCarrierOperations(
                battle,
                carrierPaths
            );
            TacticalCombat combat = new TacticalCombat(battle, mounts);
            SpaceCombatUnit[] units = battle
                .GetParticipants()
                .SelectMany(participant => participant.GetCombatants())
                .ToArray();
            Battle = battle;
            Carriers = carriers;
            Combat = combat;
            _units = units;
        }

        /// <summary>
        /// Advances movement, carrier traffic, projectile contacts, and weapons on the same fixed step.
        /// </summary>
        /// <param name="deltaTime">The elapsed unpaused battle time.</param>
        public void Advance(float deltaTime)
        {
            Combat?.Advance(deltaTime, AdvanceMovement);
        }

        /// <summary>
        /// Releases the current battle services without changing the selected tactical speed.
        /// </summary>
        public void Clear()
        {
            Battle = null;
            Combat = null;
            Carriers = null;
            _units = Array.Empty<SpaceCombatUnit>();
        }

        /// <summary>
        /// Advances the battle roster independently of the existence or visibility of rendered models.
        /// </summary>
        /// <param name="deltaTime">One fixed battle step.</param>
        private void AdvanceMovement(float deltaTime)
        {
            foreach (SpaceCombatUnit unit in _units)
                if (
                    unit is { Kind: SpaceCombatUnitKind.CapitalShip }
                    || unit.Flight.Phase == FighterFlightState.Deployed
                )
                    TacticalMovement.Advance(unit, deltaTime);
            Carriers.Advance(deltaTime);
        }
    }
}
