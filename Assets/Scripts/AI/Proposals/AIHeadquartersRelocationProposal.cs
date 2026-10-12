using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Frees destination capacity when required and relocates a threatened mobile headquarters.
    /// </summary>
    public sealed class AIHeadquartersRelocationProposal : AIProposal
    {
        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        public Building Headquarters { get; }

        public Planet Origin { get; }

        public Planet Destination { get; }

        public Building BuildingToRemove { get; }

        public int ThreatStrength { get; }

        public int DefenseStrength { get; }

        /// <summary>
        /// Creates a headquarters-relocation proposal.
        /// </summary>
        /// <param name="headquarters">The mobile headquarters building.</param>
        /// <param name="origin">The current headquarters planet.</param>
        /// <param name="destination">The selected destination planet.</param>
        /// <param name="buildingToRemove">The destination building to remove, if capacity is full.</param>
        /// <param name="threatStrength">The known incoming hostile fleet strength.</param>
        /// <param name="defenseStrength">The ready headquarters orbital defense strength.</param>
        public AIHeadquartersRelocationProposal(
            Building headquarters,
            Planet origin,
            Planet destination,
            Building buildingToRemove,
            int threatStrength,
            int defenseStrength
        )
        {
            Headquarters = headquarters;
            Origin = origin;
            Destination = destination;
            BuildingToRemove = buildingToRemove;
            ThreatStrength = threatStrength;
            DefenseStrength = defenseStrength;
        }

        /// <summary>
        /// Returns a stable key for this headquarters relocation.
        /// </summary>
        /// <returns>The relocation sort key.</returns>
        public override string GetSortKey()
        {
            return $"headquarters-relocation:{Origin?.InstanceID}:{Destination?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the headquarters remains threatened and the destination remains usable.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the headquarters remains threatened and the destination remains usable.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Creates destination capacity when necessary and starts headquarters relocation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            if (
                !context.HeadquartersQueries.CanRelocate(Headquarters, Destination)
                && !TryRemoveBuilding(context)
            )
                return;

            if (!context.HeadquartersQueries.CanRelocate(Headquarters, Destination))
                return;

            context.Headquarters.TryRelocate(Headquarters, Destination);
        }

        /// <summary>
        /// Removes the selected destination building through the applicable application command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when destination capacity was created.</returns>
        private bool TryRemoveBuilding(AITurnContext context)
        {
            if (BuildingToRemove.ManufacturingStatus == ManufacturingStatus.Building)
            {
                return context.Manufacturing.CancelManufacturing(
                    BuildingToRemove,
                    context.Faction.InstanceID
                );
            }

            return BuildingToRemove.ManufacturingStatus == ManufacturingStatus.Complete
                && context.Maintenance.TryScrap(
                    new List<IManufacturable> { BuildingToRemove },
                    context.Faction.InstanceID
                );
        }

        /// <summary>
        /// Returns whether the live game state still supports this relocation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the threat, headquarters, and destination remain valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                context?.Game == null
                || context.Faction?.Settings?.Headquarters?.IsMobile != true
                || context.Assessment == null
                || context.Headquarters == null
                || context.HeadquartersQueries == null
                || context.Maintenance == null
                || context.Manufacturing == null
                || context.Game.GetSceneNodeByInstanceID<Building>(Headquarters?.InstanceID)
                    != Headquarters
                || context.Game.GetSceneNodeByInstanceID<Planet>(Origin?.InstanceID) != Origin
                || context.Game.GetSceneNodeByInstanceID<Planet>(Destination?.InstanceID)
                    != Destination
                || Headquarters.GetParent() != Origin
                || Origin.GetOwnerInstanceID() != context.Faction.InstanceID
                || Destination.GetOwnerInstanceID() != context.Faction.InstanceID
                || !Destination.IsColonized
                || Destination.IsDestroyed
                || MovementQueries.IsBlockedFromDestinationByBlockade(Headquarters, Destination)
                || context.Assessment.HasKnownHostileFleet(Destination)
                || context.Assessment.GetKnownIncomingHostileFleetStrength(Origin)
                    <= context.Assessment.GetHeadquartersOrbitalDefenseStrength(Origin)
            )
                return false;

            if (context.HeadquartersQueries.CanRelocate(Headquarters, Destination))
                return true;

            return BuildingToRemove != null
                && context.Game.GetSceneNodeByInstanceID<Building>(BuildingToRemove.InstanceID)
                    == BuildingToRemove
                && BuildingToRemove.GetParent() == Destination
                && BuildingToRemove.GetOwnerInstanceID() == context.Faction.InstanceID
                && BuildingToRemove.GetBuildingType() != BuildingType.Headquarters
                && BuildingToRemove.Movement == null
                && BuildingToRemove.ManufacturingStatus
                    is ManufacturingStatus.Building
                        or ManufacturingStatus.Complete
                && Destination.GetAvailableEnergy() == 0;
        }
    }
}
