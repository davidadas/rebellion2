using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerRegistryTests
    {
        [Test]
        public void Bind_UnitArrival_BindsUnitDestinationAndPlanet()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Planet destination = new Planet { InstanceID = "planet" };
            UnitArrivedResult arrival = new UnitArrivedResult
            {
                Unit = officer,
                Destination = destination,
            };

            GameEventExecutionContext context = CreateContext(
                "core:unit.arrived",
                arrival,
                ("Unit", "unit"),
                ("Destination", "destination")
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("unit"));
            Assert.AreSame(destination, context.GetBinding<Planet>("destination"));
        }

        [Test]
        public void Bind_OfficerEncounter_BindsBothOfficers()
        {
            Officer encountered = new Officer { InstanceID = "encountered" };
            Officer opponent = new Officer { InstanceID = "opponent" };
            GameEventExecutionContext context = CreateContext(
                "core:officer.encountered",
                new OfficerEncounterResult
                {
                    EncounteredOfficer = encountered,
                    OpposingOfficer = opponent,
                },
                ("Officer", "officer"),
                ("Opponent", "opponent")
            );

            Assert.AreSame(encountered, context.GetBinding<Officer>("officer"));
            Assert.AreSame(opponent, context.GetBinding<Officer>("opponent"));
        }

        [Test]
        public void Bind_OfficerCapture_BindsOfficerLinkAndContext()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Officer linkedOfficer = new Officer { InstanceID = "linked" };
            Planet planet = new Planet { InstanceID = "planet" };
            GameEventExecutionContext context = CreateContext(
                "core:officer.capture-changed",
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    LinkedOfficer = linkedOfficer,
                    Context = planet,
                },
                ("Officer", "officer"),
                ("LinkedOfficer", "linkedOfficer"),
                ("Context", "context")
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
            Assert.AreSame(linkedOfficer, context.GetBinding<Officer>("linkedOfficer"));
            Assert.AreSame(planet, context.GetBinding<Planet>("context"));
        }

        [Test]
        public void Bind_MissionCompletion_BindsMission()
        {
            Mission mission = new StubMission();
            GameEventExecutionContext context = CreateContext(
                "core:mission.completed",
                new MissionCompletedResult { Mission = mission },
                ("Mission", "mission")
            );

            Assert.AreSame(mission, context.GetBinding<Mission>("mission"));
        }

        private static GameEventExecutionContext CreateContext(
            string eventID,
            GameResult result,
            params (string Argument, string As)[] bindings
        )
        {
            GameEventTrigger trigger = new GameEventTrigger { Event = eventID };
            foreach ((string argument, string alias) in bindings)
            {
                trigger.Bindings.Add(
                    new GameEventTriggerBinding { Argument = argument, As = alias }
                );
            }

            return new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                result,
                trigger
            );
        }
    }
}
