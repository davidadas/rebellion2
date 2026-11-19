using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Planet = global::Rebellion.Game.Galaxy.Planet;

namespace Rebellion.Tests.Gameplay
{
    public partial class SpaceCombatCommandsTests
    {
        private const string _networkBattleID = "812c65952335497dbe914570589edc44";

        [TestCase("FNEMP1")]
        [TestCase("FNALL1")]
        public void TrySubmitTacticalResponse_FirstHuman_RetainsDecisionWithoutStartingBattle(
            string factionID
        )
        {
            (_, SpaceCombatCommands commands, _) = CreateNetworkEncounter();

            Assert.IsTrue(
                commands.TrySubmitTacticalResponse(
                    factionID,
                    _networkBattleID,
                    42,
                    123,
                    out List<GameResult> results
                )
            );

            Assert.IsNull(results);
            Assert.IsNull(commands.ActiveTacticalCombat);
            Assert.IsNull(commands.ActiveTacticalBattleID);
            Assert.IsTrue(commands.TryGetPendingCombat(out PendingCombatResult pending));
            Assert.IsTrue(pending.RequiresBothResponses);
            Assert.IsTrue(
                pending.AttackerOwnerInstanceID == factionID
                    ? pending.AttackerHasResponded
                    : pending.DefenderHasResponded
            );
        }

        [Test]
        public void TrySubmitTacticalResponse_BothManual_UsesFirstEntryIdentityAndTwoCommanders()
        {
            (_, SpaceCombatCommands commands, _) = CreateNetworkEncounter();
            Assert.IsTrue(
                commands.TrySubmitTacticalResponse("FNALL1", _networkBattleID, 42, 123, out _)
            );

            Assert.IsTrue(
                commands.TrySubmitTacticalResponse(
                    "FNEMP1",
                    "5e62a708c5094e35ba5b197042ca0ff1",
                    99,
                    456,
                    out List<GameResult> results
                )
            );

            Assert.IsNull(results);
            Assert.AreEqual(_networkBattleID, commands.ActiveTacticalBattleID);
            TacticalCombatSession battle = commands.ActiveTacticalCombat;
            Assert.IsNotNull(battle);
            Assert.IsTrue(battle.IsSynchronized);
            Assert.IsFalse(battle.IsReplica);
            Assert.IsFalse(battle.GetSide(CombatSide.Attacker).IsAutomated);
            Assert.IsFalse(battle.GetSide(CombatSide.Defender).IsAutomated);
            Assert.IsTrue(commands.HasPendingDecision);
        }

        [TestCase("FNEMP1", true)]
        [TestCase("FNEMP1", false)]
        [TestCase("FNALL1", true)]
        [TestCase("FNALL1", false)]
        public void TrySubmitTacticalResponse_ManualAndAutomatic_RetainsEachFactionsChoice(
            string manualFaction,
            bool manualFirst
        )
        {
            (GameRoot game, SpaceCombatCommands commands, _) = CreateNetworkEncounter();
            string automaticFaction = manualFaction == "FNEMP1" ? "FNALL1" : "FNEMP1";
            if (manualFirst)
            {
                Assert.IsTrue(
                    commands.TrySubmitTacticalResponse(
                        manualFaction,
                        _networkBattleID,
                        42,
                        123,
                        out _
                    )
                );
                Assert.IsTrue(commands.TrySubmitResponse(automaticFaction, false, out _));
            }
            else
            {
                Assert.IsTrue(commands.TrySubmitResponse(automaticFaction, false, out _));
                Assert.IsTrue(
                    commands.TrySubmitTacticalResponse(
                        manualFaction,
                        _networkBattleID,
                        42,
                        123,
                        out _
                    )
                );
            }

            TacticalCombatSession battle = commands.ActiveTacticalCombat;
            Assert.IsNotNull(battle);
            foreach (TacticalCombatUnit unit in battle.Units)
            {
                CapitalShip ship = game.GetSceneNodeByInstanceID<CapitalShip>(unit.InstanceID);
                Assert.AreEqual(
                    ship.OwnerInstanceID == automaticFaction,
                    battle.GetSide(unit.Side).IsAutomated
                );
            }
        }

