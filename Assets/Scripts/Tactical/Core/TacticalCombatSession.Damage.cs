using System.Collections.Generic;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Queues the destruction report for the credited actor's applied group and current category.
        /// </summary>
        /// <param name="victim">The unit whose hull was lost.</param>
        /// <param name="attackerInstanceID">The firing unit's captured identity.</param>
        private void ReportDestruction(CombatUnit victim, string attackerInstanceID)
        {
            if (
                attackerInstanceID == null
                || !_unitsByID.TryGetValue(attackerInstanceID, out CombatUnit attacker)
            )
                return;
            if (_synchronizationAuthority)
                foreach (TacticalCombatSide side in _sides)
                    ReportFactionDestruction(victim, attacker, side.Side);
            else
                ReportFactionDestruction(victim, attacker, PlayerSide);
        }

        /// <summary>
        /// Selects one faction's existing kill, loss, category, and personnel cues from the same destruction.
        /// </summary>
        /// <param name="victim">The destroyed participant.</param>
        /// <param name="attacker">The credited firing participant.</param>
        /// <param name="side">The faction receiving its command report.</param>
        private void ReportFactionDestruction(
            CombatUnit victim,
            CombatUnit attacker,
            CombatSide side
        )
        {
            bool credited = attacker.Side == side;
            CombatUnit actor = credited ? attacker : victim;
            int bank = GetSide(side).FactionIndex == 0 ? 0 : 122;
            int message = credited ? 85 : 98;
            if (GetGroup(actor) is TacticalCombatGroup group)
            {
                if (actor.IsFighter)
                {
                    int category = GetReportFighterCategory(group, side);
                    if (category < 0)
                        return;
                    message = (credited ? 94 : 107) + category;
                }
                else
                {
                    if (
                        !credited
                        && _personnelHostCounts.TryGetValue(victim.InstanceID, out int count)
                    )
                        for (int index = 0; index < count; index++)
                            EnqueueFactionNotification(side, bank == 0 ? 134 : 260);
                    int category = GetReportObjectCategory(group, side);
                    if (category >= 0)
                        message += 1 + category;
                }
            }
            EnqueueFactionNotification(side, message + bank);
        }

        /// <summary>
        /// Finds an applied capital group in the player's current command slots.
        /// </summary>
        /// <param name="group">The applied capital group.</param>
        /// <param name="side">The receiving faction's retained category owner.</param>
        /// <returns>The matching command slot, or minus one when it has no slot.</returns>
        private int GetReportObjectCategory(TacticalCombatGroup group, CombatSide side)
        {
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_objectGroups[(int)side, category], group))
                    return category;
            return -1;
        }

        /// <summary>
        /// Resolves a fighter group's current slot or retained deployment category.
        /// </summary>
        /// <param name="group">The applied fighter group.</param>
        /// <param name="side">The receiving faction's retained category owner.</param>
        /// <returns>The resolved category, or minus one for an unavailable fighter group.</returns>
        private int GetReportFighterCategory(TacticalCombatGroup group, CombatSide side)
        {
            for (int category = 0; category < 4; category++)
                if (ReferenceEquals(_fighterGroups[(int)side, category], group))
                    return category;
            return group.IsFighterGroup ? group.FighterCategory : -1;
        }

        /// <summary>
        /// Checks the ordinary display states admitted by destruction and scuttling commands.
        /// Hull strength is evaluated separately by each command.
        /// </summary>
        /// <param name="unit">The registered encounter participant.</param>
        /// <returns>Whether the unit remains in an admitted display state.</returns>
        private static bool IsActiveDisplayObject(CombatUnit unit) =>
            unit?.IsActiveDisplayObject == true;

        /// <summary>
        /// Applies scuttling only to an active display object with ordered positive hull strength.
        /// </summary>
        /// <param name="unit">The unit addressed by the scuttling request.</param>
        private void Scuttle(CombatUnit unit)
        {
            if (!IsActiveDisplayObject(unit) || !(unit.Hull > 0))
                return;
            Audio.Enqueue(TacticalCombatAudio.GetExplosionSound(unit));
            DestructionEffectRequested?.Invoke(unit);
            Destroy(unit);
        }

        /// <summary>
        /// Applies destruction, carrier payload losses, reservation cleanup, and spatial removal.
        /// </summary>
        /// <param name="unit">The destroyed tactical unit.</param>
        private void Destroy(CombatUnit unit)
        {
            if (unit.Status == CombatUnitStatus.Destroyed)
                return;
            if (unit.IsBattleStation)
                BattleStation.Destroy();
            else
                unit.Destroy();
            unit.Status = CombatUnitStatus.Destroyed;
            _initiallyDockedFighters.Remove(unit);
            if (unit.TractorTargetID != null)
            {
                _tractorCommandsPending.Add(unit);
                QueueCommand(
                    new Command(CommandKind.TractorUnlock, unit, GetUnit(unit.TractorTargetID))
                );
            }
            RemoveFromGroup(unit);
            _spatial.Update(unit);
            if (_carriers.TryGetValue(unit.InstanceID, out TacticalCombatCarrier carrier))
            {
                foreach (CombatUnit fighter in _units)
                    if (fighter.IsFighter && fighter.CarrierID == unit.InstanceID)
                    {
                        if (fighter.Status is CombatUnitStatus.Docked or CombatUnitStatus.Recovered)
                            Destroy(fighter);
                        else if (_recoveries.ContainsKey(fighter))
                            ReleaseRecovery(fighter);
                    }
                carrier.Clear();
            }
            else
            {
                ReleaseRecovery(unit);
                if (
                    unit.CarrierID != null
                    && _carriers.TryGetValue(unit.CarrierID, out TacticalCombatCarrier host)
                )
                    host.Detach(unit);
                unit.CarrierID = null;
            }
            RefreshTractorContribution(unit);
            UnitDestroyed?.Invoke(unit);
        }

        /// <summary>
        /// Adds a retained tractor contributor after active-object command admission.
        /// </summary>
        /// <param name="source">The unit applying the tractor beam.</param>
        /// <param name="target">The unit whose movement chain retains the source.</param>
        private void ApplyTractorLock(CombatUnit source, CombatUnit target)
        {
            if (
                !_tractorSources.TryGetValue(target, out SortedDictionary<uint, CombatUnit> sources)
            )
            {
                sources = new();
                _tractorSources.Add(target, sources);
            }
            if (sources.Count == 0)
            {
                Audio.Enqueue(TacticalCombatSound.TractorEngaged);
                SetTractorSources(target, true);
            }
            source.TractorTargetID = target.InstanceID;
            sources.TryAdd(unchecked((uint)source.Index), source);
            target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Removes an addressed contributor and announces every resulting empty tractor chain.
        /// </summary>
        /// <param name="source">The contributor addressed by the command.</param>
        /// <param name="target">The resolved target whose chain is updated.</param>
        private void ApplyTractorUnlock(CombatUnit source, CombatUnit target)
        {
            bool populated = _tractorSources.TryGetValue(
                target,
                out SortedDictionary<uint, CombatUnit> sources
            );
            if (populated)
                sources.Remove(unchecked((uint)source.Index));
            if (!populated || sources.Count == 0)
            {
                Audio.Enqueue(TacticalCombatSound.TractorReleased);
                SetTractorSources(target, false);
            }
            target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Stores and publishes the change to a unit's tractor restraint source chain.
        /// </summary>
        /// <param name="target">The unit whose restraint sources changed.</param>
        /// <param name="populated">Whether at least one tractor source remains.</param>
        internal void SetTractorSources(CombatUnit target, bool populated)
        {
            target.HasTractorSources = populated;
            TractorSourcesChanged?.Invoke(target, populated);
        }

        /// <summary>
        /// Adds retained tractor contributions in unsigned source-key order with a store after every addition.
        /// </summary>
        /// <param name="target">The unit whose retained tractor chain is read.</param>
        /// <returns>The rounded sum of its current contributors, without a health or participation filter.</returns>
        private float GetTractorRestraint(CombatUnit target)
        {
            if (_synchronizationReplica)
                return target.TractorRestraint;
            float restraint = 0;
            if (_tractorSources.TryGetValue(target, out SortedDictionary<uint, CombatUnit> sources))
                foreach (CombatUnit source in sources.Values)
                    restraint = (float)((double)restraint + source.GetTractorContribution());
            return restraint;
        }

        /// <summary>
        /// Refreshes the one retained target affected by a contributor's hull or tractor-system mutation.
        /// </summary>
        /// <param name="source">The contributor whose current budget changed.</param>
        private void RefreshTractorContribution(CombatUnit source)
        {
            CombatUnit target = GetUnit(source.TractorTargetID);
            if (target != null)
                target.TractorRestraint = GetTractorRestraint(target);
        }

        /// <summary>
        /// Refreshes stored movement inputs after a retained lock or system mutation without changing link ownership.
        /// </summary>
        private void UpdateTractorRestraint()
        {
            foreach (CombatUnit unit in _units)
                unit.TractorRestraint = GetTractorRestraint(unit);
        }
    }
}
