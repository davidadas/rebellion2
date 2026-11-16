using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Retains the most recently activated orbital faction without rescanning the game graph.
    /// </summary>
    internal sealed class SpaceCombatEntryTracker
    {
        private readonly Dictionary<string, int> _factions = new(StringComparer.Ordinal);
        private readonly Dictionary<BaseSceneNode, Presence> _presence = new();
        private readonly Dictionary<CapitalShip, Fleet> _capitalParents = new();
        private readonly HashSet<BaseSceneNode> _registered = new();
        private readonly HashSet<BaseSceneNode> _pendingSet = new();
        private readonly List<BaseSceneNode> _pending = new();
        private int _updateDepth;
        private int _restorationDepth;

        /// <summary>
        /// Captures one fleet or direct fighter's last committed orbital presence.
        /// </summary>
        private sealed class Presence
        {
            internal readonly HashSet<CapitalShip> Capitals = new();
            internal Planet Planet;
            internal bool Active;
        }

        /// <summary>
        /// Ends a group of scene changes when disposed.
        /// </summary>
        internal readonly struct UpdateScope : IDisposable
        {
            private readonly SpaceCombatEntryTracker _owner;
            private readonly bool _restoring;

            /// <summary>
            /// Captures the tracker and restoration policy of one update.
            /// </summary>
            /// <param name="owner">The tracker whose update is active.</param>
            /// <param name="restoring">Whether existing retained entry values must be preserved.</param>
            internal UpdateScope(SpaceCombatEntryTracker owner, bool restoring)
            {
                _owner = owner;
                _restoring = restoring;
            }

            /// <summary>
            /// Commits pending presence changes after the outermost update ends.
            /// </summary>
            public void Dispose() => _owner.EndUpdate(_restoring);
        }

        /// <summary>
        /// Defers activation until related parent, movement, and construction changes are complete.
        /// </summary>
        /// <param name="restoring">Whether to prime runtime state without replacing retained entry values.</param>
        /// <returns>The scope that ends the update.</returns>
        internal UpdateScope BeginUpdate(bool restoring = false)
        {
            _updateDepth++;
            if (restoring)
                _restorationDepth++;
            return new UpdateScope(this, restoring);
        }

        /// <summary>
        /// Replaces the authored relationship between faction IDs and stored entry selectors.
        /// </summary>
        /// <param name="definitions">The battle factions and their authored indexes.</param>
        internal void Configure(IReadOnlyList<TacticalCombatFactionDefinition> definitions)
        {
            using UpdateScope update = BeginUpdate(restoring: true);
            _factions.Clear();
            foreach (TacticalCombatFactionDefinition faction in definitions)
            {
                if (faction.FactionIndex is < 0 or > 1)
                    throw new ArgumentOutOfRangeException(nameof(definitions));
                _factions.Add(faction.FactionInstanceID, faction.FactionIndex + 1);
            }
            foreach (BaseSceneNode node in _presence.Keys)
                Schedule(node);
        }

        /// <summary>
        /// Detaches runtime observers before rebuilding the scene registry.
        /// </summary>
        internal void Clear()
        {
            foreach (BaseSceneNode node in _registered)
                node.RuntimeStateChanged -= HandleStateChanged;
            _registered.Clear();
            _presence.Clear();
            _capitalParents.Clear();
            _pending.Clear();
            _pendingSet.Clear();
        }

        /// <summary>
        /// Observes the combat-relevant state of one newly registered scene node.
        /// </summary>
        /// <param name="node">The node added to the live scene registry.</param>
        internal void Register(ISceneNode node)
        {
            if (node is not Fleet and not CapitalShip and not Starfighter)
                return;
            BaseSceneNode sceneNode = (BaseSceneNode)node;
            if (!_registered.Add(sceneNode))
                return;
            sceneNode.RuntimeStateChanged += HandleStateChanged;
            using UpdateScope update = BeginUpdate();
            if (node is Fleet fleet)
            {
                Presence presence = new();
                _presence.Add(fleet, presence);
                foreach (CapitalShip ship in fleet.GetChildren<CapitalShip>(includeDisabled: true))
                {
                    _capitalParents[ship] = fleet;
                    if (Qualifies(ship, fleet))
                        presence.Capitals.Add(ship);
                }
                Schedule(fleet);
            }
            else if (node is CapitalShip capital)
                UpdateCapital(capital, hierarchyChanged: false);
            else
            {
                _presence.Add(sceneNode, new Presence());
                Schedule(sceneNode);
            }
        }

        /// <summary>
        /// Removes one scene node's observers and cached contribution.
        /// </summary>
        /// <param name="node">The node leaving the live scene registry.</param>
        internal void Unregister(ISceneNode node)
        {
            if (node is not BaseSceneNode sceneNode || !_registered.Remove(sceneNode))
                return;
            sceneNode.RuntimeStateChanged -= HandleStateChanged;
            using UpdateScope update = BeginUpdate();
            if (node is CapitalShip capital && _capitalParents.Remove(capital, out Fleet parent))
            {
                if (_presence.TryGetValue(parent, out Presence presence))
                    presence.Capitals.Remove(capital);
                Schedule(parent);
            }
            _presence.Remove(sceneNode);
        }

        /// <summary>
        /// Updates the affected cached contribution when a registered unit changes.
        /// </summary>
        /// <param name="node">The changed unit.</param>
        /// <param name="kind">The changed runtime fact.</param>
        private void HandleStateChanged(BaseSceneNode node, SceneNodeChangeKind kind)
        {
            using UpdateScope update = BeginUpdate();
            if (node is CapitalShip capital)
                UpdateCapital(capital, kind == SceneNodeChangeKind.Hierarchy);
            else
            {
                if (node is Fleet fleet && kind == SceneNodeChangeKind.Ownership)
                {
                    Presence presence = _presence[fleet];
                    presence.Capitals.Clear();
                    foreach (
                        CapitalShip ship in fleet.GetChildren<CapitalShip>(includeDisabled: true)
                    )
                        if (Qualifies(ship, fleet))
                            presence.Capitals.Add(ship);
                }
                Schedule(node);
            }
        }

        /// <summary>
        /// Moves one capital ship's cached qualification between its direct fleet owners.
        /// </summary>
        /// <param name="ship">The changed capital ship.</param>
        /// <param name="hierarchyChanged">Whether a changed parent replaces the cached container.</param>
        private void UpdateCapital(CapitalShip ship, bool hierarchyChanged)
        {
            _capitalParents.TryGetValue(ship, out Fleet previous);
            Fleet current = ship.GetParent() as Fleet;
            if (current == null && !hierarchyChanged && ship.GetParent() == null)
                current = previous;
            if (previous != current)
            {
                if (previous != null && _presence.TryGetValue(previous, out Presence oldPresence))
                    oldPresence.Capitals.Remove(ship);
                Schedule(previous);
                if (current == null)
                    _capitalParents.Remove(ship);
                else
                    _capitalParents[ship] = current;
            }
            if (current != null && _presence.TryGetValue(current, out Presence presence))
            {
                bool changed = Qualifies(ship, current)
                    ? presence.Capitals.Add(ship)
                    : presence.Capitals.Remove(ship);
                if (changed)
                    Schedule(current);
            }
        }

        /// <summary>
        /// Tests the direct ship state required to activate its same-owner fleet.
        /// </summary>
        /// <param name="ship">The fleet's direct child.</param>
        /// <param name="fleet">The containing fleet.</param>
        /// <returns>Whether this child contributes orbital presence.</returns>
        private static bool Qualifies(CapitalShip ship, Fleet fleet) =>
            ship.IsEnabled
            && ship.ManufacturingStatus == ManufacturingStatus.Complete
            && ship.Movement == null
            && ship.OwnerInstanceID == fleet.OwnerInstanceID;

        /// <summary>
        /// Retains the first affected-unit order while coalescing repeated changes.
        /// </summary>
        /// <param name="node">The presence owner that needs reevaluation.</param>
        private void Schedule(BaseSceneNode node)
        {
            if (node != null && _presence.ContainsKey(node) && _pendingSet.Add(node))
                _pending.Add(node);
        }

        /// <summary>
        /// Evaluates committed presence and records only new activations or locations.
        /// </summary>
        private void Flush()
        {
            foreach (BaseSceneNode node in _pending)
            {
                if (!_presence.TryGetValue(node, out Presence presence))
                    continue;
                Planet planet = node.GetParent() as Planet;
                bool active = planet != null && node.IsEnabled;
                if (node is Fleet fleet)
                    active &= fleet.Movement == null && presence.Capitals.Count != 0;
                else
                {
                    Starfighter fighter = (Starfighter)node;
                    active &=
                        fighter.ManufacturingStatus == ManufacturingStatus.Complete
                        && fighter.Movement == null;
                }
                if (
                    active
                    && (!presence.Active || presence.Planet != planet)
                    && _restorationDepth == 0
                    && node.OwnerInstanceID != null
                    && _factions.TryGetValue(node.OwnerInstanceID, out int selector)
                )
                    planet.SpaceCombatEntryFaction = selector;
                presence.Active = active;
                presence.Planet = planet;
            }
            _pending.Clear();
            _pendingSet.Clear();
        }

        /// <summary>
        /// Commits the final state after the outermost related scene update ends.
        /// </summary>
        /// <param name="restoring">Whether this scope preserves retained entry values.</param>
        private void EndUpdate(bool restoring)
        {
            _updateDepth--;
            if (_updateDepth == 0 && _pending.Count != 0)
                Flush();
            if (restoring)
                _restorationDepth--;
        }
    }
}
