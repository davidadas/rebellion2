using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

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

        [Test]
        public void RetainSession_ActiveEncounter_StoresSessionForNextTacticalScene()
        {
            PendingCombatResult encounter = CreateEncounter();
            TacticalBattleSession session = CreateSession(encounter);
            TacticalBattleLaunchContext.Open(encounter);

            TacticalBattleLaunchContext.RetainSession(session);

            Assert.IsTrue(TacticalBattleLaunchContext.HasRetainedSession);
            Assert.AreSame(session, TacticalBattleLaunchContext.TakeRetainedSession());
            Assert.IsFalse(TacticalBattleLaunchContext.HasRetainedSession);
        }

        [Test]
        public void RetainSession_DifferentEncounter_ThrowsArgumentException()
        {
            TacticalBattleLaunchContext.Open(CreateEncounter());
            TacticalBattleSession session = CreateSession(CreateEncounter());

            Assert.Throws<ArgumentException>(() =>
                TacticalBattleLaunchContext.RetainSession(session)
            );
        }

        [Test]
        public void Clear_RetainedSession_RemovesEncounterAndSession()
        {
            PendingCombatResult encounter = CreateEncounter();
            TacticalBattleLaunchContext.Open(encounter);
            TacticalBattleLaunchContext.RetainSession(CreateSession(encounter));

            TacticalBattleLaunchContext.Clear();

            Assert.IsNull(TacticalBattleLaunchContext.Encounter);
            Assert.IsFalse(TacticalBattleLaunchContext.HasRetainedSession);
        }

        private static PendingCombatResult CreateEncounter()
        {
            return new PendingCombatResult
            {
                AttackerFleet = CreateFleet(),
                DefenderFleet = CreateFleet(),
            };
        }

        private static TacticalBattleSession CreateSession(PendingCombatResult encounter)
        {
            return TacticalBattleSession.Create(encounter, new FixedRandomProvider(new[] { 0d }));
        }

        private static Fleet CreateFleet()
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            return new Fleet { CapitalShips = new List<CapitalShip> { ship } };
        }
    }
}
