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
    /// Uses dual dice rolls and table lookups matching the original uprising resolution.
    /// </summary>
    public class UprisingSystem
    {
        private readonly GameRoot _game;

        public UprisingSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Processes uprising checks for all owned, populated planets.
        /// Starts new uprisings when garrison is insufficient.
        /// Applies consequence resolution each tick for planets already in uprising.
        /// Uprisings are cleared externally by SubdueUprisingMission or OwnershipSystem.
        /// </summary>
        /// <param name="provider">Random number provider for dice rolls.</param>
        /// <returns>Game results from uprising starts and consequence resolution.</returns>
        public List<GameResult> ProcessTick(IRandomNumberProvider provider)
        {
            List<GameResult> results = new List<GameResult>();
            List<Planet> planets = _game.GetSceneNodesByType<Planet>();

            foreach (Planet planet in planets)
            {
                if (string.IsNullOrEmpty(planet.OwnerInstanceID))
                    continue;

                if (!planet.IsPopulated())
                    continue;

                Faction faction = _game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID);
                if (faction == null)
                    continue;

                if (!planet.IsInUprising)
                {
                    int troopCount = CountFriendlyTroops(planet, faction.InstanceID);
                    int garrisonRequired = CalculateGarrisonRequirement(
                        planet,
                        faction,
                        _game.Config.AI.Garrison
                    );
                    int garrisonSurplus = troopCount - garrisonRequired;

                    if (garrisonSurplus < 0)
                    {
                        planet.BeginUprising();
                        results.Add(
                            new PlanetUprisingStartedResult
                            {
                                Planet = planet,
                                InstigatorFaction = FindLeadingOpposingFaction(
                                    planet,
                                    faction.InstanceID
                                ),
                                Tick = _game.CurrentTick,
                            }
                        );
                    }
                }
                else
                {
                    int ownerSupport = planet.GetPopularSupport(faction.InstanceID);
                    int troopCount = CountFriendlyTroops(planet, faction.InstanceID);

                    ResolveUprisingTableResults(
                        planet,
                        faction,
                        ownerSupport,
                        troopCount,
                        provider,
                        out int uprisingEffect,
                        out int uprisingSeverity
                    );

                    ApplyUprisingConsequence(
                        planet,
                        faction.InstanceID,
                        uprisingEffect,
                        provider,
                        results
                    );
                    ApplyUprisingConsequence(
                        planet,
                        faction.InstanceID,
                        uprisingSeverity,
                        provider,
                        results
                    );

                    ApplyUprisingControllerSupportShift(planet, faction);
                }
            }

            return results;
        }

        /// <summary>
        /// Resolves uprising using dual dice rolls and UPRIS1/UPRIS2 table lookups.
        /// Combined score = dice + (garrison_threshold - troop_multiplier * troops)
        ///                      + (hostile_fleets + hostile_troops - typed_garrison).
        /// </summary>
        /// <param name="planet">The planet in uprising.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="supportForController">Popular support value for the controlling faction.</param>
        /// <param name="controllerTroopCount">Number of controller's troops on the planet.</param>
        /// <param name="provider">Random number provider for dice rolls.</param>
        /// <param name="uprisingEffect">Output: UPRIS1 table result (effect code).</param>
        /// <param name="uprisingSeverity">Output: UPRIS2 table result (severity code).</param>
        private void ResolveUprisingTableResults(
            Planet planet,
            Faction faction,
            int supportForController,
            int controllerTroopCount,
            IRandomNumberProvider provider,
            out int uprisingEffect,
            out int uprisingSeverity
        )
        {
            uprisingEffect = 0;
            uprisingSeverity = 0;

            GameConfig.UprisingConfig config = _game.Config.Uprising;

            int rollA = provider.NextInt(0, config.DiceRange) + config.DiceAddend;
            int rollB = provider.NextInt(0, config.DiceRange) + config.DiceAddend;

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

            uprisingEffect = LookupTable(config.Upris1Table, combinedScore);

            if (uprisingEffect > 0)
                uprisingSeverity = LookupTable(config.Upris2Table, combinedScore);
        }

        /// <summary>
        /// Dispatches an uprising consequence to its handler based on the table result code.
        /// </summary>
        /// <param name="planet">The planet experiencing the uprising.</param>
        /// <param name="controllerInstanceId">The controlling faction's instance ID.</param>
        /// <param name="consequence">The consequence code from the uprising table (0–5).</param>
        /// <param name="provider">RNG provider for selecting random targets.</param>
        /// <param name="results">Result list to append events to.</param>
        private void ApplyUprisingConsequence(
            Planet planet,
            string controllerInstanceId,
            int consequence,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            switch (consequence)
            {
                case 1:
                    DestroyRandomBuilding(planet, controllerInstanceId, provider, results);
                    return;
                case 2:
                    DestroyRandomRegiment(planet, controllerInstanceId, provider, results);
                    return;
                case 3:
                    CaptureRandomOfficer(planet, controllerInstanceId, provider, results);
                    return;
                case 4:
                    FreeRandomCapturedOfficer(planet, controllerInstanceId, provider, results);
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
        /// <param name="provider">RNG provider for selecting a random building.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomBuilding(
            Planet planet,
            string controllerInstanceId,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            List<Building> facilities = planet
                .GetAllBuildings()
                .Where(b => b.GetOwnerInstanceID() == controllerInstanceId)
                .ToList();
            if (facilities.Count == 0)
                return;
            _game.DetachNode(facilities[provider.NextInt(0, facilities.Count)]);
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
        /// <param name="provider">RNG provider for selecting a random regiment.</param>
        /// <param name="results">Result list to append events to.</param>
        private void DestroyRandomRegiment(
            Planet planet,
            string controllerInstanceId,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            List<Regiment> regiments = planet
                .GetAllRegiments()
                .Where(r => r.GetOwnerInstanceID() == controllerInstanceId)
                .ToList();
            if (regiments.Count == 0)
                return;
            _game.DetachNode(regiments[provider.NextInt(0, regiments.Count)]);
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
        /// <param name="provider">RNG provider for selecting a random officer.</param>
        /// <param name="results">Result list to append events to.</param>
        private void CaptureRandomOfficer(
            Planet planet,
            string controllerInstanceId,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && !o.IsCaptured)
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[provider.NextInt(0, candidates.Count)];
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
        /// <param name="provider">RNG provider for selecting a random captured officer.</param>
        /// <param name="results">Result list to append events to.</param>
        private void FreeRandomCapturedOfficer(
            Planet planet,
            string controllerInstanceId,
            IRandomNumberProvider provider,
            List<GameResult> results
        )
        {
            List<Officer> candidates = planet
                .GetAllOfficers()
                .Where(o => o.GetOwnerInstanceID() == controllerInstanceId && o.IsCaptured)
                .ToList();
            if (candidates.Count == 0)
                return;
            Officer target = candidates[provider.NextInt(0, candidates.Count)];
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
        private static int CountFriendlyTroops(Planet planet, string factionId)
        {
            return planet.GetAllRegiments().Count(r => r.GetOwnerInstanceID() == factionId);
        }

        /// <summary>
        /// Returns the opposing faction with the highest popular support on this planet,
        /// or null if no opposing faction has any support.
        /// </summary>
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
