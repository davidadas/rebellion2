using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Deploys or retains an officer in an attack fleet's command post.
    /// </summary>
    public sealed class AIOfficerCommandProposal : AIProposal
    {
        public Officer Officer { get; }
        public Fleet Fleet { get; }
        public Planet TargetPlanet { get; }
        public OfficerRank Rank { get; }

        /// <summary>
        /// Creates a command assignment competing with the officer's mission opportunities.
        /// </summary>
        /// <param name="officer">The officer to deploy or retain.</param>
        /// <param name="fleet">The fleet receiving the officer.</param>
        /// <param name="targetPlanet">The fleet's attack target.</param>
        /// <param name="rank">The command post to fill.</param>
        public AIOfficerCommandProposal(
            Officer officer,
            Fleet fleet,
            Planet targetPlanet,
            OfficerRank rank
        )
        {
            Officer = officer;
            Fleet = fleet;
            TargetPlanet = targetPlanet;
            Rank = rank;
        }

        /// <summary>Returns the stable identity of this assignment.</summary>
        /// <returns>The officer, fleet, and command post key.</returns>
        public override string GetSortKey() =>
            $"officer-command:{Officer?.InstanceID}:{Fleet?.InstanceID}:{Rank}";

        /// <summary>Checks ownership, availability, the command vacancy, and deployment legality.</summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>Whether the assignment remains feasible.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            if (
                context?.OfficerCommands == null
                || !IsOwnedBy(context, Officer)
                || !IsOwnedBy(context, Fleet)
                || !Officer.IsActive()
                || !Fleet.IsActive()
                || TargetPlanet == null
                || Fleet.RoleType != FleetRoleType.Battle
                || Fleet.IsInCombat
                || Fleet.Order?.OrderType != FleetOrderType.Attack
                || Fleet.Order.TargetPlanetId != TargetPlanet.InstanceID
                || Fleet.Order.Status == FleetOrderStatus.Returning
                || !context.Faction.IsAvailableMissionParticipant(Officer)
                || Officer.IsRetired
                || Officer.InjuryPoints > 0
                || Officer.AllowedRanks?.Contains(Rank) != true
                || Rank is not (OfficerRank.Admiral or OfficerRank.Commander or OfficerRank.General)
            )
                return false;

            bool aboard = Officer.GetParentOfType<Fleet>() == Fleet;
            if (Officer.CurrentRank != OfficerRank.None && (!aboard || Officer.CurrentRank != Rank))
                return false;
            if (
                Fleet
                    .GetOfficers()
                    .Any(candidate => candidate != Officer && candidate.CurrentRank == Rank)
            )
                return false;
            if (aboard)
                return OfficerCommandQueries.CanChangeCommand(Officer, Rank);

            return context.Movement != null
                && Fleet.Movement == null
                && !Fleet
                    .GetOfficers()
                    .Any(candidate =>
                        candidate.Movement != null && candidate.AllowedRanks?.Contains(Rank) == true
                    )
                && context.MovementQueries.CanMoveGroup(new List<IMovable> { Officer }, Fleet);
        }

        /// <summary>Revalidates the assignment immediately before execution.</summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>Whether the appointment or deployment can execute.</returns>
        public override bool CanExecute(AITurnContext context) => CanSelect(context);

        /// <summary>Uses normal movement, appointing only after physical arrival.</summary>
        /// <param name="context">The current faction turn.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;
            if (Officer.GetParentOfType<Fleet>() != Fleet)
                context.Movement.TryRequestMove(
                    new ISceneNode[] { Officer },
                    Fleet,
                    context.Faction.InstanceID
                );
            if (
                Officer.GetParentOfType<Fleet>() == Fleet
                && Officer.CurrentRank != Rank
                && OfficerCommandQueries.CanChangeCommand(Officer, Rank)
            )
                context.OfficerCommands.TrySetRank(
                    Officer.InstanceID,
                    Rank,
                    context.Faction.InstanceID
                );
        }
    }
}
