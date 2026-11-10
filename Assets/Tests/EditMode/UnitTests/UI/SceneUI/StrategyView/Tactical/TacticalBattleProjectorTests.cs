using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Tactical
{
    [TestFixture]
    public class TacticalBattleProjectorTests
    {
        private const string _empireFactionId = "FNEMP1";
        private const string _allianceFactionId = "FNALL1";
        private static readonly RectInt _arena = new RectInt(0, 0, 640, 480);
        private GameObject _root;
        private TacticalBattleScene _scene;

        /// <summary>
        /// Builds the view whose scene projects battle positions for the projector.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _scene = TacticalBattleViewTestFactory.Create(out _root).Scene;
        }

        /// <summary>
        /// Destroys the test hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Project_LiveBattle_ProjectsUnitsStatusAndControls()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy,
                attackerCanWithdraw: true
            );
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                new[] { friendly },
                friendly,
                null,
                false,
                1f
            );

            Assert.AreEqual("Battle at Deep Space", data.Title);
            StringAssert.Contains("Empire: 1 ship, 0 squadrons", data.AttackerStatus);
            StringAssert.Contains("Alliance: 1 ship, 0 squadrons", data.DefenderStatus);
            Assert.IsNull(data.Superlaser);
            Assert.AreEqual("0:00", data.ClockStatus);
            Assert.AreEqual(2, data.Units.Count);
            TacticalUnitRenderData friendlyData = data.Units[0];
            TacticalUnitRenderData enemyData = data.Units[1];
            Assert.IsTrue(friendlyData.Selected);
            Assert.IsFalse(enemyData.Selected);
            Assert.Greater(friendlyData.BarColor.g, friendlyData.BarColor.r);
            Assert.Greater(enemyData.BarColor.r, enemyData.BarColor.g);
            Assert.Greater(friendlyData.Y, enemyData.Y);
            Assert.AreEqual(
                (float)(friendly.Heading * Mathf.Rad2Deg),
                friendlyData.RotationDegrees,
                0.001f
            );
            Assert.IsNull(friendlyData.Texture);
            Assert.AreEqual(friendlyData.Width, friendlyData.Height, 0.001f);
            Assert.AreEqual("Retreat", data.RetreatLabel);
            Assert.IsTrue(data.RetreatInteractable);
            Assert.AreEqual("Auto: Off", data.AutoLabel);
            Assert.IsTrue(data.AutoInteractable);
            Assert.AreEqual("Pause", data.PauseLabel);
            Assert.AreEqual("Speed 1x", data.SpeedLabel);
            Assert.AreEqual("Right-click to move or attack.", data.Hint);
            Assert.IsNotNull(data.Panel);
            Assert.AreEqual(friendly.DisplayName, data.Panel.Name);
            Assert.IsNull(data.SelectionBox);
            Assert.IsNull(data.PlanetTexture);
        }

        [Test]
        public void Project_CapitalShipPanel_ReportsBarsSystemsArcsAndOrders()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            Officer officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Admiral Ackbar",
                OwnerInstanceID = _empireFactionId,
            };
            friendly.Ship.AddChild(officer);
            officer.SetParent(friendly.Ship);
            battle.IssueAttackOrder(new[] { friendly }, enemy);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalPanelRenderData panel = projector.ProjectPanel(
                battle,
                friendly,
                null,
                "Empire",
                "Alliance"
            );

            Assert.AreEqual("Victory Destroyer", panel.Name);
            Assert.AreEqual("Empire", panel.Subtitle);
            Assert.IsTrue(panel.ShowShields);
            Assert.AreEqual(1f, panel.ShieldFraction, 0.001f);
            Assert.AreEqual("50 / 50", panel.ShieldText);
            Assert.AreEqual(1f, panel.HullFraction, 0.001f);
            Assert.AreEqual("100 / 100", panel.HullText);
            CollectionAssert.AreEqual(
                new[] { "Shields", "Engines", "Weapons", "Hyperdrive" },
                panel.Systems.Select(system => system.Label)
            );
            Assert.IsTrue(panel.Systems.All(system => system.State == TacticalSystemState.Online));
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 0f }, panel.ArcCharges);
            Assert.AreEqual(1, panel.Personnel.Count);
            Assert.AreEqual("Admiral Ackbar", panel.Personnel[0].Name);
            Assert.AreEqual("1 officer", panel.PersonnelText);
            Assert.AreEqual("Orders: Attack Corvette", panel.OrdersText);
            Assert.AreEqual("Tactics: Manual command", panel.TacticsText);
            Assert.AreEqual("Targets: Capital ships first", panel.TargetPriorityLabel);
            Assert.IsTrue(panel.TargetPriorityInteractable);
        }

        [Test]
        public void Project_EnemyPanel_ReportsUnarmedSystemsAndEnemyTactics()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out TacticalCapitalShipUnit enemy);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalPanelRenderData panel = projector.ProjectPanel(
                battle,
                enemy,
                null,
                "Empire",
                "Alliance"
            );

            Assert.AreEqual("Corvette", panel.Name);
            Assert.IsFalse(panel.ShowShields);
            Assert.AreEqual("None", panel.ShieldText);
            Assert.AreEqual(
                TacticalSystemState.Absent,
                panel.Systems.Single(system => system.Label == "Weapons").State
            );
            Assert.AreEqual("No personnel aboard", panel.PersonnelText);
            Assert.AreEqual("Tactics: Enemy vessel", panel.TacticsText);
        }

        [Test]
        public void Project_RetreatUnavailable_NamesTheReasonOnTheButtonAndInTheHint()
        {
            TacticalSpaceBattle blocked = CreateBattle(out _, out _);
            TacticalSpaceBattle interdicted = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy,
                attackerCanWithdraw: true
            );
            enemy.Ship.HasGravityWell = true;
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData blockedData = Project(
                projector,
                blocked,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                null,
                false,
                1f
            );
            TacticalBattleRenderData interdictedData = Project(
                projector,
                interdicted,
                _arena,
                null,
                new[] { friendly },
                friendly,
                null,
                false,
                1f
            );

            Assert.AreEqual("No Escape", blockedData.RetreatLabel);
            Assert.IsFalse(blockedData.RetreatInteractable);
            StringAssert.StartsWith("No retreat: no safe system to jump to. ", blockedData.Hint);
            Assert.AreEqual("Interdicted", interdictedData.RetreatLabel);
            Assert.IsFalse(interdictedData.RetreatInteractable);
            Assert.AreEqual(
                "Retreat blocked by an enemy gravity well. Right-click to move or attack.",
                interdictedData.Hint
            );
            Assert.AreEqual(
                "Retreat",
                TacticalBattleProjector.GetRetreatLabel(TacticalWithdrawalStatus.Available)
            );
            Assert.IsNull(
                TacticalBattleProjector.DescribeWithdrawalBlock(TacticalWithdrawalStatus.Available)
            );
        }

        [Test]
        public void Project_BattleOver_ShowsThePlainRetreatCaptionWhateverTheReasonWas()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            TacticalBattleProjector projector = new TacticalBattleProjector();
            TacticalBattleRenderData during = Project(
                projector,
                battle,
                _arena,
                null,
                new[] { friendly },
                friendly,
                null,
                false,
                1f
            );
            enemy.ApplyDamage(1000000, 0, CreateConfig(), new SystemRandomProvider(11));
            battle.Advance(1);

            TacticalBattleRenderData after = Project(
                projector,
                battle,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                null,
                false,
                1f
            );

            Assert.AreEqual("No Escape", during.RetreatLabel);
            Assert.IsTrue(battle.IsComplete);
            Assert.AreEqual("Retreat", after.RetreatLabel);
            Assert.IsFalse(after.RetreatInteractable);
            Assert.AreEqual("The engagement has ended.", after.Hint);
        }

        [Test]
        public void Project_Hints_NameShipsAndSquadronsForWhatTheyAre()
        {
            CapitalShip attackerShip = CreateShip("Carrier", 100, 10);
            attackerShip.OwnerInstanceID = _empireFactionId;
            CapitalShip defenderShip = CreateShip("Corvette", 100000, 0);
            defenderShip.OwnerInstanceID = _allianceFactionId;
            Starfighter first = CreateSquadron("tie", "TIE Fighter", 12);
            Starfighter second = CreateSquadron("tie", "TIE Fighter", 11);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(11),
                new[] { attackerShip },
                new[] { first, second },
                new[] { defenderShip },
                new List<Starfighter>(),
                true,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            TacticalBattleUnit ship = battle.Units.Single(unit => unit.Node == attackerShip);
            TacticalBattleUnit enemy = battle.Units.Single(unit => unit.Node == defenderShip);
            TacticalBattleUnit[] squadrons = battle
                .Units.Where(unit => unit.IsStarfighter)
                .ToArray();
            TacticalBattleProjector projector = new TacticalBattleProjector();

            string squadronsOnly = Project(
                projector,
                battle,
                _arena,
                null,
                squadrons,
                squadrons[0],
                null,
                false,
                1f
            ).Hint;
            string mixed = Project(
                projector,
                battle,
                _arena,
                null,
                new[] { ship, squadrons[0] },
                ship,
                null,
                false,
                1f
            ).Hint;
            string inspecting = Project(
                projector,
                battle,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                enemy,
                null,
                false,
                1f
            ).Hint;
            string deployingClock = projector
                .Project(
                    battle,
                    _arena,
                    _scene,
                    null,
                    null,
                    _empireFactionId,
                    _allianceFactionId,
                    "Empire",
                    "Alliance",
                    Array.Empty<TacticalBattleUnit>(),
                    null,
                    null,
                    false,
                    1f,
                    CombatSide.Attacker,
                    true
                )
                .ClockStatus;

            Assert.AreEqual("2 squadrons selected. Right-click an enemy to attack.", squadronsOnly);
            Assert.AreEqual(
                "1 ship and 1 squadron selected. Right-click to move or attack.",
                mixed
            );
            Assert.AreEqual(
                "Click one of your units to command it. Tab cycles your ships.",
                inspecting
            );
            StringAssert.EndsWith("Fleets deploying", deployingClock);
        }

        [Test]
        public void Project_WithdrawalOrdered_ShowsRetreatingAndDisablesControls()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _, attackerCanWithdraw: true);
            battle.OrderWithdrawal(CombatSide.Attacker);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                null,
                true,
                4f
            );

            Assert.AreEqual("Retreating", data.RetreatLabel);
            Assert.IsFalse(data.RetreatInteractable);
            Assert.IsFalse(data.AutoInteractable);
            Assert.AreEqual("Resume", data.PauseLabel);
            Assert.AreEqual("Speed 4x", data.SpeedLabel);
            StringAssert.Contains("PAUSED", data.ClockStatus);
            Assert.IsNull(data.Panel);
            Assert.Less(data.Units[0].Alpha, 1f);
        }

        [Test]
        public void Project_SelectionBox_ConvertsToArenaRelativeBounds()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            RectInt arena = new RectInt(10, 20, 600, 400);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                new RectInt(110, 220, 50, 40),
                false,
                1f
            );

            Assert.AreEqual(new RectInt(100, 200, 50, 40), data.SelectionBox);
        }

        [Test]
        public void Project_AfterWeaponFire_DrawsAShortBoltLeavingTheMuzzleColoredBySide()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            battle.Advance(TacticalSpaceBattle.RoundSeconds / 4);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                null,
                false,
                1f
            );

            Assert.AreEqual(1, data.Shots.Count);
            Assert.IsTrue(_scene.TryProject(friendly.X, friendly.Y, _arena, out Vector2 muzzle));
            Assert.IsTrue(_scene.TryProject(enemy.X, enemy.Y, _arena, out Vector2 impact));
            Vector2 bolt = new Vector2(data.Shots[0].X + _arena.x, data.Shots[0].Y + _arena.y);
            Assert.Greater(data.Shots[0].Length, 0f);
            Assert.Less(data.Shots[0].Length, Vector2.Distance(muzzle, impact) / 2f);
            Assert.Less(Vector2.Distance(bolt, muzzle), Vector2.Distance(bolt, impact));
            Assert.Greater(data.Shots[0].Color.g, data.Shots[0].Color.r);
        }

        [Test]
        public void GetShotColor_FactionThemes_UsesGreenForEmpireAndRedForAlliance()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            UIContext uiContext = CreateUIContext();

            Color empire = TacticalBattleProjector.GetShotColor(
                battle,
                CombatSide.Attacker,
                uiContext,
                _empireFactionId
            );
            Color alliance = TacticalBattleProjector.GetShotColor(
                battle,
                CombatSide.Defender,
                uiContext,
                _allianceFactionId
            );

            Assert.Greater(empire.g, empire.r);
            Assert.Greater(empire.g, empire.b);
            Assert.Greater(alliance.r, alliance.g);
            Assert.Greater(alliance.r, alliance.b);
            Assert.AreEqual(1f, empire.a);
            Assert.AreEqual(1f, alliance.a);
        }

        [Test]
        public void GetSideColor_FactionThemes_ColorsBarsByFactionNotBySide()
        {
            TacticalSpaceBattle battle = CreateBattle(out _, out _);
            UIContext uiContext = CreateUIContext();

            // The attacker is the player's side here; an Alliance attacker still gets a red bar.
            Color alliance = TacticalBattleProjector.GetSideColor(
                battle,
                CombatSide.Attacker,
                uiContext,
                _allianceFactionId
            );
            Color empire = TacticalBattleProjector.GetSideColor(
                battle,
                CombatSide.Defender,
                uiContext,
                _empireFactionId
            );
            Color fallback = TacticalBattleProjector.GetSideColor(
                battle,
                CombatSide.Attacker,
                null,
                null
            );

            Assert.Greater(alliance.r, alliance.g);
            Assert.Greater(empire.g, empire.r);
            Assert.AreEqual(1f, alliance.a);
            Assert.AreEqual(1f, empire.a);
            Assert.Greater(fallback.g, fallback.r);
        }

        [Test]
        public void Project_WithFactionThemes_DrawsEachShipsBarInItsFactionColor()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            UIContext uiContext = CreateUIContext();
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                uiContext,
                new[] { friendly },
                friendly,
                null,
                false,
                1f
            );

            Color empire = uiContext.ResolveFactionColor(_empireFactionId);
            Color alliance = uiContext.ResolveFactionColor(_allianceFactionId);
            TacticalUnitRenderData friendlyData = data.Units.Single(unit => unit.Selected);
            TacticalUnitRenderData enemyData = data.Units.Single(unit => !unit.Selected);
            Assert.AreEqual(empire.r, friendlyData.BarColor.r, 0.001f);
            Assert.AreEqual(empire.g, friendlyData.BarColor.g, 0.001f);
            Assert.AreEqual(alliance.r, enemyData.BarColor.r, 0.001f);
            Assert.AreEqual(alliance.g, enemyData.BarColor.g, 0.001f);
        }

        [Test]
        public void GetBoltColor_TorpedoIonAndLaser_AreGreyBlueAndFactionColored()
        {
            Color faction = new Color(0.9f, 0.2f, 0.2f, 1f);
            TacticalShot torpedo = new TacticalShot(
                CombatSide.Attacker,
                0,
                0,
                10,
                0,
                false,
                true,
                true,
                4,
                1
            );
            TacticalShot ion = new TacticalShot(
                CombatSide.Attacker,
                0,
                0,
                10,
                0,
                true,
                false,
                false,
                4,
                1
            );
            TacticalShot laser = new TacticalShot(
                CombatSide.Attacker,
                0,
                0,
                10,
                0,
                false,
                true,
                false,
                4,
                1
            );

            Color torpedoColor = TacticalBattleProjector.GetBoltColor(torpedo, faction);
            Color ionColor = TacticalBattleProjector.GetBoltColor(ion, faction);

            Assert.AreEqual(torpedoColor.r, torpedoColor.g, 0.05f);
            Assert.AreEqual(torpedoColor.g, torpedoColor.b, 0.05f);
            Assert.Greater(ionColor.b, ionColor.r);
            Assert.AreEqual(faction, TacticalBattleProjector.GetBoltColor(laser, faction));
        }

        [Test]
        public void GetBoltThickness_CapitalBolts_GrowWithPowerAndNeverVanish()
        {
            TacticalShot light = CreateShot(isIon: false, isStarfighter: false, strength: 10);
            TacticalShot heavy = CreateShot(isIon: false, isStarfighter: false, strength: 100);
            TacticalShot ion = CreateShot(isIon: true, isStarfighter: false, strength: 100);
            TacticalShot fighter = CreateShot(isIon: false, isStarfighter: true, strength: 8);

            Assert.Greater(
                TacticalBattleProjector.GetBoltThickness(heavy),
                TacticalBattleProjector.GetBoltThickness(light)
            );
            Assert.GreaterOrEqual(TacticalBattleProjector.GetBoltThickness(light), 1.5f);
            Assert.LessOrEqual(TacticalBattleProjector.GetBoltThickness(heavy), 7f);
            Assert.Less(
                TacticalBattleProjector.GetBoltThickness(fighter),
                TacticalBattleProjector.GetBoltThickness(light)
            );
            Assert.Less(
                TacticalBattleProjector.GetBoltThickness(ion),
                TacticalBattleProjector.GetBoltThickness(heavy)
            );
        }

        [Test]
        public void Project_Roster_ListsTheSidesShipsWithTheFocusHighlighted()
        {
            TacticalSpaceBattle battle = CreateBattle(
                out TacticalCapitalShipUnit friendly,
                out TacticalCapitalShipUnit enemy
            );
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                new[] { friendly },
                friendly,
                null,
                false,
                1f
            );

            Assert.AreEqual("Empire", data.Roster.SideLabel);
            Assert.AreEqual(1, data.Roster.Ships.Count);
            Assert.AreEqual("1", data.Roster.Ships[0].Label);
            Assert.IsTrue(data.Roster.Ships[0].Highlighted);
            Assert.IsTrue(data.Roster.Ships[0].Alive);
            Assert.IsEmpty(data.Roster.SquadronGroups);
            Assert.AreEqual(
                1,
                TacticalBattleProjector.GetRosterShips(battle, CombatSide.Defender).Count
            );
            Assert.AreSame(
                enemy,
                TacticalBattleProjector.GetRosterShips(battle, CombatSide.Defender)[0]
            );
        }

        [Test]
        public void Project_Roster_ListsEverySquadronGroupedByFighterType()
        {
            CapitalShip attackerShip = CreateShip("Carrier", 100, 10);
            attackerShip.OwnerInstanceID = _empireFactionId;
            CapitalShip defenderShip = CreateShip("Corvette", 100000, 0);
            defenderShip.OwnerInstanceID = _allianceFactionId;
            Starfighter firstXWing = CreateSquadron("x-wing", "X-Wing", 12);
            Starfighter yWing = CreateSquadron("y-wing", "Y-Wing", 11);
            Starfighter secondXWing = CreateSquadron("x-wing", "X-Wing", 10);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(11),
                new[] { attackerShip },
                new[] { firstXWing, yWing, secondXWing },
                new[] { defenderShip },
                new List<Starfighter>(),
                true,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            TacticalStarfighterUnit secondXWingUnit = battle
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == secondXWing);
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                new[] { secondXWingUnit },
                secondXWingUnit,
                null,
                false,
                1f
            );

            List<TacticalStarfighterUnit> squadrons = TacticalBattleProjector.GetRosterSquadrons(
                battle,
                CombatSide.Attacker
            );
            Assert.AreEqual(2, data.Roster.SquadronGroups.Count);
            CollectionAssert.AreEqual(
                new[] { "12", "10" },
                data.Roster.SquadronGroups[0].Select(slot => slot.Label).ToArray()
            );
            CollectionAssert.AreEqual(
                new[] { "11" },
                data.Roster.SquadronGroups[1].Select(slot => slot.Label).ToArray()
            );
            Assert.IsTrue(data.Roster.SquadronGroups[0][1].Highlighted);
            Assert.IsFalse(data.Roster.SquadronGroups[0][0].Highlighted);
            CollectionAssert.AreEqual(
                new[] { firstXWing, secondXWing, yWing },
                squadrons.Select(unit => unit.Fighter).ToArray()
            );
        }

        [Test]
        public void Project_PlayerCommandsAStation_ShowsTheSuperlaserBarAndNamesTheStation()
        {
            CapitalShip station = CreateShip("Death Star", 25000, 0);
            station.CanDestroyPlanets = true;
            CapitalShip escort = CreateShip("Escort", 100, 10);
            CapitalShip enemyShip = CreateShip("Corvette", 100000, 0);
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(11),
                new[] { station, escort },
                new List<Starfighter>(),
                new[] { enemyShip },
                new List<Starfighter>(),
                true,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            TacticalBattleUnit enemy = battle.Units.Single(unit =>
                unit.Side == CombatSide.Defender
            );
            TacticalBattleProjector projector = new TacticalBattleProjector();

            TacticalBattleRenderData data = Project(
                projector,
                battle,
                _arena,
                null,
                Array.Empty<TacticalBattleUnit>(),
                null,
                null,
                false,
                1f
            );
            TacticalSuperlaserRenderData armed = TacticalBattleProjector.ProjectSuperlaser(
                battle,
                true
            );
            battle.FireSuperlaser(battle.Stations[0], enemy);
            TacticalSuperlaserRenderData firing = TacticalBattleProjector.ProjectSuperlaser(
                battle,
                true
            );

            Assert.AreEqual("Superlaser ready: click to aim", data.Superlaser.Label);
            Assert.AreEqual(1f, data.Superlaser.ChargeFraction);
            Assert.IsTrue(data.Superlaser.Ready);
            Assert.IsFalse(data.Superlaser.Armed);
            StringAssert.Contains("Empire: Death Star, 1 ship, 0 squadrons", data.AttackerStatus);
            Assert.AreEqual("Superlaser: pick a target", armed.Label);
            Assert.IsTrue(armed.Armed);
            Assert.AreEqual("Superlaser firing", firing.Label);
            Assert.IsFalse(firing.Ready);
            Assert.IsFalse(firing.Armed);
            Assert.AreEqual(0f, firing.ChargeFraction);
        }

        [Test]
        public void DescribeStationHint_SquadronSelectedAgainstAStation_OffersTheTrenchRunOrSaysWhyNot()
        {
            Starfighter xWing = CreateSquadron("x-wing", "X-Wing", 12);
            xWing.Torpedoes = 4;
            Starfighter aWing = CreateSquadron("a-wing", "A-Wing", 12);
            TacticalSpaceBattle open = CreateStationBattle(xWing, aWing, shielded: false);
            TacticalSpaceBattle shielded = CreateStationBattle(
                CreateSquadron("x-wing", "X-Wing", 11),
                CreateSquadron("a-wing", "A-Wing", 11),
                shielded: true
            );
            TacticalStarfighterUnit torpedoSquadron = open
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == xWing);
            TacticalStarfighterUnit escortSquadron = open
                .Units.OfType<TacticalStarfighterUnit>()
                .Single(unit => unit.Fighter == aWing);
            TacticalBattleUnit ship = open.Units.First(unit => !unit.IsStarfighter);

            string offer = TacticalBattleProjector.DescribeStationHint(
                open,
                new[] { torpedoSquadron }
            );
            string unarmed = TacticalBattleProjector.DescribeStationHint(
                open,
                new[] { escortSquadron }
            );
            string blocked = TacticalBattleProjector.DescribeStationHint(
                shielded,
                shielded.Units.Where(unit => unit.IsStarfighter).ToList()
            );
            string shipsOnly = TacticalBattleProjector.DescribeStationHint(open, new[] { ship });

            Assert.AreEqual(
                "Right-click an enemy to attack, or the Death Star for a trench run (90% success).",
                offer
            );
            Assert.AreEqual(
                "Right-click an enemy to attack. A trench run on the Death Star needs torpedoes.",
                unarmed
            );
            Assert.AreEqual(
                "Right-click an enemy to attack. The Death Star is shielded from fighters.",
                blocked
            );
            Assert.IsNull(shipsOnly);
        }

        [Test]
        public void Project_NullBattle_ThrowsArgumentNullException()
        {
            TacticalBattleProjector projector = new TacticalBattleProjector();

            Assert.Throws<ArgumentNullException>(() =>
                Project(
                    projector,
                    null,
                    _arena,
                    null,
                    Array.Empty<TacticalBattleUnit>(),
                    null,
                    null,
                    false,
                    1f
                )
            );
        }

        /// <summary>
        /// Creates a battle in which the player's ship and two squadrons face an enemy battle
        /// station and its escort.
        /// </summary>
        /// <param name="first">The player's first squadron.</param>
        /// <param name="second">The player's second squadron.</param>
        /// <param name="shielded">Whether a planetary shield protects the station.</param>
        /// <returns>The created battle.</returns>
        private static TacticalSpaceBattle CreateStationBattle(
            Starfighter first,
            Starfighter second,
            bool shielded
        )
        {
            CapitalShip station = CreateShip("Death Star", 25000, 0);
            station.CanDestroyPlanets = true;
            return new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(11),
                new[] { CreateShip("Carrier", 100, 10) },
                new[] { first, second },
                new[] { station, CreateShip("Escort", 100000, 0) },
                new List<Starfighter>(),
                true,
                false,
                default,
                default,
                CombatSide.Attacker,
                shielded ? new[] { station } : null
            );
        }

        /// <summary>
        /// Projects a frame with the fixture's faction identities and names.
        /// </summary>
        /// <param name="projector">The projector under test.</param>
        /// <param name="battle">The battle to present.</param>
        /// <param name="arena">The source-space arena bounds.</param>
        /// <param name="uiContext">The strategy UI context, or null.</param>
        /// <param name="selection">The selected units.</param>
        /// <param name="focus">The inspected unit.</param>
        /// <param name="selectionBox">The drag-selection bounds.</param>
        /// <param name="paused">Whether playback is paused.</param>
        /// <param name="speed">The playback speed.</param>
        /// <returns>The projected frame.</returns>
        private TacticalBattleRenderData Project(
            TacticalBattleProjector projector,
            TacticalSpaceBattle battle,
            RectInt arena,
            UIContext uiContext,
            IReadOnlyCollection<TacticalBattleUnit> selection,
            TacticalBattleUnit focus,
            RectInt? selectionBox,
            bool paused,
            float speed
        )
        {
            _scene.Begin(null);
            _scene.Sync(battle, arena);
            _scene.Advance(10f);
            _scene.Sync(battle, arena);
            _scene.Zoom(-100f);
            return projector.Project(
                battle,
                arena,
                _scene,
                uiContext,
                null,
                _empireFactionId,
                _allianceFactionId,
                "Empire",
                "Alliance",
                selection,
                focus,
                selectionBox,
                paused,
                speed,
                CombatSide.Attacker
            );
        }

        /// <summary>
        /// Creates a ten-unit energy bolt from the attacker for presentation checks.
        /// </summary>
        /// <param name="isIon">Whether the bolt is ion fire.</param>
        /// <param name="isStarfighter">Whether a squadron fired it.</param>
        /// <param name="strength">The weapon strength behind it.</param>
        /// <returns>The shot.</returns>
        private static TacticalShot CreateShot(bool isIon, bool isStarfighter, int strength)
        {
            return new TacticalShot(
                CombatSide.Attacker,
                0,
                0,
                10,
                0,
                isIon,
                isStarfighter,
                false,
                strength,
                1
            );
        }

        /// <summary>
        /// Creates a UI context whose theme library carries the pack's faction colors.
        /// </summary>
        /// <returns>The UI context.</returns>
        private static UIContext CreateUIContext()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _empireFactionId });
            game.GetFactions().Add(new Faction { InstanceID = _allianceFactionId });
            return TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
        }

        /// <summary>
        /// Creates a battle between one shielded, armed player ship and one passive enemy ship.
        /// </summary>
        /// <param name="friendly">Receives the player's ship.</param>
        /// <param name="enemy">Receives the enemy ship.</param>
        /// <param name="attackerCanWithdraw">Whether the player can withdraw.</param>
        /// <returns>The created battle.</returns>
        private static TacticalSpaceBattle CreateBattle(
            out TacticalCapitalShipUnit friendly,
            out TacticalCapitalShipUnit enemy,
            bool attackerCanWithdraw = false
        )
        {
            CapitalShip attackerShip = CreateShip("Victory Destroyer", 100, 10);
            attackerShip.MaxShieldStrength = 50;
            attackerShip.OwnerInstanceID = _empireFactionId;
            CapitalShip defenderShip = CreateShip("Corvette", 100000, 0);
            defenderShip.Hyperdrive = 0;
            defenderShip.OwnerInstanceID = _allianceFactionId;
            TacticalSpaceBattle battle = new TacticalSpaceBattle(
                CreateConfig(),
                new SystemRandomProvider(11),
                new[] { attackerShip },
                new List<Starfighter>(),
                new[] { defenderShip },
                new List<Starfighter>(),
                attackerCanWithdraw,
                false,
                default,
                default,
                CombatSide.Attacker
            );
            friendly = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == attackerShip);
            enemy = battle
                .Units.OfType<TacticalCapitalShipUnit>()
                .Single(unit => unit.Ship == defenderShip);
            return battle;
        }

        /// <summary>
        /// Creates a laser-armed squadron of a fighter type.
        /// </summary>
        /// <param name="typeId">The fighter type identifier that groups it in the roster.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="size">The current squadron size, which labels its roster slot.</param>
        /// <returns>The created squadron.</returns>
        private static Starfighter CreateSquadron(string typeId, string displayName, int size)
        {
            return new Starfighter
            {
                InstanceID = $"{typeId}-{size}",
                TypeID = typeId,
                DisplayName = displayName,
                MaxSquadronSize = 12,
                CurrentSquadronSize = size,
                Hyperdrive = 1,
                SublightSpeed = 5,
                Agility = 3,
                LaserCannon = 5,
                LaserRange = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates combat parameters without automatic retreat.
        /// </summary>
        /// <returns>The combat parameters.</returns>
        private static GameConfig.SpaceCombatConfig CreateConfig()
        {
            return new GameConfig.SpaceCombatConfig
            {
                AdmiralLeadershipDivisor = 10,
                CommanderCombatDivisor = 20,
                LaserCannonCapitalDamageMultiplier = 1.0 / 6.0,
                AutoResolveFighterWeaponRechargeMultiplier = 3.751,
                AutoResolveMaximumIterations = 4096,
                AutoResolveStagnationIterations = 1200,
                AutoResolveRetreatStrengthRatio = 0,
                AutoResolveMinimumManeuverRatio = 0.1,
                AutoResolveTargetScanDivisor = 3,
                AutoResolveStartingDistance = 24,
                AutoResolveWithdrawalDistance = 10,
                AutoResolveComponentDamageInterval = 1,
                AutoResolveComponentDamageRollMaximum = 10,
                AutoResolveComponentDelayMinimum = 30,
                AutoResolveComponentDelayMaximum = 50,
                AutoResolveComponentDelayRecovery = 1,
            };
        }

        /// <summary>
        /// Creates a capital ship with one forward turbolaser lane that reaches across the arena.
        /// </summary>
        /// <param name="displayName">The display name and instance identifier.</param>
        /// <param name="hull">The hull strength.</param>
        /// <param name="weaponStrength">The forward turbolaser strength, or zero for unarmed.</param>
        /// <returns>The created ship.</returns>
        private static CapitalShip CreateShip(string displayName, int hull, int weaponStrength)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = displayName,
                DisplayName = displayName,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                Hyperdrive = 1,
                SublightSpeed = 3,
                Maneuverability = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                WeaponRecharge = Math.Max(weaponStrength, 0),
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = weaponStrength;
            if (weaponStrength > 0)
                ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][4] = 150;
            return ship;
        }
    }
}
