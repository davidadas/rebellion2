using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatStrengthCalculatorTests
    {
        [TestCase(0.5, 30)]
        [TestCase(0.25, 15)]
        public void GetCapitalShipFirepowerAgainstStarfighters_ConfiguredMultiplier_ScalesTurbolasers(
            double multiplier,
            int expectedFirepower
        )
        {
            CapitalShip ship = CreateTurbolaserShip();
            GameConfig.SpaceCombatConfig config = new GameConfig.SpaceCombatConfig
            {
                TurbolaserFighterDamageMultiplier = multiplier,
            };

            int firepower =
                SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstStarfighters(
                    ship,
                    config
                );

            Assert.AreEqual(expectedFirepower, firepower);
        }

        [Test]
        public void IsArmedEscort_DisabledTurbolaserEffectiveness_RejectsTurbolaserOnlyShip()
        {
            CapitalShip ship = CreateTurbolaserShip();
            GameConfig.SpaceCombatConfig config = new GameConfig.SpaceCombatConfig
            {
                TurbolaserFighterDamageMultiplier = 0,
            };

            bool isEscort = SpaceCombatStrengthCalculator.IsArmedEscort(ship, config);

            Assert.IsFalse(isEscort);
        }

        /// <summary>
        /// Creates a capital ship whose fighter-target strength comes entirely from turbolasers.
        /// </summary>
        /// <returns>The configured capital ship.</returns>
        private static CapitalShip CreateTurbolaserShip()
        {
            CapitalShip ship = new CapitalShip { Maneuverability = 2, WeaponRecharge = 3 };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 4;
            return ship;
        }
    }
}