        [Test]
        public void ResolvePending_ActiveManualBattle_RejectsCompetingResolution()
        {
            (_, SpaceCombatCommands commands, _) = CreateNetworkEncounter();
            AdmitNetworkBattle(commands);

            Assert.Throws<InvalidOperationException>(() => commands.ResolvePending(true));
            Assert.Throws<InvalidOperationException>(() =>
                commands.ResolvePendingRetreat("FNEMP1")
            );
            Assert.IsFalse(commands.TrySubmitResponse("FNALL1", true, out _));
            Assert.AreEqual(_networkBattleID, commands.ActiveTacticalBattleID);
            Assert.IsTrue(commands.HasPendingDecision);
        }

        [Test]
        public void TryCompleteNetworkTacticalCombat_FinalOutcome_ImportsOnceAndPreservesEntryReport()
        {
            (GameRoot game, SpaceCombatCommands commands, CapitalShip attacker) =
                CreateNetworkEncounter();
            TacticalCombatSession battle = AdmitNetworkBattle(commands);
            battle.GetUnit(attacker.InstanceID).Systems.ApplyHullLoss(100);
            TacticalCombatOutcome outcome = FinishNetworkBattle(game, battle);
            Assert.AreEqual(1000, attacker.CurrentHullStrength);

            Assert.IsTrue(
                commands.TryCompleteNetworkTacticalCombat(
                    _networkBattleID,
                    outcome,
                    out List<GameResult> results
                )
            );

            SpaceCombatResult result = results.OfType<SpaceCombatResult>().Single();
            ShipDamageResult damage = result.ShipDamage.Single(item => item.Ship == attacker);
            Assert.AreEqual(1000, damage.HullBefore);
            Assert.AreEqual(
                outcome.Ships.Single(item => item.UnitID == attacker.InstanceID).Hull,
                damage.HullAfter
            );
            Assert.AreEqual(damage.HullAfter, attacker.CurrentHullStrength);
            Assert.IsFalse(commands.HasPendingDecision);
            Assert.IsNull(commands.ActiveTacticalCombat);
            Assert.IsNull(commands.ActiveTacticalBattleID);
            Assert.AreSame(
                attacker,
                game.GetSceneNodeByInstanceID<CapitalShip>(attacker.InstanceID)
            );
            Assert.IsFalse(
                commands.TryCompleteNetworkTacticalCombat(_networkBattleID, outcome, out _)
            );
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("hull")]
        [TestCase("components")]
        [TestCase("flags")]
        [TestCase("instant")]
        [TestCase("squadron")]
        [TestCase("carrier")]
        [TestCase("battle")]
        public void TryCompleteNetworkTacticalCombat_InvalidOutcome_RejectsBeforeCampaignMutation(
            string invalidField
        )
        {
            (GameRoot game, SpaceCombatCommands commands, CapitalShip attacker) =
                CreateNetworkEncounter();
            CreateDeploymentFighter(
                game,
                attacker.GetParentOfType<Planet>(),
                "FNEMP1",
                "network-fighter",
                "SFEM01"
            );
            TacticalCombatSession battle = AdmitNetworkBattle(commands);
            TacticalCombatOutcome outcome = FinishNetworkBattle(game, battle);
            string battleID = _networkBattleID;
            switch (invalidField)
            {
                case "missing":
                    outcome.Ships = outcome.Ships.Take(1).ToArray();
                    break;
                case "duplicate":
                    outcome.Ships[1].UnitID = outcome.Ships[0].UnitID;
                    break;
                case "unknown":
                    outcome.Ships[0].UnitID = "another-encounter-ship";
                    break;
                case "hull":
                    outcome.Ships[0].Hull = int.MaxValue;
                    break;
                case "components":
                    outcome.Ships[0].SystemDamage.Engines = 5;
                    break;
                case "flags":
                    outcome.TerminalFlags = new bool[6];
                    break;
                case "instant":
                    outcome.TerminalFlags[6] = true;
                    break;
                case "squadron":
                    outcome.Fighters[0].SquadronSize = int.MaxValue;
                    break;
                case "carrier":
                    outcome.Fighters[0].CarrierID = battle
                        .Units.Single(unit =>
                            game.NodesByInstanceID[unit.InstanceID] is CapitalShip ship
                            && ship.OwnerInstanceID == "FNALL1"
                        )
                        .InstanceID;
                    break;
                case "battle":
                    battleID = "a381ad6c94af496fb8981cd8315bfe88";
                    break;
            }

            Assert.IsFalse(commands.TryCompleteNetworkTacticalCombat(battleID, outcome, out _));

            Assert.AreEqual(1000, attacker.CurrentHullStrength);
            Assert.AreSame(
                attacker,
                game.GetSceneNodeByInstanceID<CapitalShip>(attacker.InstanceID)
            );
            Assert.IsTrue(commands.HasPendingDecision);
            Assert.AreSame(battle, commands.ActiveTacticalCombat);
            Assert.AreEqual(
                12,
                game.GetSceneNodeByInstanceID<Starfighter>("network-fighter").CurrentSquadronSize
            );
        }

