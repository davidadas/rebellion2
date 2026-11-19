using UnityEngine;
using TacticalCombatEffectKind = global::Rebellion.Game.SpaceCombat.TacticalCombatEffectKind;

namespace Rebellion.Tactical.Rendering
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Connects admitted tractor-source and gravity-generator transitions to their looping visuals.
        /// </summary>
        /// <param name="visual">The capital ship's owned presentation.</param>
        private void BindLockEffects(Visual visual)
        {
            if (!_showPyro)
                return;
            TacticalCombatUnit unit = visual.Unit;
            if (unit.HasGravityWell)
            {
                visual.GravityWellPresentationRequested = () =>
                    _impacts.SetLock(unit, TacticalCombatEffectKind.GravityLock, true);
                unit.GravityWellPresentationRequested += visual.GravityWellPresentationRequested;
            }
            else
            {
                visual.TractorSourcesChanged = enabled =>
                    _impacts.SetLock(unit, TacticalCombatEffectKind.TractorRestraint, enabled);
                unit.TractorSourcesChanged += visual.TractorSourcesChanged;
                if (unit.HasTractorSources)
                    visual.TractorSourcesChanged(true);
            }
        }

        /// <summary>
        /// Displays a discharge using the firing faction's authored beam color.
        /// </summary>
        /// <param name="shot">The applied discharge.</param>
        /// <param name="factionIndex">The firing faction's color index.</param>
        public void ShowShot(TacticalCombatShot shot, int factionIndex) =>
            _effects.ShowShot(shot, factionIndex);

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
            TacticalCombatUnit unit,
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
        public void ShowDestruction(TacticalCombatUnit unit)
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
        public void ShowSuperlaser(
            TacticalCombatUnit station,
            TacticalCombatUnit target,
            float battleDepth
        )
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
