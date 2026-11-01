using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorUnitFactoryTests
    {
        [TestCase("Officer", typeof(Officer))]
        [TestCase("SpecialForce", typeof(SpecialForces))]
        [TestCase("Regiment", typeof(Regiment))]
        [TestCase("CapitalShip", typeof(CapitalShip))]
        [TestCase("Starfighter", typeof(Starfighter))]
        [TestCase("Building", typeof(Building))]
        public void Create_UnitCategory_LoadsThroughTheEngineWithNoRuntimeAssignments(
            string kind,
            Type type
        )
        {
            XElement definition = ContentEditorUnitFactory.Create(kind);
            using StringReader text = new StringReader(definition.ToString());
            BaseSceneNode unit = (BaseSceneNode)new GameSerializer(type).Deserialize(text);

            Assert.IsTrue(string.IsNullOrEmpty(unit.DisplayImagePath));
            Assert.IsTrue(string.IsNullOrEmpty(unit.SmallDisplayImagePath));
            Assert.IsTrue(string.IsNullOrEmpty(unit.Description));
            Assert.IsNull(unit.ParentInstanceID);
            Assert.IsNull(unit.OwnerInstanceID);
            Assert.IsNull(((IMovable)unit).Movement);
            Assert.IsNull(definition.Element("ManufacturingStatus"));
            Assert.IsNull(definition.Element("ManufacturingProgress"));
            Assert.IsNull(definition.Element("ProducerPlanetID"));
            if (unit is IManufacturable manufacturable)
                Assert.IsEmpty(manufacturable.ManufacturingFactionInstanceIDs);
            else
                Assert.IsEmpty(((Officer)unit).RecruitingFactionInstanceIDs);
        }

        [Test]
        public void Create_Starfighter_ExposesEmptyCombatStatsAndACompleteSquadron()
        {
            XElement definition = ContentEditorUnitFactory.Create("Starfighter");
            using StringReader text = new StringReader(definition.ToString());
            Starfighter unit = (Starfighter)
                new GameSerializer(typeof(Starfighter)).Deserialize(text);

            Assert.AreEqual(0, unit.LaserCannon);
            Assert.AreEqual(0, unit.IonCannon);
            Assert.AreEqual(0, unit.Torpedoes);
            Assert.Greater(unit.MaxSquadronSize, 0);
            Assert.AreEqual(unit.MaxSquadronSize, unit.CurrentSquadronSize);
            Assert.IsNotNull(definition.Element(nameof(Starfighter.Hyperdrive)));
            Assert.IsNotNull(definition.Element(nameof(Starfighter.ConstructionCost)));
        }

        [Test]
        public void Create_CapitalShip_ProvidesIndependentZeroedWeaponArcsAndNoCargo()
        {
            XElement definition = ContentEditorUnitFactory.Create("CapitalShip");
            using StringReader text = new StringReader(definition.ToString());
            CapitalShip unit = (CapitalShip)
                new GameSerializer(typeof(CapitalShip)).Deserialize(text);

            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(PrimaryWeaponType)),
                unit.PrimaryWeapons.Keys
            );
            foreach (int[] weapon in unit.PrimaryWeapons.Values)
            {
                Assert.AreEqual(CapitalShip.PrimaryWeaponRangeIndex + 1, weapon.Length);
                Assert.IsTrue(weapon.All(value => value == 0));
            }
            Assert.AreEqual(unit.MaxHullStrength, unit.CurrentHullStrength);
            Assert.IsNull(definition.Element("Officers"));
            Assert.IsNull(definition.Element("Starfighters"));
            Assert.IsFalse(unit.HasGravityWell);
            Assert.IsFalse(unit.CanDestroyPlanets);
        }

        [Test]
        public void Create_UnsupportedCategory_RejectsCreation()
        {
            Assert.Throws<ArgumentException>(() => ContentEditorUnitFactory.Create("GameConfig"));
        }
    }
}
