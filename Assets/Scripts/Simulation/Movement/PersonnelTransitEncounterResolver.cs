using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Resolves detection, evasion, injury, capture, and destruction for personnel crossing hostile orbit.
    /// </summary>
    internal sealed class PersonnelTransitEncounterResolver
    {
        private readonly GameRoot _game;
        private readonly MissionQueries _queries;
        private readonly PersonnelCommands _personnelCommands;
        private readonly IRandomNumberProvider _random;

        /// <summary>
        /// Creates a personnel transit encounter resolver for the active game.
        /// </summary>
        /// <param name="game">The active game state.</param>
        /// <param name="random">The deterministic simulation random source.</param>
        internal PersonnelTransitEncounterResolver(GameRoot game, IRandomNumberProvider random)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _queries = new MissionQueries(game);
            _personnelCommands = new PersonnelCommands(new PersonnelQueries(game));
        }

        /// <summary>
        /// Resolves one personnel group's attempt to cross hostile orbit at a planet.
        /// </summary>
        /// <param name="movables">The personnel crossing together.</param>
        /// <param name="planet">The planet where the crossing occurs.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        internal void Resolve(
            IReadOnlyList<IMovable> movables,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            if (movables == null || planet == null || results == null)
                return;

            foreach (
                IGrouping<string, IMissionParticipant> group in movables
                    .OfType<IMissionParticipant>()
                    .Where(IsEligibleParticipant)
                    .GroupBy(participant => participant.GetOwnerInstanceID())
            )
            {
                ResolveFactionGroup(group.Key, group.ToList(), planet, results);
            }
        }

        /// <summary>
        /// Returns whether personnel may still resolve a transit encounter.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True when the participant remains active and free.</returns>
        private static bool IsEligibleParticipant(IMissionParticipant participant)
        {
            return participant switch
            {
                Officer officer => !officer.IsKilled && !officer.IsCaptured,
                SpecialForces specialForces => specialForces.IsActive(),
                _ => false,
            };
        }

        /// <summary>
        /// Resolves detection and consequences for one faction's personnel group.
        /// </summary>
        /// <param name="ownerInstanceId">The group owner's faction identifier.</param>
        /// <param name="participants">The personnel crossing together.</param>
        /// <param name="planet">The planet where the transit encounter occurs.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolveFactionGroup(
            string ownerInstanceId,
            IReadOnlyList<IMissionParticipant> participants,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            List<ISceneNode> detectors = MissionQueries.GetPersonnelTransitDetectors(
                ownerInstanceId,
                planet
            );
            if (detectors.Count == 0)
                return;

            ISceneNode detectingUnit = detectors.FirstOrDefault(detector =>
                RollPercent(
                    _queries.GetPersonnelTransitFoilProbability(participants, detector, planet)
                )
            );
            if (detectingUnit == null)
                return;

            foreach (IMissionParticipant participant in participants.ToList())
            {
                ISceneNode confrontingUnit = detectors[_random.NextInt(0, detectors.Count)];
                ResolveParticipant(participant, confrontingUnit, planet, results);
            }
        }

        /// <summary>
        /// Resolves one detected participant's evasion and resulting consequences.
        /// </summary>
        /// <param name="participant">The detected participant.</param>
        /// <param name="detector">The detector confronting the participant.</param>
        /// <param name="planet">The planet where the transit encounter occurs.</param>
        /// <param name="results">The collection receiving encounter results.</param>
        private void ResolveParticipant(
            IMissionParticipant participant,
            ISceneNode detector,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            bool evaded = RollPercent(
                _queries.GetPersonnelTransitEvasionProbability(participant, detector, planet)
            );
            if (participant is SpecialForces specialForces)
            {
                if (!evaded)
                    DestroySpecialForces(specialForces, planet, results);
                return;
            }

            if (participant is not Officer officer)
                return;

            Officer commander = Mission.FindDetectorCommanderAtPlanet(detector, planet);
            IGameEntity opponent = commander ?? detector as IGameEntity;
            List<GameResult> injuryResults = new List<GameResult>();
            bool killed = Mission.ApplyEvasionInjury(
                officer,
                opponent,
                planet,
                _game,
                _random,
                injuryResults
            );
            foreach (GameResult injuryResult in injuryResults)
                results.Add(injuryResult);

            if (killed)
            {
                _personnelCommands.KillOfficer(officer);
                return;
            }

            if (evaded)
                return;

            MovementState interruptedMovement = officer.Movement;
            officer.Movement = null;
            if (!officer.TryCapture(detector.GetOwnerInstanceID()))
            {
                officer.Movement = interruptedMovement;
                return;
            }

            results.Add(
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    IsCaptured = true,
                    CaptorInstanceID = detector.GetOwnerInstanceID(),
                    ParentAtCapture = officer.GetParent(),
                    CapturingUnit = detector,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Removes detected special forces that fail to evade hostile orbital forces.
        /// </summary>
        /// <param name="specialForces">The unit to remove.</param>
        /// <param name="planet">The planet where the transit encounter occurs.</param>
        /// <param name="results">The collection receiving the destruction result.</param>
        private void DestroySpecialForces(
            SpecialForces specialForces,
            Planet planet,
            ICollection<GameResult> results
        )
        {
            _game.DeleteNode(specialForces);
            results.Add(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = specialForces,
                    Context = planet,
                    Tick = _game.CurrentTick,
                }
            );
        }

        /// <summary>
        /// Rolls against a percentage probability.
        /// </summary>
        /// <param name="probability">The percentage chance of success.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollPercent(double probability)
        {
            return probability > 0 && _random.NextDouble() * 100 < probability;
        }
    }
}
