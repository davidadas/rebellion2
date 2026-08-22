using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerTests
    {
        [Test]
        public void Bind_UnitArrivedTrigger_BindsAuthoredValues()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Planet destination = new Planet { InstanceID = "planet" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:unit.arrived",
                ("Unit", "unit"),
                ("Destination", "destination")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new UnitArrivedResult { Unit = officer, Destination = destination }
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("unit"));
            Assert.AreSame(destination, context.GetBinding<Planet>("destination"));
        }

        [Test]
        public void Bind_DuelCompletedTrigger_BindsAuthoredValues()
        {
            Officer encountered = new Officer { InstanceID = "encountered" };
            Officer opponent = new Officer { InstanceID = "opponent" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:duel.completed",
                ("Officer", "officer"),
                ("Opponent", "opponent")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new DuelResult { EncounteredOfficer = encountered, OpposingOfficer = opponent }
            );

            Assert.AreSame(encountered, context.GetBinding<Officer>("officer"));
            Assert.AreSame(opponent, context.GetBinding<Officer>("opponent"));
        }

        [Test]
        public void Bind_OfficerCaptureChangedTrigger_BindsAuthoredValues()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Officer linkedOfficer = new Officer { InstanceID = "linked" };
            Planet planet = new Planet { InstanceID = "planet" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:officer.capture-changed",
                ("Officer", "officer"),
                ("LinkedOfficer", "linkedOfficer"),
                ("Context", "context")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    LinkedOfficer = linkedOfficer,
                    Context = planet,
                }
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
            Assert.AreSame(linkedOfficer, context.GetBinding<Officer>("linkedOfficer"));
            Assert.AreSame(planet, context.GetBinding<Planet>("context"));
        }

        [Test]
        public void Bind_MissionCompletedTrigger_BindsAuthoredValues()
        {
            Mission mission = new StubMission();
            GameEventTrigger trigger = new GameEventTrigger(
                "core:mission.completed",
                ("Mission", "mission")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new MissionCompletedResult { Mission = mission }
            );

            Assert.AreSame(mission, context.GetBinding<Mission>("mission"));
        }

        [TestCase("core:planet.owner-changed", "Planet", typeof(Planet))]
        [TestCase("core:unit.owner-changed", "Unit", typeof(ISceneNode))]
        [TestCase("core:unit.created", "Unit", typeof(IGameEntity))]
        [TestCase("core:unit.destroyed", "Unit", typeof(IGameEntity))]
        [TestCase("core:officer.killed", "Officer", typeof(Officer))]
        [TestCase("core:officer.injured", "Severity", typeof(int))]
        [TestCase("core:officer.recruited", "Faction", typeof(Faction))]
        [TestCase("core:combat.completed", "Planet", typeof(Planet))]
        [TestCase("core:bombardment.completed", "PlanetDestroyed", typeof(bool))]
        [TestCase("core:planetary-assault.completed", "Success", typeof(bool))]
        [TestCase("core:manufacturing.completed", "Unit", typeof(IGameEntity))]
        [TestCase("core:research.completed", "TechnologyTypeID", typeof(string))]
        [TestCase("core:uprising.started", "Planet", typeof(Planet))]
        [TestCase("core:uprising.ended", "Faction", typeof(Faction))]
        [TestCase("core:planet.stat-changed", "NewValue", typeof(int))]
        [TestCase("core:smuggling.changed", "NewPercent", typeof(int))]
        [TestCase("core:blockade.changed", "IsBlockaded", typeof(bool))]
        [TestCase("core:uprising.nearing", "Planet", typeof(Planet))]
        [TestCase("core:headquarters.destroyed", "Headquarters", typeof(Building))]
        [TestCase("core:planet.garrison-changed", "Planet", typeof(Planet))]
        [TestCase("core:planet.incident", "Severity", typeof(int))]
        [TestCase("core:intelligence.revealed", "RecipientFaction", typeof(Faction))]
        [TestCase("core:maintenance.required", "Amount", typeof(int))]
        [TestCase("core:research.exhausted", "Faction", typeof(Faction))]
        [TestCase("core:recruitment.exhausted", "Planet", typeof(Planet))]
        [TestCase("core:game.completed", "WinnerFaction", typeof(Faction))]
        [TestCase("core:planet-sectors.revealed", "PlanetSectors", typeof(List<PlanetSector>))]
        [TestCase("core:officer.rescued", "Officer", typeof(Officer))]
        [TestCase("core:officer.command-changed", "CommandKind", typeof(int))]
        [TestCase("core:officer.command-assigned", "CommandTarget", typeof(IGameEntity))]
        [TestCase("core:officer.traitor-discovered", "Officer", typeof(Officer))]
        [TestCase("core:force.training-completed", "Progress", typeof(int))]
        [TestCase("core:force.experience-gained", "ExperienceGained", typeof(int))]
        [TestCase("core:unit.deployed", "Unit", typeof(IGameEntity))]
        [TestCase("core:unit.movement-started", "Unit", typeof(IGameEntity))]
        [TestCase("core:unit.damaged", "Damage", typeof(int))]
        [TestCase("core:unit.destroyed-on-arrival", "Unit", typeof(IGameEntity))]
        [TestCase("core:unit.autoscrapped", "Unit", typeof(IGameEntity))]
        [TestCase("core:unit.sabotaged", "Unit", typeof(IGameEntity))]
        [TestCase("core:evacuation.completed", "Faction", typeof(Faction))]
        [TestCase("core:manufacturing.idle", "Planet", typeof(Planet))]
        public void AvailableArguments_GameplayTrigger_ExposesTypedArgument(
            string eventID,
            string argument,
            Type expectedType
        )
        {
            GameEventTrigger trigger = new GameEventTrigger(eventID);

            IReadOnlyDictionary<string, Type> arguments = trigger.AvailableArguments;

            Assert.AreEqual(expectedType, arguments[argument]);
            Assert.AreEqual(typeof(string), arguments["SourceEventInstanceID"]);
        }

        [Test]
        public void Bind_PlanetOwnershipChangedTrigger_BindsAuthoredValues()
        {
            Planet planet = new Planet { InstanceID = "planet" };
            Faction previousOwner = new Faction { InstanceID = "previous" };
            Faction newOwner = new Faction { InstanceID = "new" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:planet.owner-changed",
                ("Planet", "planet"),
                ("PreviousOwnerInstanceID", "previousOwnerInstanceID"),
                ("NewOwner", "newOwner")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new PlanetOwnershipChangedResult
                {
                    Planet = planet,
                    PreviousOwner = previousOwner,
                    NewOwner = newOwner,
                }
            );

            Assert.AreSame(planet, context.GetBinding<Planet>("planet"));
            Assert.AreEqual(
                previousOwner.InstanceID,
                context.GetBinding<string>("previousOwnerInstanceID")
            );
            Assert.AreSame(newOwner, context.GetBinding<Faction>("newOwner"));
        }

        private static GameEventExecutionContext CreateContext(
            GameEventTrigger trigger,
            GameResult result
        ) =>
            new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                result,
                trigger
            );
    }
}
