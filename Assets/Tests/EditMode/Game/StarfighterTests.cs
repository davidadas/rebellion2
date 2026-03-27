using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Extensions;

[TestFixture]
public class StarfighterTests
{
    private Starfighter starfighter;

    [SetUp]
    public void SetUp()
    {
        starfighter = new Starfighter
        {
            InstanceID = "STARFIGHTER1",
            OwnerInstanceID = "FACTION1",
            ConstructionCost = 200,
            MaintenanceCost = 15,
            BaseBuildSpeed = 8,
            RequiredResearchLevel = 2,
            SquadronSize = 12,
            DetectionRating = 50,
            Bombardment = 30,
            ShieldStrength = 40,
            Hyperdrive = 3,
            SublightSpeed = 80,
            Agility = 70,
            LaserCannon = 20,
            IonCannon = 15,
            Torpedoes = 10,
            LaserRange = 5,
            IonRange = 4,
            TorpedoRange = 6,
            Movement = null,
            ManufacturingStatus = ManufacturingStatus.Building,
            ManufacturingProgress = 0,
            ProducerOwnerID = "PRODUCER1",
        };
    }

    [Test]
    public void GetManufacturingType_ReturnsShip()
    {
        ManufacturingType type = starfighter.GetManufacturingType();

        Assert.AreEqual(ManufacturingType.Ship, type, "Starfighter should be of type Ship");
    }

