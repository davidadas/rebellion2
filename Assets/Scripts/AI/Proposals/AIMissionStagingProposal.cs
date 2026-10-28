using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Prepares sabotage personnel by relocating or waiting for a team already arriving at a safe planet.
    /// </summary>
    public sealed class AIMissionStagingProposal : AIProposal
    {
        public AIMissionProposal Mission { get; }

        public Planet Destination { get; }

        public IReadOnlyList<IMissionParticipant> Participants { get; }

        public bool IsWaiting { get; }

        public int WaitingTicks =>
            Mission.Participants.Max(participant =>
                participant.GetTransitMovement()?.TicksRemaining() ?? 0
            );

        /// <summary>
        /// Creates a relocation alternative for a fully assessed sabotage team.
        /// </summary>
        /// <param name="mission">The mission opportunity motivating the relocation.</param>
        /// <param name="destination">The proposed friendly departure planet.</param>
        public AIMissionStagingProposal(AIMissionProposal mission, Planet destination)
            : this(mission, destination, false) { }

        /// <summary>
        /// Creates one turn of waiting for personnel already traveling to this departure planet.
        /// </summary>
        /// <param name="mission">The complete future mission team.</param>
        /// <param name="destination">The common assembly planet.</param>
        /// <returns>A proposal claiming only the personnel already present.</returns>
        internal static AIMissionStagingProposal WaitForArrivals(
            AIMissionProposal mission,
            Planet destination
        )
        {
            return new AIMissionStagingProposal(mission, destination, true);
        }

        /// <summary>
        /// Creates a move or wait alternative without retaining an assignment beyond this turn.
        /// </summary>
        /// <param name="mission">The assessed mission opportunity.</param>
        /// <param name="destination">The assembly planet.</param>
        /// <param name="isWaiting">Whether present personnel should await incoming teammates.</param>
        private AIMissionStagingProposal(
            AIMissionProposal mission,
            Planet destination,
            bool isWaiting
        )
        {
            Mission = mission;
            Destination = destination;
            IsWaiting = isWaiting;
            Participants =
                mission
                    ?.Participants.Where(participant =>
                        isWaiting
                            ? participant.GetParent()?.InstanceID == destination?.InstanceID
                                && participant.GetTransitMovement() == null
                            : participant.GetParent()?.InstanceID != destination?.InstanceID
                    )
                    .ToArray()
                ?? Array.Empty<IMissionParticipant>();
        }

        /// <summary>
        /// Identifies this team, opportunity, and departure planet deterministically.
        /// </summary>
        /// <returns>The proposal's stable selection key.</returns>
        public override string GetSortKey()
        {
            return $"{(IsWaiting ? "mission-waiting" : "mission-staging")}:{Mission?.GetSortKey()}:{Destination?.InstanceID}";
        }

        /// <summary>
        /// Checks that relocation saves travel and the whole team can reach a safe owned planet.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>True when relocation is valid and improves the mission round trip.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return TryGetTravelTicks(context, out _, out _);
        }

        /// <summary>
        /// Revalidates the move against the live state before execution.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>True when the normal movement command remains available and valid.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return context?.Movement != null && CanSelect(context);
        }

        /// <summary>
        /// Moves the team normally without creating a mission or retaining a future assignment.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context) || IsWaiting)
                return;

            foreach (
                IGrouping<ISceneNode, IMissionParticipant> group in Participants.GroupBy(
                    participant => participant.GetParent()
                )
            )
            {
                if (group.Key?.InstanceID == Destination.InstanceID)
                    continue;
                context.Movement.TryRequestMove(
                    group.Cast<ISceneNode>().ToArray(),
                    Destination,
                    context.Faction.InstanceID
                );
            }
        }

        /// <summary>
        /// Compares direct mission travel with relocation followed by a local mission and return.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="directTicks">Receives the direct outbound and return travel duration.</param>
        /// <param name="stagedTicks">Receives relocation plus the local outbound and return duration.</param>
        /// <returns>True when staging is safe, valid, and saves travel for the complete team.</returns>
        internal bool TryGetTravelTicks(
            AITurnContext context,
            out long directTicks,
            out long stagedTicks
        )
        {
            directTicks = 0;
            stagedTicks = 0;
            if (
                context?.MovementQueries == null
                || Mission?.MissionTypeID != MissionTypeIDs.Sabotage
                || !Mission.IsTeamFinal
                || Participants.Count == 0
                || Participants.Any(participant =>
                    participant.IsOnMission() || participant.GetTransitMovement() != null
                )
                || !context.Assessment.IsEnemyPlanet(Mission.TargetPlanet)
                || !IsOwnedBy(context, Destination)
                || context.Assessment.HasKnownHostileFleet(Destination)
            )
                return false;

            foreach (IMissionParticipant participant in Participants)
                if (
                    !context.MovementQueries.CanUseSafeRelocationDestination(
                        participant,
                        Destination,
                        participant.GetParentOfType<Planet>(),
                        allowOriginPlanet: true
                    )
                )
                    return false;

            if (IsWaiting)
            {
                if (
                    WaitingTicks <= 0
                    || Mission.Participants.Any(participant =>
                        participant.GetParent() is not Planet planet
                        || planet.InstanceID != Destination.InstanceID
                        || participant.IsOnMission()
                        || participant.GetOwnerInstanceID() != context.Faction.InstanceID
                    )
                )
                    return false;
                directTicks = Mission.EstimateTravelTicks(context, Destination);
                stagedTicks = WaitingTicks + directTicks;
                return true;
            }

            if (!Mission.CanExecute(context))
                return false;

            int relocationTicks = 0;
            foreach (
                IGrouping<ISceneNode, IMissionParticipant> group in Participants.GroupBy(
                    participant => participant.GetParent()
                )
            )
            {
                if (group.Key?.InstanceID == Destination.InstanceID)
                    continue;
                if (
                    !context.MovementQueries.TryGetSelectionTransitTicks(
                        group.Cast<ISceneNode>().ToArray(),
                        Destination,
                        context.Faction.InstanceID,
                        out int groupTicks
                    )
                )
                    return false;
                relocationTicks = Math.Max(relocationTicks, groupTicks);
            }

            directTicks = Mission.EstimateTravelTicks(context);
            stagedTicks = relocationTicks + Mission.EstimateTravelTicks(context, Destination);
            return stagedTicks < directTicks;
        }

        /// <summary>
        /// Estimates comparable travel costs before performing full movement validation.
        /// </summary>
        /// <param name="context">The current faction turn containing the available team.</param>
        /// <returns>The direct round trip and the round trip including relocation.</returns>
        internal (long DirectTicks, long StagedTicks) EstimateTravelTicks(AITurnContext context)
        {
            int relocation = 0;
            foreach (
                IGrouping<ISceneNode, IMissionParticipant> group in Participants.GroupBy(
                    participant => participant.GetParent()
                )
            )
            {
                int percent = MovementQueries.GetPersonnelGroupTravelTimePercent(
                    group.Cast<IMovable>().ToArray()
                );
                foreach (IMissionParticipant participant in group)
                {
                    Planet origin = participant.GetParentOfType<Planet>();
                    if (origin.InstanceID != Destination.InstanceID)
                        relocation = Math.Max(
                            relocation,
                            context.MovementQueries.CalculateTransitTicks(
                                participant,
                                origin,
                                Destination,
                                percent
                            )
                        );
                }
            }
            return (
                Mission.EstimateTravelTicks(context),
                relocation + Mission.EstimateTravelTicks(context, Destination)
            );
        }
    }
}
