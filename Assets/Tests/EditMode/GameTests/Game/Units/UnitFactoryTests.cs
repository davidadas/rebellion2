using System;
using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class UnitFactoryTests
    {
        [Test]
        public void Create_KnownType_CreatesInitializedIndependentInstance()
        {
            Starfighter template = new Starfighter
            {
                InstanceID = "template-instance",
                TypeID = "X_WING",
                OwnerInstanceID = "template-owner",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 50,
            };
            UnitFactory factory = CreateFactory(template);

            Starfighter unit = factory.Create<Starfighter>("X_WING", "FNALL1");

            Assert.AreNotSame(template, unit);
            Assert.IsNotEmpty(unit.InstanceID);
            Assert.AreNotEqual(template.InstanceID, unit.InstanceID);
            Assert.AreEqual("X_WING", unit.TypeID);
            Assert.AreEqual("FNALL1", unit.OwnerInstanceID);
            Assert.AreEqual(ManufacturingStatus.Complete, unit.ManufacturingStatus);
            Assert.AreEqual(0, unit.ManufacturingProgress);
            Assert.IsNull(unit.Movement);
            Assert.IsNull(unit.GetParent());
        }

        [Test]
        public void Create_UnknownType_ThrowsInvalidOperationException()
        {
            UnitFactory factory = CreateFactory();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                factory.Create<Starfighter>("UNKNOWN", "FNALL1")
            );

            StringAssert.Contains("Unknown unit TypeID 'UNKNOWN'", exception.Message);
        }

        [Test]
        public void Create_WrongCategory_ThrowsInvalidOperationException()
        {
            UnitFactory factory = CreateFactory(new Starfighter { TypeID = "X_WING" });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                factory.Create<Regiment>("X_WING", "FNALL1")
            );

            StringAssert.Contains("Unit TypeID 'X_WING' is not a Regiment", exception.Message);
        }

        private static UnitFactory CreateFactory(params Starfighter[] starfighters) =>
            new UnitFactory(
                Array.Empty<Building>(),
                Array.Empty<CapitalShip>(),
                starfighters,
                Array.Empty<Regiment>(),
                Array.Empty<SpecialForces>()
            );
    }
}
