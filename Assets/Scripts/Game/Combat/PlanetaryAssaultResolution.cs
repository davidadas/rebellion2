using System.Collections.Generic;
using Rebellion.Game.Units;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Contains the resolved casualties, damage, and capture state from a planetary assault.
    /// </summary>
    internal sealed class PlanetaryAssaultResolution
    {
        internal int InitialAttackerRegimentCount { get; }
        internal int RemainingAttackerRegimentCount { get; }
        internal int InitialDefenderRegimentCount { get; }
        internal int RemainingDefenderRegimentCount { get; }
        internal int EnergyCapacityDamage { get; }
        internal int AllocatedEnergyDamage { get; }
        internal bool CapturesPlanet { get; }
        internal IReadOnlyList<Regiment> DestroyedAttackerRegiments { get; }
        internal IReadOnlyList<Regiment> DestroyedDefenderRegiments { get; }
        internal IReadOnlyList<Building> DestroyedBuildings { get; }
        internal IReadOnlyList<Regiment> RegimentsToLand { get; }

        /// <summary>
        /// Creates a resolved planetary-assault outcome.
        /// </summary>
        /// <param name="initialAttackerRegimentCount">The number of attacking regiments.</param>
        /// <param name="remainingAttackerRegimentCount">The number of surviving attackers.</param>
        /// <param name="initialDefenderRegimentCount">The number of defending regiments.</param>
        /// <param name="remainingDefenderRegimentCount">The number of surviving defenders.</param>
        /// <param name="energyCapacityDamage">The planet's lost energy capacity.</param>
        /// <param name="allocatedEnergyDamage">The planet's lost allocated energy.</param>
        /// <param name="capturesPlanet">Whether the attackers capture the planet.</param>
        /// <param name="destroyedAttackerRegiments">The destroyed attacking regiments.</param>
        /// <param name="destroyedDefenderRegiments">The destroyed defending regiments.</param>
        /// <param name="destroyedBuildings">The buildings destroyed by collateral damage.</param>
        /// <param name="regimentsToLand">The surviving regiments selected to garrison the planet.</param>
        internal PlanetaryAssaultResolution(
            int initialAttackerRegimentCount,
            int remainingAttackerRegimentCount,
            int initialDefenderRegimentCount,
            int remainingDefenderRegimentCount,
            int energyCapacityDamage,
            int allocatedEnergyDamage,
            bool capturesPlanet,
            IReadOnlyList<Regiment> destroyedAttackerRegiments,
            IReadOnlyList<Regiment> destroyedDefenderRegiments,
            IReadOnlyList<Building> destroyedBuildings,
            IReadOnlyList<Regiment> regimentsToLand
        )
        {
            InitialAttackerRegimentCount = initialAttackerRegimentCount;
            RemainingAttackerRegimentCount = remainingAttackerRegimentCount;
            InitialDefenderRegimentCount = initialDefenderRegimentCount;
            RemainingDefenderRegimentCount = remainingDefenderRegimentCount;
            EnergyCapacityDamage = energyCapacityDamage;
            AllocatedEnergyDamage = allocatedEnergyDamage;
            CapturesPlanet = capturesPlanet;
            DestroyedAttackerRegiments = destroyedAttackerRegiments;
            DestroyedDefenderRegiments = destroyedDefenderRegiments;
            DestroyedBuildings = destroyedBuildings;
            RegimentsToLand = regimentsToLand;
        }
    }
}
