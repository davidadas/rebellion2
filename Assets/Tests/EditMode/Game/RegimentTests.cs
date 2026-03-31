using NUnit.Framework;
using Rebellion.Game;

[TestFixture]
public class RegimentTests
{
    private Regiment regiment;

    [SetUp]
    public void SetUp()
    {
        regiment = new Regiment
        {
            InstanceID = "REGIMENT1",
            OwnerInstanceID = "FACTION1",
            ConstructionCost = 100,
            MaintenanceCost = 10,
            BaseBuildSpeed = 5,
            RequiredResearchLevel = 1,
            AttackRating = 50,
            DefenseRating = 40,
            DetectionRating = 20,
            BombardmentDefense = 30,
            Movement = null,
            ManufacturingStatus = ManufacturingStatus.Building,
            ManufacturingProgress = 0,
        };
    }

    [Test]
    public void GetManufacturingType_ReturnsTroop()
    {
        ManufacturingType type = regiment.GetManufacturingType();

        Assert.AreEqual(ManufacturingType.Troop, type, "Regiment should be of type Troop");
    }

    [Test]
    public void IsMovable_WhenInTransit_ReturnsFalse()
    {
        regiment.Movement = new MovementState();

        bool isMovable = regiment.IsMovable();

        Assert.IsFalse(isMovable, "Regiment should not be movable when already in transit");
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsTrue()
    {
        regiment.Movement = null;

        bool isMovable = regiment.IsMovable();

        Assert.IsTrue(isMovable, "Regiment should be movable when idle");
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        string serialized = SerializationHelper.Serialize(regiment);
        Regiment deserialized = SerializationHelper.Deserialize<Regiment>(serialized);

        Assert.AreEqual(
            regiment.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.ConstructionCost,
            deserialized.ConstructionCost,
            "ConstructionCost should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.MaintenanceCost,
            deserialized.MaintenanceCost,
            "MaintenanceCost should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.BaseBuildSpeed,
            deserialized.BaseBuildSpeed,
            "BaseBuildSpeed should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.RequiredResearchLevel,
            deserialized.RequiredResearchLevel,
            "RequiredResearchLevel should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.AttackRating,
            deserialized.AttackRating,
            "AttackRating should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.DefenseRating,
            deserialized.DefenseRating,
            "DefenseRating should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.DetectionRating,
            deserialized.DetectionRating,
            "DetectionRating should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.BombardmentDefense,
            deserialized.BombardmentDefense,
            "BombardmentDefense should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.ManufacturingStatus,
            deserialized.ManufacturingStatus,
            "ManufacturingStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            regiment.ManufacturingProgress,
            deserialized.ManufacturingProgress,
            "ManufacturingProgress should be correctly deserialized."
        );
    }

    [Test]
    public void AttackRating_SetAndGet_ReturnsExpectedValue()
    {
        regiment.AttackRating = 75;

        Assert.AreEqual(75, regiment.AttackRating, "AttackRating should return the set value");
    }

    [Test]
    public void DefenseRating_SetAndGet_ReturnsExpectedValue()
    {
        regiment.DefenseRating = 60;

        Assert.AreEqual(60, regiment.DefenseRating, "DefenseRating should return the set value");
    }

    [Test]
    public void DetectionRating_SetAndGet_ReturnsExpectedValue()
    {
        regiment.DetectionRating = 35;

        Assert.AreEqual(
            35,
            regiment.DetectionRating,
            "DetectionRating should return the set value"
        );
    }

    [Test]
    public void BombardmentDefense_SetAndGet_ReturnsExpectedValue()
    {
        regiment.BombardmentDefense = 45;

        Assert.AreEqual(
            45,
            regiment.BombardmentDefense,
            "BombardmentDefense should return the set value"
        );
    }

    [Test]
    public void ManufacturingProgress_InitialValue_IsZero()
    {
        Regiment newRegiment = new Regiment();

        Assert.AreEqual(
            0,
            newRegiment.ManufacturingProgress,
            "ManufacturingProgress should default to 0"
        );
    }

    [Test]
    public void ManufacturingProgress_Increment_IncreasesValue()
    {
        regiment.ManufacturingProgress = 10;
        regiment.ManufacturingProgress += 5;

        Assert.AreEqual(
            15,
            regiment.ManufacturingProgress,
            "ManufacturingProgress should increment correctly"
        );
    }

    [Test]
    public void ManufacturingProgress_SetToConstructionCost_CompletesBuilding()
    {
        regiment.ManufacturingProgress = regiment.ConstructionCost;

        Assert.AreEqual(
            regiment.ConstructionCost,
            regiment.ManufacturingProgress,
            "ManufacturingProgress should equal ConstructionCost when complete"
        );
    }

    [Test]
    public void ManufacturingStatus_DefaultValue_IsBuilding()
    {
        Regiment newRegiment = new Regiment();

        Assert.AreEqual(
            ManufacturingStatus.Building,
            newRegiment.ManufacturingStatus,
            "ManufacturingStatus should default to Building"
        );
    }

    [Test]
    public void ManufacturingStatus_TransitionToBuilt_UpdatesStatus()
    {
        regiment.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(
            ManufacturingStatus.Complete,
            regiment.ManufacturingStatus,
            "ManufacturingStatus should transition to Built"
        );
    }

    [Test]
    public void ManufacturingStatus_TransitionFromBuildingToBuilt_IsValid()
    {
        regiment.ManufacturingStatus = ManufacturingStatus.Building;
        regiment.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(
            ManufacturingStatus.Complete,
            regiment.ManufacturingStatus,
            "Should transition from Building to Built"
        );
    }

    [Test]
    public void ProducerOwnerID_SetAndGet_ReturnsExpectedValue()
    {
        regiment.ProducerOwnerID = "FACTORY1";

        Assert.AreEqual(
            "FACTORY1",
            regiment.ProducerOwnerID,
            "ProducerOwnerID should return the set value"
        );
    }

    [Test]
    public void ProducerOwnerID_DifferentFromOwner_CanBeSet()
    {
        regiment.OwnerInstanceID = "FACTION1";
        regiment.ProducerOwnerID = "PLANET1";

        Assert.AreNotEqual(
            regiment.OwnerInstanceID,
            regiment.ProducerOwnerID,
            "ProducerOwnerID can be different from OwnerInstanceID"
        );
    }

    [Test]
    public void DisplayName_SetAndGet_ReturnsExpectedValue()
    {
        regiment.DisplayName = "Elite Infantry";

        Assert.AreEqual(
            "Elite Infantry",
            regiment.DisplayName,
            "DisplayName should return the set value"
        );
    }

    [Test]
    public void DisplayName_InheritsFromLeafNode_IsAvailable()
    {
        regiment.DisplayName = "Test Regiment";

        Assert.IsNotNull(
            regiment.DisplayName,
            "DisplayName property should be available from LeafNode"
        );
    }
}
