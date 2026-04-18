using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Manages planetary uprisings based on garrison strength vs. popular support.
    /// </summary>
    public class UprisingSystem : IGameSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly PlanetaryControlSystem _planetaryControl;

        /// <summary>
        /// Creates a new UprisingSystem.
        /// </summary>
        public UprisingSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem planetaryControl
        )
        {
            _game = game;
            _provider = provider;
            _planetaryControl = planetaryControl;
        }

        /// <summary>
        /// Checks garrison levels and resolves active uprisings for all owned planets.
        /// </summary>
        /// <returns>Game results from uprising starts and consequence resolution.</returns>
        public List<GameResult> ProcessTick()
        {
            List<GameResult> results = new List<GameResult>();

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                Faction faction = GetControllingFaction(planet);
                if (faction == null)
                    continue;

                if (planet.IsInUprising)
                    ResolveActiveUprising(planet, faction, results);
                else
                    CheckForNewUprising(planet, faction, results);
            }

            return results;
        }

        /// <summary>
        /// Returns the controlling faction for a planet, or null if the planet is
        /// unowned, unpopulated, or its faction cannot be resolved.
        /// </summary>
        private Faction GetControllingFaction(Planet planet)
        {
            if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                return null;
            if (!planet.IsPopulated())
                return null;
            return _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
        }

        /// <summary>
        /// Starts an uprising if the garrison is too weak to hold the planet.
        /// </summary>
        private void CheckForNewUprising(Planet planet, Faction faction, List<GameResult> results)
        {
            int troopCount = CountFriendlyTroops(planet, faction.InstanceID);
            int garrisonRequired = CalculateGarrisonRequirement(
                planet,
                faction,
                _game.Config.AI.Garrison
            );

            if (troopCount >= garrisonRequired)
                return;

            planet.BeginUprising();
            results.Add(
                new PlanetUprisingStartedResult
                {
                    Planet = planet,
                    InstigatorFaction = FindLeadingOpposingFaction(planet, faction.InstanceID),
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Rolls uprising dice, applies consequences, and shifts controller support.
        /// If the controller's last troops are destroyed, the planet goes neutral.
        /// </summary>
        private void ResolveActiveUprising(Planet planet, Faction faction, List<GameResult> results)
        {
            int ownerSupport = planet.GetPopularSupport(faction.InstanceID);
            int troopCount = CountFriendlyTroops(planet, faction.InstanceID);

            ResolveUprisingTableResults(
                planet,
                faction,
                ownerSupport,
                troopCount,
                out int uprisingEffect,
                out int uprisingSeverity
            );

            ApplyUprisingConsequence(planet, faction.InstanceID, uprisingEffect, results);
            ApplyUprisingConsequence(planet, faction.InstanceID, uprisingSeverity, results);

            ApplyUprisingControllerSupportShift(planet, faction);

            // If controller troops are gone while an uprising is active, the controller
            // has lost the planet: clear owner (system goes neutral) and end the uprising.
            if (CountFriendlyTroops(planet, faction.InstanceID) == 0)
            {
                _planetaryControl.ClearPlanetOwnership(planet);
                planet.EndUprising();
                results.Add(
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = faction,
                        NewOwner = null,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Resolves uprising outcome using dice rolls and consequence table lookups.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="supportForController">Popular support value for the controlling faction.</param>
        /// <param name="controllerTroopCount">Number of controller's troops on the planet.</param>
        /// <param name="uprisingEffect">Output: primary consequence table result.</param>
        /// <param name="uprisingSeverity">Output: secondary consequence table result.</param>
        private void ResolveUprisingTableResults(
            Planet planet,
            Faction faction,
            int supportForController,
            int controllerTroopCount,
            out int uprisingEffect,
            out int uprisingSeverity
        )
        {
            uprisingEffect = 0;
            uprisingSeverity = 0;

            GameConfig.UprisingConfig config = _game.Config.Uprising;

            int rollA = _provider.NextInt(0, config.DiceRange) + config.DiceAddend;
            int rollB = _provider.NextInt(0, config.DiceRange) + config.DiceAddend;

            int troopMultiplier = 1;
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.UprisingResistance > 1
            )
            {
                troopMultiplier = faction.Modifiers.UprisingResistance;
            }

            int threshold = CalculateUprisingThreshold(supportForController);

            // Hostile fleet and troop presence increases the uprising score.
            int hostileFleetCount = planet
                .GetFleets()
                .Count(f =>
                    f.GetOwnerInstanceID() != null && f.GetOwnerInstanceID() != faction.InstanceID
                );
            int hostileTroopCount = planet
                .GetAllRegiments()
                .Count(r =>
                    r.GetOwnerInstanceID() != null && r.GetOwnerInstanceID() != faction.InstanceID
                );

            // Typed garrison term — regiment type metadata is not modelled, so this is zero.
            int attachedTroopState = 0;

            int combinedScore =
                rollA
                + rollB
                + (threshold - troopMultiplier * controllerTroopCount)
                + (hostileFleetCount + hostileTroopCount - attachedTroopState);

            uprisingEffect = LookupTable(config.PrimaryConsequenceTable, combinedScore);

            if (uprisingEffect > 0)
                uprisingSeverity = LookupTable(config.SecondaryConsequenceTable, combinedScore);
        }

        /// <summary>
        /// Dispatches an uprising consequence to its handler based on the table result code.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="consequence">The consequence code from the uprising table (0–5).</param>
        /// <param name="results">Result list to append events to.</param>
        private void ApplyUprisingConsequence(
            Planet planet,
            string controllerInstanceId,
            int consequence,
            List<GameResult> results
        )
        {
            switch (consequence)
            {
                case 1:
                    DestroyRandomBuilding(planet, controllerInstanceId, results);
                    return;
                case 2:
                    DestroyRandomRegiment(planet, controllerInstanceId, results);
                    return;
                case 3:
                    CaptureRandomOfficer(planet, controllerInstanceId, results);
                    return;
                case 4:
                    FreeRandomCapturedOfficer(planet, controllerInstanceId, results);
                    return;
                case 5:
                    FreeAllCapturedOfficers(planet, controllerInstanceId, results);
                    return;
            }
        }

        /// <summary>
        /// Destroys a random controller-owned building on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomBuilding(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Building> facilities = planet
                .GetAllBuildings()
                .Where(b => b.GetOwnerInstanceID() == controllerInstanceId)
                .ToList();
            if (facilities.Count == 0)
                return;
            _game.DetachNode(facilities[_provider.NextInt(0, facilities.Count)]);
            results.Add(
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType.Uprising,
                    Severity = 1,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Destroys a random controller-owned regiment on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomRegiment(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Regiment> regiments = planet
                .GetAllRegiments()
                .Where(r => r.GetOwnerInstanceID() == controllerInstanceId)
                .ToList();
            if (regiments.Count == 0)
                return;
            _game.DetachNode(regiments[_provider.NextInt(0, regiments.Count)]);
            results.Add(
                new PlanetIncidentResult
                {
                    Planet = planet,
                    IncidentType = IncidentType.Uprising,
                    Severity = 2,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Captures a random uncaptured controller-owned officer on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void CaptureRandomOfficer(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && !o.IsCaptured)
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[_provider.NextInt(0, candidates.Count)];
            target.IsCaptured = true;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = true,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Frees one randomly selected captured controller-owned officer on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void FreeRandomCapturedOfficer(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && o.IsCaptured)
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[_provider.NextInt(0, candidates.Count)];
            target.IsCaptured = false;
            target.CaptorInstanceID = null;
            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = target,
                    IsCaptured = false,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Frees all captured controller-owned officers on the planet.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="results">Result list to append events to.</param>
        private void FreeAllCapturedOfficers(
            Planet planet,
            string controllerInstanceId,
            List<GameResult> results
        )
        {
            List<Officer> captured = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && o.IsCaptured)
                .ToList();
            foreach (Officer target in captured)
            {
                target.IsCaptured = false;
                target.CaptorInstanceID = null;
                results.Add(
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = target,
                        IsCaptured = false,
                        Context = planet,
                        Tick = _game.CurrentTick,
                    }
                );
            }
        }

        /// <summary>
        /// Applies the per-tick popular support shift to the controlling faction during uprising.
        /// On core systems the shift is halved when it moves against the faction's favor.
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction whose support is shifted.</param>
        private void ApplyUprisingControllerSupportShift(Planet planet, Faction faction)
        {
            int shift = _game.Config.Uprising.ControllerSupportShift;
            if (shift == 0)
                return;

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (parentSystem != null && parentSystem.SystemType == PlanetSystemType.CoreSystem)
            {
                bool penaltyApplies = faction.Modifiers.WeakSupportPenaltyTrigger switch
                {
                    SupportShiftCondition.Positive => shift > 0,
                    SupportShiftCondition.Negative => shift < 0,
                    _ => false,
                };
                if (penaltyApplies)
                    shift /= 2;
            }

            int currentSupport = planet.GetPopularSupport(faction.InstanceID);
            int newSupport = Math.Max(
                0,
                Math.Min(_game.Config.Planet.MaxPopularSupport, currentSupport + shift)
            );
            if (newSupport != currentSupport)
            {
                planet.SetPopularSupport(
                    faction.InstanceID,
                    newSupport,
                    _game.Config.Planet.MaxPopularSupport
                );
            }
        }

        /// <summary>
        /// Calculates how many garrison troops a planet requires for the given faction.
        /// Returns 0 when popular support is at or above the threshold.
        /// Core worlds with a faction GarrisonEfficiency modifier receive a reduced requirement.
        /// Planets in active uprisings apply the uprising multiplier.
        /// </summary>
        /// <param name="planet">The planet to calculate garrison requirements for.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="config">Garrison configuration parameters.</param>
        /// <returns>The number of garrison troops required, or 0 if support is sufficient.</returns>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config
        )
        {
            int popularSupport = planet.GetPopularSupport(faction.InstanceID);

            if (popularSupport >= config.SupportThreshold)
                return 0;

            int garrison = (int)
                Math.Ceiling(
                    (config.SupportThreshold - popularSupport) / (double)config.GarrisonDivisor
                );

            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.GarrisonEfficiency > 1
            )
            {
                garrison /= faction.Modifiers.GarrisonEfficiency;
            }

            if (planet.IsInUprising)
                garrison *= config.UprisingMultiplier;

            return garrison;
        }

        /// <summary>
        /// Calculates the uprising threshold used in the dice score formula.
        /// This is the simplified garrison requirement without efficiency or uprising multipliers.
        /// </summary>
        /// <param name="supportForController">Popular support for the controlling faction.</param>
        /// <returns>The uprising threshold value for the score formula.</returns>
        private int CalculateUprisingThreshold(int supportForController)
        {
            GameConfig.GarrisonConfig config = _game.Config.AI.Garrison;

            if (supportForController >= config.SupportThreshold)
                return 0;

            return (int)
                Math.Ceiling(
                    (config.SupportThreshold - supportForController)
                        / (double)config.GarrisonDivisor
                );
        }

        /// <summary>
        /// Looks up a value from an uprising table. Finds the highest threshold
        /// that the score meets or exceeds, and returns the associated value.
        /// </summary>
        /// <param name="table">The threshold-to-value lookup table.</param>
        /// <param name="score">The score to look up against the table thresholds.</param>
        /// <returns>The value associated with the highest matching threshold.</returns>
        private static int LookupTable(Dictionary<int, int> table, int score)
        {
            int result = 0;
            foreach (KeyValuePair<int, int> entry in table.OrderBy(e => e.Key))
            {
                if (score >= entry.Key)
                    result = entry.Value;
                else
                    break;
            }
            return result;
        }

        /// <summary>
        /// Counts friendly regiment troops at a planet.
        /// </summary>
        /// <param name="planet">The planet to count troops on.</param>
        /// <param name="factionId">The faction whose troops to count.</param>
        /// <returns>The number of friendly regiments present.</returns>
        private static int CountFriendlyTroops(Planet planet, string factionId)
        {
            return planet.GetAllRegiments().Count(r => r.GetOwnerInstanceID() == factionId);
        }

        /// <summary>
        /// Returns the opposing faction with the highest popular support on this planet,
        /// or null if no opposing faction has any support.
        /// </summary>
        /// <param name="planet">The planet to check support on.</param>
        /// <param name="ownerInstanceId">The current owner's instance ID to exclude.</param>
        /// <returns>The opposing faction with the most support, or null.</returns>
        private Faction FindLeadingOpposingFaction(Planet planet, string ownerInstanceId)
        {
            string opposingFactionId = null;
            int maxSupport = 0;
            foreach (KeyValuePair<string, int> kvp in planet.PopularSupport)
            {
                if (kvp.Key != ownerInstanceId && kvp.Value > maxSupport)
                {
                    maxSupport = kvp.Value;
                    opposingFactionId = kvp.Key;
                }
            }
            return opposingFactionId != null
                ? _game.GetFactionByOwnerInstanceID(opposingFactionId)
                : null;
        }
    }
}
