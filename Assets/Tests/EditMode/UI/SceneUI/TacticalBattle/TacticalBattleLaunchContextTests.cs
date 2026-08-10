using System;
using NUnit.Framework;
using Rebellion.Game.Results;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalBattleLaunchContextTests
    {
        [TearDown]
        public void TearDown()
        {
            TacticalBattleLaunchContext.Clear();
        }

        [Test]
        public void Open_NullEncounter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TacticalBattleLaunchContext.Open(null));
        }

        [Test]
        public void Open_Encounter_StoresEncounterForScene()
        {
            PendingCombatResult encounter = new PendingCombatResult();

            TacticalBattleLaunchContext.Open(encounter);

            Assert.AreSame(encounter, TacticalBattleLaunchContext.Encounter);
        }

        [Test]
        public void Clear_StoredEncounter_RemovesEncounter()
        {
            TacticalBattleLaunchContext.Open(new PendingCombatResult());

            TacticalBattleLaunchContext.Clear();

            Assert.IsNull(TacticalBattleLaunchContext.Encounter);
        }
    }
}