        /// <summary>
        /// Binds both human commanders to the existing content-backed pending encounter.
        /// </summary>
        /// <returns>The campaign, command owner, and Empire ship.</returns>
        private (
            GameRoot Game,
            SpaceCombatCommands Commands,
            CapitalShip Attacker
        ) CreateNetworkEncounter()
        {
            var encounter = CreateTacticalEncounter();
            encounter.Game.Metadata.MultiplayerCampaign = new MultiplayerCampaignIdentity
            {
                HostUserID = "host-user",
                GuestUserID = "guest-user",
                HostFactionID = "FNEMP1",
                GuestFactionID = "FNALL1",
            };
            encounter.Game.LocalPlayerFactionID = "FNEMP1";
            encounter.Game.SetFactionController("FNALL1", "guest", PlayerControllerType.Human);
            return encounter;
        }

        /// <summary>
        /// Admits both explicit manual preferences before accessing the authoritative encounter.
        /// </summary>
        /// <param name="commands">The pending campaign combat owner.</param>
        /// <returns>The authoritative battle.</returns>
        private static TacticalCombatSession AdmitNetworkBattle(SpaceCombatCommands commands)
        {
            Assert.IsTrue(
                commands.TrySubmitTacticalResponse("FNEMP1", _networkBattleID, 42, 123, out _)
            );
            Assert.IsTrue(
                commands.TrySubmitTacticalResponse("FNALL1", _networkBattleID, 99, 456, out _)
            );
            Assert.IsNotNull(commands.ActiveTacticalCombat);
            return commands.ActiveTacticalCombat;
        }

        /// <summary>
        /// Runs actual weapon and terminal phases before capturing the authoritative outcome.
        /// </summary>
        /// <param name="game">The campaign containing the admitted participants.</param>
        /// <param name="battle">The admitted authoritative encounter.</param>
        /// <returns>The completed encounter's detached outcome.</returns>
        private static TacticalCombatOutcome FinishNetworkBattle(
            GameRoot game,
            TacticalCombatSession battle
        )
        {
            TacticalCombatUnit target = battle.Units.Single(unit =>
                game.NodesByInstanceID[unit.InstanceID] is CapitalShip ship
                && ship.OwnerInstanceID == "FNALL1"
            );
            TacticalCombatUnit attacker = battle.Units.Single(unit =>
                game.NodesByInstanceID[unit.InstanceID] is CapitalShip ship
                && ship.OwnerInstanceID == "FNEMP1"
            );
            battle.SetTarget(battle.GetRequestedGroup(attacker), target);
            for (int frame = 0; frame < 12000 && !battle.IsComplete; frame++)
                battle.Advance(50);
            Assert.IsTrue(
                battle.IsComplete,
                "The authoritative encounter must finish through normal combat."
            );
            return battle.CaptureSynchronizedOutcome();
        }
    }
}
