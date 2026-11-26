using Rebellion.Game.Combat;
using Rebellion.UI;
using UnityEngine;
using TacticalCombatEffectKind = global::Rebellion.Content.TacticalCombatEffectKind;

namespace Rebellion.Tactical.Rendering
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Updates the tractor lock effect for an ordinary capital ship.
        /// </summary>
        /// <param name="unit">The ship whose tractor sources changed.</param>
        /// <param name="enabled">Whether the ship has an active tractor source.</param>
        private void HandleTractorSourcesChanged(CombatUnit unit, bool enabled)
        {
            if (_showPyro && !unit.IsBattleStation && !unit.IsFighter && !unit.HasGravityWell)
                _impacts.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, enabled);
        }

        /// <summary>
        /// Starts the gravity well effect on the activating capital ship.
        /// </summary>
        /// <param name="unit">The capital ship supplying the gravity well.</param>
        private void HandleGravityWell(CombatUnit unit)
        {
            if (_showPyro && !unit.IsBattleStation && !unit.IsFighter && unit.HasGravityWell)
                _impacts.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);
        }

        /// <summary>
        /// Displays a discharge using the firing faction's authored beam color.
        /// </summary>
        /// <param name="shot">The applied discharge.</param>
        /// <param name="factionIndex">The firing faction's color index.</param>
        public void ShowShot(TacticalCombatShot shot, int factionIndex) =>
            _effects.ShowShot(
                shot,
                factionIndex,
                GetUnitDefinition(shot.Target)?.SplitTurbolaserBeams == true
            );

        /// <summary>
        /// Advances visual effect timers independently of campaign and tactical simulation ticks.
        /// </summary>
        /// <param name="elapsedSeconds">The unscaled frame interval.</param>
        public void AdvanceEffects(float elapsedSeconds)
        {
            _effects.Advance(elapsedSeconds);
            _impacts.Advance(elapsedSeconds);
        }

        /// <summary>
        /// Displays the animation associated with one applied weapon impact.
        /// </summary>
        /// <param name="unit">The impacted encounter participant.</param>
        /// <param name="weapon">The impacting weapon.</param>
        /// <param name="impact">The affected surface or destruction transition.</param>
        public void ShowImpact(
            CombatUnit unit,
            TacticalWeaponType weapon,
            TacticalCombatImpactKind impact
        )
        {
            if (_showPyro)
                _impacts.ShowImpact(unit, weapon, impact);
        }

        /// <summary>
        /// Begins the capital model's authored destruction sequence.
        /// </summary>
        /// <param name="unit">The destroyed encounter participant.</param>
        public void ShowDestruction(CombatUnit unit)
        {
            if (_showPyro)
                _impacts.ShowDestruction(unit);
        }

        /// <summary>
        /// Fires the station's superlaser from its rendered emitter position.
        /// </summary>
        /// <param name="station">The firing station.</param>
        /// <param name="target">The selected capital ship.</param>
        /// <param name="battleDepth">The battle's deployment span.</param>
        public void ShowSuperlaser(CombatUnit station, CombatUnit target, float battleDepth)
        {
            foreach (Visual visual in _visuals)
                if (ReferenceEquals(visual.Unit, station))
                {
                    _effects.ShowSuperlaser(
                        (visual.StationPosition ?? ToUnity(station.Position))
                            + new Vector3(0, battleDepth * 0.11f, battleDepth * -0.09f),
                        target
                    );
                    return;
                }
        }
    }
}
