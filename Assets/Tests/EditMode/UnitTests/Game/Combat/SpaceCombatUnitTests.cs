using System;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class SpaceCombatUnitTests
    {
        [Test]
        public void Create_CapitalShip_CreatesIndependentBattleCopy()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 100,
            };
            source.GetComponents().Add(new Engine { Health = 50 });

            SpaceCombatUnit combatUnit = SpaceCombatUnit.Create(source);
            SpaceCombatUnit copy = combatUnit;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
            Assert.AreNotSame(source, copy);
            Assert.AreNotSame(source.GetComponents(), copy.GetComponents());
            Assert.AreNotSame(source.GetComponents()[0], copy.GetComponents()[0]);

            combatUnit.Condition.CurrentHullStrength = 25;
            copy.GetComponents()[0].Health = 10;

            Assert.AreEqual(100, source.CurrentHullStrength);
            Assert.AreEqual(50, source.GetComponents()[0].Health);
        }

        [Test]
        public void Create_CapitalShipWithOfficer_CopiesOfficerWithoutOtherCargo()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            Officer officer = new Officer
            {
                InstanceID = "OFFICER_1",
                OwnerInstanceID = "FACTION_1",
                SmallDisplayImagePath = "Pack/Officer/small-display",
            };
            Starfighter fighter = new Starfighter { OwnerInstanceID = "FACTION_1" };
            source.AddChild(officer);
            officer.SetParent(source);
            source.AddChild(fighter);
            fighter.SetParent(source);

            SpaceCombatUnit copy = SpaceCombatUnit.Create(source);

            Officer copiedOfficer = copy.Officers.Single();
            Assert.AreNotSame(officer, copiedOfficer);
            Assert.AreEqual(officer.InstanceID, copiedOfficer.InstanceID);
            Assert.AreEqual(officer.SmallDisplayImagePath, copiedOfficer.SmallDisplayImagePath);
            Assert.AreSame(source, officer.GetParent());
            Assert.AreEqual(1, copy.Officers.Count);
        }

        /// <summary>
        /// Checks a copied squadron retains its strength independently of the strategic source.
        /// </summary>
        [Test]
        public void Create_Starfighter_CreatesIndependentBattleCopy()
        {
            Starfighter source = new Starfighter
            {
                InstanceID = "STARFIGHTER_SQUADRON_1",
                OwnerInstanceID = "FACTION_1",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
            };

            SpaceCombatUnit combatUnit = SpaceCombatUnit.Create(source);
            SpaceCombatUnit copy = combatUnit;

            Assert.IsNotNull(copy);
            Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(12, copy.MaxSquadronSize);
            Assert.AreEqual(12, combatUnit.Condition.CurrentSquadronSize);

            combatUnit.Condition.CurrentSquadronSize = 0;

            Assert.AreEqual(12, source.CurrentSquadronSize);
        }

        [Test]
        public void Create_CapitalShip_StartsShieldsAtConfiguredMaximum()
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new CapitalShip { MaxShieldStrength = 200 }
            );

            Assert.AreEqual(200, combatant.Condition.CurrentShieldStrength);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Create_Starfighter_StartsShieldsAtConfiguredStrength(int strength)
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new Starfighter { ShieldStrength = strength }
            );

            Assert.AreEqual(strength, combatant.Condition.CurrentShieldStrength);
        }

        [Test]
        public void Create_DamagedCapitalShip_PreservesHullCondition()
        {
            CapitalShip source = new CapitalShip
            {
                MaxHullStrength = 100,
                CurrentHullStrength = 35,
            };

            SpaceCombatUnit copy = SpaceCombatUnit.Create(source);

            Assert.AreEqual(100, copy.MaxHullStrength);
            Assert.AreEqual(35, copy.Condition.CurrentHullStrength);
        }

        [Test]
        public void Create_ConfiguredFighter_CopiesIndependentPerFighterHull()
        {
            Starfighter source = new Starfighter { MaxHullStrength = 20, CurrentHullStrength = 12 };

            SpaceCombatUnit copy = SpaceCombatUnit.Create(source);
            copy.Condition.CurrentHullStrength = 3;

            Assert.AreEqual(20, copy.MaxHullStrength);
            Assert.AreEqual(12, source.CurrentHullStrength);
        }

        [Test]
        public void Create_UnconfiguredFighter_DoesNotInventHullStrength()
        {
            SpaceCombatUnit copy = SpaceCombatUnit.Create(new Starfighter());

            Assert.AreEqual(0, copy.MaxHullStrength);
            Assert.AreEqual(0, copy.Condition.CurrentHullStrength);
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(37)]
        public void Serialize_ShieldState_PreservesRecordedValue(int? remainingShields)
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new CapitalShip { MaxShieldStrength = 100 }
            );
            combatant.Condition.CurrentShieldStrength = remainingShields;

            SpaceCombatUnit restored = SerializationHelper.Deserialize<SpaceCombatUnit>(
                SerializationHelper.Serialize(combatant)
            );

            Assert.AreEqual(remainingShields, restored.Condition.CurrentShieldStrength);
        }

        /// <summary>
        /// Checks saving a moving combatant preserves its position and unfinished order.
        /// </summary>
        [Test]
        public void Serialize_MoveDestination_PreservesPendingOrder()
        {
            SpaceCombatUnit unit = SpaceCombatUnit.Create(new CapitalShip { SublightSpeed = 4 });
            unit.Position.X = 5f;
            unit.MoveOrder.Destination = new BattleVector3
            {
                X = 20f,
                Y = 3f,
                Z = 40f,
            };
            unit.MoveOrder.SpeedLimit = 2f;

            SpaceCombatUnit restored = SerializationHelper.Deserialize<SpaceCombatUnit>(
                SerializationHelper.Serialize(unit)
            );

            Assert.AreEqual(5f, restored.Position.X);
            Assert.AreEqual(20f, restored.MoveOrder.Destination.X);
            Assert.AreEqual(3f, restored.MoveOrder.Destination.Y);
            Assert.AreEqual(40f, restored.MoveOrder.Destination.Z);
            Assert.AreEqual(2f, restored.MoveOrder.SpeedLimit);
        }

        [Test]
        public void Serialize_FighterHull_PreservesCurrentAndMaximum()
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new Starfighter { MaxHullStrength = 20, CurrentHullStrength = 12 }
            );

            SpaceCombatUnit restored = SerializationHelper.Deserialize<SpaceCombatUnit>(
                SerializationHelper.Serialize(combatant)
            );
            Assert.AreEqual(20, restored.MaxHullStrength);
            Assert.AreEqual(12, restored.Condition.CurrentHullStrength);
        }

        [Test]
        public void Deserialize_UnrecordedShields_DoesNotRefillBattleShields()
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new CapitalShip { MaxShieldStrength = 100 }
            );
            string xml = SerializationHelper
                .Serialize(combatant)
                .Replace("<CurrentShieldStrength>100</CurrentShieldStrength>", string.Empty);
            StringAssert.DoesNotContain("CurrentShieldStrength", xml);

            SpaceCombatUnit restored = SerializationHelper.Deserialize<SpaceCombatUnit>(xml);

            Assert.IsNull(restored.Condition.CurrentShieldStrength);
        }

        /// <summary>
        /// Checks the battle records carrier identity even though its fighter copy has no parent.
        /// </summary>
        [Test]
        public void Create_CarriedFighter_PreservesAssignmentWithoutStrategicParent()
        {
            CapitalShip carrier = new CapitalShip { InstanceID = "CARRIER" };
            Starfighter fighter = new Starfighter();
            fighter.SetParent(carrier);
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(fighter);
            Assert.That(combatant.Flight.CarrierSourceUnitInstanceID, Is.EqualTo("CARRIER"));
            Assert.That(combatant.Flight.Phase, Is.EqualTo(FighterFlightState.Docked));
            Assert.AreSame(carrier, fighter.ParentNode);
        }

        [Test]
        public void Serialize_StrategicSource_ExcludesProductionAndTravelState()
        {
            CapitalShip source = new CapitalShip
            {
                ProducerPlanetID = "FACTORY",
                ManufacturingProgress = 73,
                MaxHullStrength = 100,
                CurrentHullStrength = 35,
            };
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(source);

            string xml = SerializationHelper.Serialize(combatant);

            StringAssert.DoesNotContain("ProducerPlanetID", xml);
            StringAssert.DoesNotContain("ManufacturingProgress", xml);
            StringAssert.DoesNotContain("<CapitalShip>", xml);
            Assert.AreEqual(35, combatant.Condition.CurrentHullStrength);
        }

        [Test]
        public void Serialize_MeasuredGeometry_RebuildsOutsideSavedState()
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(new CapitalShip());
            combatant.ModelSize = new Rebellion.SceneGraph.ModelDimensions
            {
                Width = 10f,
                Height = 3f,
                Depth = 20f,
            };

            string xml = SerializationHelper.Serialize(combatant);
            SpaceCombatUnit restored = SerializationHelper.Deserialize<SpaceCombatUnit>(xml);

            StringAssert.DoesNotContain("ModelSize", xml);
            Assert.IsNull(restored.ModelSize);
        }

        [Test]
        public void Serialize_ShipCapabilities_StoresFieldsDirectlyOnSpaceUnit()
        {
            SpaceCombatUnit combatant = SpaceCombatUnit.Create(
                new CapitalShip
                {
                    TypeID = "CRUISER",
                    OwnerInstanceID = "OWNER",
                    DisplayName = "Cruiser",
                    DisplayImagePath = "cruiser.png",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 35,
                    SublightSpeed = 4,
                }
            );

            XmlDocument document = new XmlDocument();
            document.LoadXml(SerializationHelper.Serialize(combatant));

            Assert.AreEqual("SpaceCombatUnit", document.DocumentElement.Name);
            Assert.IsNull(document.SelectSingleNode("/SpaceCombatUnit/Data"));
            Assert.AreEqual(
                "CRUISER",
                document.SelectSingleNode("/SpaceCombatUnit/TypeID").InnerText
            );
            Assert.AreEqual(
                "OWNER",
                document.SelectSingleNode("/SpaceCombatUnit/OwnerInstanceID").InnerText
            );
            Assert.AreEqual(
                "Cruiser",
                document.SelectSingleNode("/SpaceCombatUnit/DisplayName").InnerText
            );
            Assert.AreEqual(
                "cruiser.png",
                document.SelectSingleNode("/SpaceCombatUnit/DisplayImagePath").InnerText
            );
            Assert.AreEqual(
                "100",
                document.SelectSingleNode("/SpaceCombatUnit/MaxHullStrength").InnerText
            );
            Assert.AreEqual(
                "4",
                document.SelectSingleNode("/SpaceCombatUnit/SublightSpeed").InnerText
            );
            Assert.AreEqual(
                "35",
                document
                    .SelectSingleNode("/SpaceCombatUnit/Condition/CurrentHullStrength")
                    .InnerText
            );
        }

        [Test]
        public void Create_NullUnit_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SpaceCombatUnit.Create(null));
        }

        [Test]
        public void Create_UnsupportedUnit_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SpaceCombatUnit.Create(new Regiment()));
        }
    }
}
