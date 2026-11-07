using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatUnitTests
    {
        [TestCase(0, 1, 0, true)]
        [TestCase(1, 1, 1, true)]
        [TestCase(1, 1, 2, false)]
        public void Constructor_FighterDriveDamage_RetainsBackupAvailability(
            int primary,
            int backup,
            int damaged,
            bool expected
        )
        {
            Starfighter fighter = new()
            {
                InstanceID = "fighter",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                Hyperdrive = primary,
            };
            TacticalCombatUnit unit = new(
                fighter,
                CombatSide.Attacker,
                1,
                new TacticalCombatUnitDefinition { BackupHyperdrive = backup }
            );
            for (int index = 0; index < damaged; index++)
                unit.Systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);

            Assert.AreEqual(expected, unit.Systems.HasHyperdrive);
        }
    }
}
