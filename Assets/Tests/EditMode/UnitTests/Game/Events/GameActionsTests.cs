using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameActionsTests
    {
        [Test]
        public void PlaceUnits_SpawnSources_RoundTripsAuthoredStructure()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SPAWN_REINFORCEMENTS",
                Actions = new List<GameAction>
                {
                    new PlaceUnitsAction
                    {
                        DestinationInstanceID = "NABOO",
                        Units = new List<GameEventSelector>
                        {
                            new SpawnUnits
                            {
                                TypeID = "X_WING",
                                Count = 3,
                                OwnerFactionInstanceID = "FNALL1",
                            },
                            new SpawnUnits
                            {
                                TypeID = "ALLIANCE_REGIMENT",
                                Count = 2,
                                OwnerFactionInstanceID = "FNALL1",
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restoredEvent = SerializationHelper.Deserialize<GameEvent>(xml);
            PlaceUnitsAction restored = restoredEvent.Actions.OfType<PlaceUnitsAction>().Single();

            StringAssert.Contains("<PlaceUnits DestinationInstanceID=\"NABOO\">", xml);
            StringAssert.Contains("<SpawnUnits", xml);
            StringAssert.Contains("TypeID=\"X_WING\"", xml);
            Assert.AreEqual("NABOO", restored.DestinationInstanceID);
            SpawnUnits[] sources = restored.Units.OfType<SpawnUnits>().ToArray();
            Assert.AreEqual(2, sources.Length);
            Assert.AreEqual("X_WING", sources[0].TypeID);
            Assert.AreEqual(3, sources[0].Count);
            Assert.AreEqual("FNALL1", sources[0].OwnerFactionInstanceID);
            Assert.AreEqual("ALLIANCE_REGIMENT", sources[1].TypeID);
            Assert.AreEqual(2, sources[1].Count);
            Assert.AreEqual("FNALL1", sources[1].OwnerFactionInstanceID);
        }

        [Test]
        public void PlaceUnits_AuthoredSpawnSources_DeserializesStructure()
        {
            const string xml =
                @"
                <PlaceUnits DestinationInstanceID=""NABOO"">
                  <Units>
                    <SpawnUnits TypeID=""X_WING"" Count=""3"" OwnerFactionInstanceID=""FNALL1""/>
                    <SpawnUnits TypeID=""ALLIANCE_REGIMENT"" Count=""2"" OwnerFactionInstanceID=""FNALL1""/>
                  </Units>
                </PlaceUnits>";

            PlaceUnitsAction action = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("NABOO", action.DestinationInstanceID);
            SpawnUnits[] sources = action.Units.OfType<SpawnUnits>().ToArray();
            Assert.AreEqual(2, sources.Length);
            Assert.AreEqual("X_WING", sources[0].TypeID);
            Assert.AreEqual(3, sources[0].Count);
            Assert.AreEqual("FNALL1", sources[0].OwnerFactionInstanceID);
            Assert.AreEqual("ALLIANCE_REGIMENT", sources[1].TypeID);
            Assert.AreEqual(2, sources[1].Count);
            Assert.AreEqual("FNALL1", sources[1].OwnerFactionInstanceID);
        }

        [Test]
        public void PlaceUnits_AuthoredSelectors_DeserializesStructure()
        {
            const string xml =
                @"
                <PlaceUnits>
                  <Units>
                    <SelectBinding Binding=""participants""/>
                  </Units>
                  <Destination>
                    <SelectFirst>
                      <From>
                        <SelectPreviousLocation UnitInstanceID=""LUKE_SKYWALKER""/>
                        <SelectPlanets InstanceID=""YAVIN""/>
                      </From>
                    </SelectFirst>
                  </Destination>
                </PlaceUnits>";

            PlaceUnitsAction action = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("participants", action.Units.OfType<SelectBinding>().Single().Binding);
            SelectFirst destination = action.Destination.OfType<SelectFirst>().Single();
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                destination.Selectors.OfType<SelectPreviousLocation>().Single().UnitInstanceID
            );
            Assert.AreEqual(
                "YAVIN",
                destination.Selectors.OfType<SelectPlanets>().Single().InstanceID
            );
        }

        [Test]
        public void PlaceUnits_Selectors_RoundTripsTransferStructure()
        {
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                Units = new List<GameEventSelector>
                {
                    new SelectBinding { Binding = "participants" },
                },
                Destination = new List<GameEventSelector>
                {
                    new SelectFirst
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPreviousLocation { UnitInstanceID = "LUKE_SKYWALKER" },
                            new SelectPlanets { InstanceID = "YAVIN" },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            PlaceUnitsAction restored = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual(
                "participants",
                restored.Units.OfType<SelectBinding>().Single().Binding
            );
            SelectFirst destination = restored.Destination.OfType<SelectFirst>().Single();
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                destination.Selectors.OfType<SelectPreviousLocation>().Single().UnitInstanceID
            );
            Assert.AreEqual(
                "YAVIN",
                destination.Selectors.OfType<SelectPlanets>().Single().InstanceID
            );
        }

        [Test]
        public void ChangeOwner_PlanetSelectors_RoundTripsAuthoredStructure()
        {
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "FNALL1",
                Planets = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = "NABOO" },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            ChangeOwnerAction restored = (ChangeOwnerAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("FNALL1", restored.FactionInstanceID);
            Assert.AreEqual("NABOO", restored.Planets.OfType<SelectPlanets>().Single().InstanceID);
            Assert.IsEmpty(restored.Units);
        }

        [Test]
        public void SetNodeState_Attributes_DeserializeState()
        {
            SetNodeStateAction action = (SetNodeStateAction)
                SerializationHelper.Deserialize<GameAction>(
                    "<SetNodeState InstanceID=\"LUKE_SKYWALKER\" State=\"Inactive\"/>"
                );

            Assert.AreEqual("LUKE_SKYWALKER", action.InstanceID);
            Assert.AreEqual(SceneNodeState.Inactive, action.State);
        }

        [Test]
        public void SetNodeState_InactiveOfficerSelector_RoundTripsSelector()
        {
            SetNodeStateAction action = new SetNodeStateAction
            {
                State = SceneNodeState.Active,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetBinding = "destination",
                        IsCaptured = true,
                        IncludeInactive = true,
                    },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            SetNodeStateAction restored = (SetNodeStateAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            SelectOfficers selector = restored.Selectors.OfType<SelectOfficers>().Single();
            Assert.AreEqual(SceneNodeState.Active, restored.State);
            Assert.AreEqual("destination", selector.PlanetBinding);
            Assert.AreEqual(true, selector.IsCaptured);
            Assert.IsTrue(selector.IncludeInactive);
        }

        [Test]
        public void RevealToFaction_Targets_DeserializeSelectors()
        {
            RevealToFactionAction action = (RevealToFactionAction)
                SerializationHelper.Deserialize<GameAction>(
                    "<RevealToFaction FactionInstanceID=\"FNALL1\"><Targets><SelectPlanets InstanceID=\"NABOO\"/></Targets></RevealToFaction>"
                );

            Assert.AreEqual("FNALL1", action.FactionInstanceID);
            Assert.AreEqual("NABOO", action.Targets.OfType<SelectPlanets>().Single().InstanceID);
        }
    }
}
