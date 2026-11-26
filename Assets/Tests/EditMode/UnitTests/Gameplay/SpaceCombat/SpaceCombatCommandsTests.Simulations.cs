using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Tactical;

namespace Rebellion.Tests.Gameplay
{
    public partial class SpaceCombatCommandsTests
    {
        [TestCase("gunnery")]
        [TestCase("carrier")]
        [TestCase("withdrawal")]
        [TestCase("auto")]
        public void RecordSeededCombatSimulation(string scenario)
        {
            (GameRoot game, SpaceCombatCommands commands, CapitalShip attacker) =
                CreateTacticalEncounter();
            game.UseDeterministicRuntimeIdentifiers = true;
            Fleet fleet = attacker.GetParentOfType<Fleet>();
            Planet planet = fleet.GetParentOfType<Planet>();
            if (scenario == "carrier")
            {
                Starfighter fighter = TacticalTestData.CreateFighter();
                fighter.InstanceID = "launch-squadron";
                fighter.OwnerInstanceID = attacker.OwnerInstanceID;
                fighter.Hyperdrive = 0;
                attacker.StarfighterCapacity = 2;
                game.AttachNode(fighter, attacker);
                foreach (CapitalShip enemy in planet.GetChildren<CapitalShip>(recursive: true))
                    if (enemy != attacker)
                        enemy.MaxHullStrength = enemy.CurrentHullStrength = 2000;
            }
            if (scenario == "withdrawal")
            {
                CreatePlanet(game, "withdrawal-home", "FNEMP1");
                foreach (int[] battery in attacker.PrimaryWeapons.Values)
                    for (int arc = 0; arc < 4; arc++)
                        battery[arc] = 0;
            }

            List<string> trace = new();
            List<GameResult> results;
            if (scenario == "auto")
            {
                results = commands.ResolvePending(autoResolve: true);
            }
            else
            {
                TacticalCombatSession battle = commands.BeginTacticalCombat();
                int frame = 0;
                battle.ShotFired += shot =>
                    trace.Add(
                        FormattableString.Invariant(
                            $"shot {frame} {battle.SystemTime} {shot.Source.InstanceID} {shot.Target.InstanceID} {shot.Weapon} {shot.Strength:R} {shot.ImpactDelay}"
                        )
                    );
                battle.ImpactApplied += (unit, weapon, kind) =>
                    trace.Add(
                        $"impact {frame} {battle.SystemTime} {unit.InstanceID} {weapon} {kind}"
                    );
                battle.UnitDestroyed += unit => trace.Add($"destroyed {frame} {unit.InstanceID}");
                battle.UnitWithdrawn += unit => trace.Add($"withdrawn {frame} {unit.InstanceID}");
                battle.FighterRecovered += unit =>
                    trace.Add($"recovered {frame} {unit.InstanceID} {unit.CarrierID}");
                if (scenario is "gunnery" or "carrier")
                    battle.SetTarget(
                        battle.GetGroup(battle.GetUnit(attacker.InstanceID)),
                        battle.Units.Single(unit => unit.Side != battle.PlayerSide)
                    );
                if (scenario == "withdrawal")
                    Assert.IsTrue(battle.BeginWithdrawal(battle.PlayerSide));
                for (; frame < 6000 && !battle.IsComplete; frame++)
                {
                    if (frame == 1800 && scenario is "carrier" or "gunnery")
                        battle.RequestImmediateResult();
                    battle.Advance(50);
                    if (frame % 20 == 0 || battle.IsComplete)
                    {
                        foreach (CombatUnit unit in battle.Units)
                            trace.Add(
                                FormattableString.Invariant(
                                    $"unit {frame} {battle.SystemTime} {unit.InstanceID} {unit.Status} {unit.CarrierID} {unit.Position.X:R} {unit.Position.Y:R} {unit.Position.Z:R} {unit.Systems.Hull:R} {unit.Systems.Shields:R}"
                                )
                            );
                    }
                }
                Assert.IsTrue(battle.IsComplete, scenario);
                results = commands.CompleteTacticalCombat(battle);
            }

            SpaceCombatResult result = results.OfType<SpaceCombatResult>().Single();
            trace.Add(
                $"result {result.Winner} {result.AttackerOutcome} {result.DefenderOutcome} {result.IterationsCompleted}"
            );
            foreach (ShipDamageResult damage in result.ShipDamage)
                trace.Add(
                    $"damage {damage.Ship.InstanceID} {damage.HullBefore} {damage.HullAfter} {damage.SystemDamage?.Total}"
                );
            foreach (FighterLossResult loss in result.FighterLosses)
                trace.Add($"loss {loss.Fighter.InstanceID} {loss.SquadsBefore} {loss.SquadsAfter}");
            foreach (FighterRecoveryResult recovery in result.FighterRecovery)
                trace.Add($"landing {recovery.Fighter.InstanceID} {recovery.Carrier?.InstanceID}");
            RecordReportRows(trace, "attack", result.AttackingUnits);
            RecordReportRows(trace, "defend", result.DefendingUnits);
            foreach (GameResult item in result.Events)
            {
                string destroyed = (item as GameObjectDestroyedResult)?.DestroyedObject?.InstanceID;
                trace.Add($"battle-event {item.GetType().Name} {item.Tick} {destroyed}");
            }
            foreach (GameResult item in results)
                trace.Add($"event {item.GetType().Name} {item.Tick}");
            foreach (CapitalShip ship in game.GetSceneNodesByType<CapitalShip>())
                trace.Add(
                    $"ship {ship.InstanceID} {ship.ParentInstanceID} {ship.CurrentHullStrength} {ship.SystemDamage.Total} {ship.NextRepairTick}"
                );
            foreach (Starfighter fighter in game.GetSceneNodesByType<Starfighter>())
                trace.Add(
                    $"fighter {fighter.InstanceID} {fighter.ParentInstanceID} {fighter.CurrentSquadronSize}"
                );
            foreach (Fleet remaining in game.GetSceneNodesByType<Fleet>())
                trace.Add(
                    $"fleet {remaining.InstanceID} {remaining.ParentInstanceID} {remaining.Movement?.TicksRemaining()}"
                );
            trace.Add($"campaign-random-index {game.RandomIndex}");
            string outputDirectory = Environment.GetEnvironmentVariable(
                "REBELLION_BATTLE_COMPARISON"
            );
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllLines(Path.Combine(outputDirectory, scenario + ".txt"), trace);
            }
        }

        private static void RecordReportRows(
            List<string> trace,
            string side,
            List<CombatUnitSnapshot> units
        )
        {
            foreach (CombatUnitSnapshot row in units)
                trace.Add(
                    $"report {side} {row.Unit.InstanceID} {row.Unit.ParentInstanceID} {row.WasOperational} {row.Damaged} {row.Destroyed} {row.Captured}"
                );
        }
    }
}
