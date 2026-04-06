using NUnit.Framework;
using Rebellion.Game;

[TestFixture]
public class RegimentTests
{
    private Regiment _regiment;

    [SetUp]
    public void SetUp()
    {
        _regiment = new Regiment
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
        ManufacturingType type = _regiment.GetManufacturingType();

        Assert.AreEqual(ManufacturingType.Troop, type, "Regiment should be of type Troop");
    }

    [Test]
    public void IsMovable_WhenInTransit_ReturnsFalse()
    {
        _regiment.Movement = new MovementState();

        bool isMovable = _regiment.IsMovable();

        Assert.IsFalse(isMovable, "Regiment should not be movable when already in transit");
    }

    [Test]
    public void IsMovable_WhenIdle_ReturnsTrue()
    {
        _regiment.Movement = null;

        bool isMovable = _regiment.IsMovable();

        Assert.IsTrue(isMovable, "Regiment should be movable when idle");
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        string serialized = SerializationHelper.Serialize(_regiment);
        Regiment deserialized = SerializationHelper.Deserialize<Regiment>(serialized);

        Assert.AreEqual(
            _regiment.InstanceID,
            deserialized.InstanceID,
            "InstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.OwnerInstanceID,
            deserialized.OwnerInstanceID,
            "OwnerInstanceID should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.ConstructionCost,
            deserialized.ConstructionCost,
            "ConstructionCost should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.MaintenanceCost,
            deserialized.MaintenanceCost,
            "MaintenanceCost should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.BaseBuildSpeed,
            deserialized.BaseBuildSpeed,
            "BaseBuildSpeed should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.RequiredResearchLevel,
            deserialized.RequiredResearchLevel,
            "RequiredResearchLevel should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.AttackRating,
            deserialized.AttackRating,
            "AttackRating should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.DefenseRating,
            deserialized.DefenseRating,
            "DefenseRating should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.DetectionRating,
            deserialized.DetectionRating,
            "DetectionRating should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.BombardmentDefense,
            deserialized.BombardmentDefense,
            "BombardmentDefense should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.Movement,
            deserialized.Movement,
            "MovementStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.ManufacturingStatus,
            deserialized.ManufacturingStatus,
            "ManufacturingStatus should be correctly deserialized."
        );
        Assert.AreEqual(
            _regiment.ManufacturingProgress,
            deserialized.ManufacturingProgress,
            "ManufacturingProgress should be correctly deserialized."
        );
    }

    [Test]
    public void AttackRating_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.AttackRating = 75;

        Assert.AreEqual(75, _regiment.AttackRating, "AttackRating should return the set value");
    }

    [Test]
    public void DefenseRating_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.DefenseRating = 60;

        Assert.AreEqual(60, _regiment.DefenseRating, "DefenseRating should return the set value");
    }

    [Test]
    public void DetectionRating_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.DetectionRating = 35;

        Assert.AreEqual(
            35,
            _regiment.DetectionRating,
            "DetectionRating should return the set value"
        );
    }

    [Test]
    public void BombardmentDefense_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.BombardmentDefense = 45;

        Assert.AreEqual(
            45,
            _regiment.BombardmentDefense,
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
        _regiment.ManufacturingProgress = 10;
        _regiment.ManufacturingProgress += 5;

        Assert.AreEqual(
            15,
            _regiment.ManufacturingProgress,
            "ManufacturingProgress should increment correctly"
        );
    }

    [Test]
    public void ManufacturingProgress_SetToConstructionCost_CompletesBuilding()
    {
        _regiment.ManufacturingProgress = _regiment.ConstructionCost;

        Assert.AreEqual(
            _regiment.ConstructionCost,
            _regiment.ManufacturingProgress,
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
        _regiment.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(
            ManufacturingStatus.Complete,
            _regiment.ManufacturingStatus,
            "ManufacturingStatus should transition to Built"
        );
    }

    [Test]
    public void ManufacturingStatus_TransitionFromBuildingToBuilt_IsValid()
    {
        _regiment.ManufacturingStatus = ManufacturingStatus.Building;
        _regiment.ManufacturingStatus = ManufacturingStatus.Complete;

        Assert.AreEqual(
            ManufacturingStatus.Complete,
            _regiment.ManufacturingStatus,
            "Should transition from Building to Built"
        );
    }

    [Test]
    public void ProducerOwnerID_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.ProducerOwnerID = "FACTORY1";

        Assert.AreEqual(
            "FACTORY1",
            _regiment.ProducerOwnerID,
            "ProducerOwnerID should return the set value"
        );
    }

    [Test]
    public void ProducerOwnerID_DifferentFromOwner_CanBeSet()
    {
        _regiment.OwnerInstanceID = "FACTION1";
        _regiment.ProducerOwnerID = "PLANET1";

        Assert.AreNotEqual(
            _regiment.OwnerInstanceID,
            _regiment.ProducerOwnerID,
            "ProducerOwnerID can be different from OwnerInstanceID"
        );
    }

    [Test]
    public void DisplayName_SetAndGet_ReturnsExpectedValue()
    {
        _regiment.DisplayName = "Elite Infantry";

        Assert.AreEqual(
            "Elite Infantry",
            _regiment.DisplayName,
            "DisplayName should return the set value"
        );
    }

    [Test]
    public void DisplayName_InheritsFromLeafNode_IsAvailable()
    {
        _regiment.DisplayName = "Test Regiment";

        Assert.IsNotNull(
            _regiment.DisplayName,
            "DisplayName property should be available from LeafNode"
        );
    }
}
