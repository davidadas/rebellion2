using System;
using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Estimates when proposed fleet reinforcements will be manufactured and delivered.
    /// </summary>
    public sealed class AIReinforcementArrivalForecast
    {
        private readonly AITurnContext _context;
        private readonly Dictionary<
            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ),
            int
        > _arrivalTicks = new();

        /// <summary>
        /// Creates a turn-scoped reinforcement arrival forecast.
        /// </summary>
        /// <param name="context">The active AI turn context.</param>
        public AIReinforcementArrivalForecast(AITurnContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Estimates ticks until a proposed fleet reinforcement is ready at its destination.
        /// </summary>
        /// <param name="proposal">The manufacturing proposal to evaluate.</param>
        /// <returns>The estimated arrival ticks, or zero for non-fleet production.</returns>
        public int GetArrivalTicks(AIManufactureProposal proposal)
        {
            if (
                proposal?.Demand?.DestinationFleet == null
                || proposal.ProducerPlanet == null
                || proposal.Product?.GetReference() is not IManufacturable product
                || product is not IMovable movable
            )
            {
                return 0;
            }

            return GetArrivalTicks(
                proposal.ProducerPlanet,
                proposal.Demand.DestinationFleet,
                product,
                movable,
                proposal.GetManufacturingCount()
            );
        }

        /// <summary>
        /// Estimates ticks until a product manufactured by a producer reaches a fleet.
        /// </summary>
        /// <param name="producerPlanet">The planet manufacturing the product.</param>
        /// <param name="destinationFleet">The fleet receiving the product.</param>
        /// <param name="product">The product to manufacture.</param>
        /// <param name="movable">The product movement characteristics.</param>
        /// <param name="quantity">The number of products appended to the queue.</param>
        /// <returns>The estimated arrival ticks, or <see cref="int.MaxValue"/> when unavailable.</returns>
        public int GetArrivalTicks(
            Planet producerPlanet,
            Fleet destinationFleet,
            IManufacturable product,
            IMovable movable,
            int quantity
        )
        {
            if (
                producerPlanet == null
                || destinationFleet == null
                || product == null
                || movable == null
            )
                return int.MaxValue;

            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ) key = (
                producerPlanet.InstanceID,
                destinationFleet.InstanceID,
                product.GetTypeID(),
                quantity
            );
            if (_arrivalTicks.TryGetValue(key, out int cachedTicks))
                return cachedTicks;

            int manufacturingTicks =
                ManufacturingSystem.EstimateAppendedCompletionTicks(
                    producerPlanet,
                    product,
                    quantity
                ) ?? int.MaxValue;
            if (manufacturingTicks == int.MaxValue)
                return Cache(key, int.MaxValue);

            if (_context.Movement == null)
                return Cache(key, int.MaxValue);

            if (
                !_context.Movement.TryEstimateManufacturedTransitTicks(
                    movable,
                    producerPlanet,
                    destinationFleet,
                    out int transitTicks
                )
            )
            {
                return Cache(key, int.MaxValue);
            }

            long arrivalTicks = (long)manufacturingTicks + transitTicks;
            return Cache(key, arrivalTicks >= int.MaxValue ? int.MaxValue : (int)arrivalTicks);
        }

        /// <summary>
        /// Stores and returns one forecast value.
        /// </summary>
        /// <param name="key">The forecast cache key.</param>
        /// <param name="ticks">The arrival duration.</param>
        /// <returns>The supplied duration.</returns>
        private int Cache(
            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ) key,
            int ticks
        )
        {
            _arrivalTicks[key] = ticks;
            return ticks;
        }
    }
}