    [Test]
    public void IsMovable_WhenInTransit_ReturnsTrue()
    {
        starfighter.Movement = new MovementState();

        bool isMovable = starfighter.IsMovable();

        Assert.IsTrue(isMovable, "Starfighter should be movable when in transit");
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsFalse()
    {
        starfighter.Movement = null;

        bool isMovable = starfighter.IsMovable();

        Assert.IsFalse(isMovable, "Starfighter should not be movable when idle");
    }

    // Weapon System Tests
    [Test]
    public void LaserCannon_SetAndGet_StoresCorrectValue()
    {
        starfighter.LaserCannon = 25;

        Assert.AreEqual(
            25,
            starfighter.LaserCannon,
            "LaserCannon should store and return the correct value"
        );
    }

    [Test]
    public void IonCannon_SetAndGet_StoresCorrectValue()
    {
        starfighter.IonCannon = 18;

        Assert.AreEqual(
            18,
            starfighter.IonCannon,
            "IonCannon should store and return the correct value"
        );
    }

    [Test]
    public void Torpedoes_SetAndGet_StoresCorrectValue()
    {
        starfighter.Torpedoes = 12;

        Assert.AreEqual(
            12,
            starfighter.Torpedoes,
            "Torpedoes should store and return the correct value"
        );
    }

    [Test]
    public void LaserRange_SetAndGet_StoresCorrectValue()
    {
        starfighter.LaserRange = 7;

        Assert.AreEqual(
            7,
            starfighter.LaserRange,
            "LaserRange should store and return the correct value"
        );
    }

    [Test]
    public void IonRange_SetAndGet_StoresCorrectValue()
    {
        starfighter.IonRange = 6;

        Assert.AreEqual(
            6,
            starfighter.IonRange,
            "IonRange should store and return the correct value"
        );
    }

    [Test]
    public void TorpedoRange_SetAndGet_StoresCorrectValue()
    {
        starfighter.TorpedoRange = 8;

        Assert.AreEqual(
            8,
            starfighter.TorpedoRange,
            "TorpedoRange should store and return the correct value"
        );
    }

    // Squadron Size Tests
    [Test]
    public void SquadronSize_SetAndGet_StoresCorrectValue()
    {
        starfighter.SquadronSize = 24;

        Assert.AreEqual(
            24,
            starfighter.SquadronSize,
            "SquadronSize should store and return the correct value"
        );
    }

    [Test]
    public void SquadronSize_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.SquadronSize, "SquadronSize should default to 0");
    }

    // Detection and Bombardment Tests
    [Test]
    public void DetectionRating_SetAndGet_StoresCorrectValue()
    {
        starfighter.DetectionRating = 75;

        Assert.AreEqual(
            75,
            starfighter.DetectionRating,
            "DetectionRating should store and return the correct value"
        );
    }

    [Test]
    public void DetectionRating_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.DetectionRating, "DetectionRating should default to 0");
    }

    [Test]
    public void Bombardment_SetAndGet_StoresCorrectValue()
    {
        starfighter.Bombardment = 45;

        Assert.AreEqual(
            45,
            starfighter.Bombardment,
            "Bombardment should store and return the correct value"
        );
    }

    [Test]
    public void Bombardment_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.Bombardment, "Bombardment should default to 0");
    }

    // Maneuverability Tests
    [Test]
    public void Hyperdrive_SetAndGet_StoresCorrectValue()
    {
        starfighter.Hyperdrive = 5;

        Assert.AreEqual(
            5,
            starfighter.Hyperdrive,
            "Hyperdrive should store and return the correct value"
        );
    }

    [Test]
    public void Hyperdrive_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.Hyperdrive, "Hyperdrive should default to 0");
    }

    [Test]
    public void SublightSpeed_SetAndGet_StoresCorrectValue()
    {
        starfighter.SublightSpeed = 95;

        Assert.AreEqual(
            95,
            starfighter.SublightSpeed,
            "SublightSpeed should store and return the correct value"
        );
    }

    [Test]
    public void SublightSpeed_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.SublightSpeed, "SublightSpeed should default to 0");
    }

    [Test]
    public void Agility_SetAndGet_StoresCorrectValue()
    {
        starfighter.Agility = 85;

        Assert.AreEqual(
            85,
            starfighter.Agility,
            "Agility should store and return the correct value"
        );
    }

    [Test]
    public void Agility_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.Agility, "Agility should default to 0");
    }

    // Shield Strength Tests
    [Test]
    public void ShieldStrength_SetAndGet_StoresCorrectValue()
    {
        starfighter.ShieldStrength = 60;

        Assert.AreEqual(
            60,
            starfighter.ShieldStrength,
            "ShieldStrength should store and return the correct value"
        );
    }

    [Test]
    public void ShieldStrength_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.ShieldStrength, "ShieldStrength should default to 0");
    }

    // Manufacturing Progress Tests
    [Test]
    public void ManufacturingProgress_SetAndGet_StoresCorrectValue()
    {
        starfighter.ManufacturingProgress = 150;

        Assert.AreEqual(
            150,
            starfighter.ManufacturingProgress,
            "ManufacturingProgress should store and return the correct value"
        );
    }

    [Test]
    public void ManufacturingProgress_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(
            0,
            newStarfighter.ManufacturingProgress,
            "ManufacturingProgress should default to 0"
        );
    }

    [Test]
    public void ManufacturingProgress_Increment_UpdatesCorrectly()
    {
        starfighter.ManufacturingProgress = 50;
        starfighter.ManufacturingProgress += 25;

        Assert.AreEqual(
            75,
            starfighter.ManufacturingProgress,
            "ManufacturingProgress should increment correctly"
        );
    }

    [Test]
    public void ManufacturingStatus_SetToCompleted_StoresCorrectValue()
    {
        starfighter.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(
            ManufacturingStatus.Complete,
            starfighter.ManufacturingStatus,
            "ManufacturingStatus should be set to Completed"
        );
    }

    [Test]
    public void ManufacturingStatus_DefaultValue_IsBuilding()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(
            ManufacturingStatus.Building,
            newStarfighter.ManufacturingStatus,
            "ManufacturingStatus should default to Building"
        );
    }

    // Position Tests
    [Test]
    public void PositionX_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.GetPosition().X, "PositionX should default to 0");
    }

    [Test]
    public void PositionY_DefaultValue_IsZero()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.AreEqual(0, newStarfighter.GetPosition().Y, "PositionY should default to 0");
    }

    // ProducerOwnerID Tests
    [Test]
    public void ProducerOwnerID_SetAndGet_StoresCorrectValue()
    {
        starfighter.ProducerOwnerID = "PRODUCER123";

        Assert.AreEqual(
            "PRODUCER123",
            starfighter.ProducerOwnerID,
            "ProducerOwnerID should store and return the correct value"
        );
    }

    [Test]
    public void ProducerOwnerID_DefaultValue_IsNull()
    {
        Starfighter newStarfighter = new Starfighter();

        Assert.IsNull(newStarfighter.ProducerOwnerID, "ProducerOwnerID should default to null");
    }

    [Test]
    public void ProducerOwnerID_SetToNull_StoresNull()
    {
        starfighter.ProducerOwnerID = "PRODUCER123";
        starfighter.ProducerOwnerID = null;

        Assert.IsNull(
            starfighter.ProducerOwnerID,
            "ProducerOwnerID should be able to be set to null"
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        string serialized = SerializationHelper.Serialize(starfighter);
        Starfighter deserialized = SerializationHelper.Deserialize<Starfighter>(serialized);

        Assert.AreEqual(
            starfighter.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.ConstructionCost,
            deserialized.ConstructionCost,
            "ConstructionCost should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.MaintenanceCost,
            deserialized.MaintenanceCost,
            "MaintenanceCost should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.BaseBuildSpeed,
            deserialized.BaseBuildSpeed,
            "BaseBuildSpeed should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.RequiredResearchLevel,
            deserialized.RequiredResearchLevel,
            "RequiredResearchLevel should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.SquadronSize,
            deserialized.SquadronSize,
            "SquadronSize should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.DetectionRating,
            deserialized.DetectionRating,
            "DetectionRating should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.Bombardment,
            deserialized.Bombardment,
            "Bombardment should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.ShieldStrength,
            deserialized.ShieldStrength,
            "ShieldStrength should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.Hyperdrive,
            deserialized.Hyperdrive,
            "Hyperdrive should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.SublightSpeed,
            deserialized.SublightSpeed,
            "SublightSpeed should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.Agility,
            deserialized.Agility,
            "Agility should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.LaserCannon,
            deserialized.LaserCannon,
            "LaserCannon should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.IonCannon,
            deserialized.IonCannon,
            "IonCannon should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.Torpedoes,
            deserialized.Torpedoes,
            "Torpedoes should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.LaserRange,
            deserialized.LaserRange,
            "LaserRange should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.IonRange,
            deserialized.IonRange,
            "IonRange should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.TorpedoRange,
            deserialized.TorpedoRange,
            "TorpedoRange should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.ManufacturingStatus,
            deserialized.ManufacturingStatus,
            "ManufacturingStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.ManufacturingProgress,
            deserialized.ManufacturingProgress,
            "ManufacturingProgress should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.ProducerOwnerID,
            deserialized.ProducerOwnerID,
            "ProducerOwnerID should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.GetPosition().X,
            deserialized.GetPosition().X,
            "PositionX should be correctly deserialized."
        );
        Assert.AreEqual(
            starfighter.GetPosition().Y,
            deserialized.GetPosition().Y,
            "PositionY should be correctly deserialized."
        );
    }
}
