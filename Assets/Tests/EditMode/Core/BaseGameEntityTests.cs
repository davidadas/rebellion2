using NUnit.Framework;
using ObjectExtensions;

[TestFixture]
public class BaseGameEntityTests
{
    [Test]
    public void InstanceID_WhenNotSet_GeneratesUniqueValue()
    {
        BaseGameEntity entity = new BaseGameEntity();

        string instanceId = entity.InstanceID;

        Assert.IsNotNull(instanceId, "InstanceID should not be null.");
        Assert.AreEqual(32, instanceId.Length, "InstanceID should be a 32-character string.");
    }

    [Test]
    public void InstanceID_WhenSet_ReturnsSetValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string customInstanceId = "customInstanceID123";

        entity.InstanceID = customInstanceId;

        Assert.AreEqual(
            customInstanceId,
            entity.InstanceID,
            "InstanceID should return the explicitly set value."
        );
    }

    [Test]
    public void TypeID_SetValue_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string expectedTypeId = "TestType";

        entity.TypeID = expectedTypeId;

        Assert.AreEqual(
            expectedTypeId,
            entity.TypeID,
            "TypeID should return the value that was set."
        );
    }

    [Test]
    public void DisplayName_SetValue_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string expectedDisplayName = "Test Entity";

        entity.DisplayName = expectedDisplayName;

        Assert.AreEqual(
            expectedDisplayName,
            entity.DisplayName,
            "DisplayName should return the value that was set."
        );
    }

    [Test]
    public void Description_SetValue_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string expectedDescription = "This is a test description.";

        entity.Description = expectedDescription;

        Assert.AreEqual(
            expectedDescription,
            entity.Description,
            "Description should return the value that was set."
        );
    }

    [Test]
    public void GetInstanceID_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();

        string instanceId = entity.GetInstanceID();

        Assert.AreEqual(
            entity.InstanceID,
            instanceId,
            "GetInstanceID should return the same value as InstanceID."
        );
    }

    [Test]
    public void GetTypeID_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string expectedTypeId = "TestType";

        entity.TypeID = expectedTypeId;

        string typeId = entity.GetTypeID();

        Assert.AreEqual(
            expectedTypeId,
            typeId,
            "GetTypeID should return the same value as TypeID."
        );
    }

    [Test]
    public void GetDisplayName_ReturnsCorrectValue()
    {
        BaseGameEntity entity = new BaseGameEntity();
        string expectedDisplayName = "Test Entity";

        entity.DisplayName = expectedDisplayName;

        string displayName = entity.GetDisplayName();

        Assert.AreEqual(
            expectedDisplayName,
            displayName,
            "GetDisplayName should return the same value as DisplayName."
        );
    }

    [Test]
    public void GetDeepCopy_IgnoresInstanceID_WhenDeepCopying()
    {
        // Arrange
        BaseGameEntity originalEntity = new BaseGameEntity
        {
            InstanceID = "originalInstanceID",
            TypeID = "TestType",
            DisplayName = "Test Entity",
            Description = "This is a test description.",
        };

        // Act
        BaseGameEntity clonedEntity = originalEntity.GetDeepCopy();

        // Assert
        Assert.AreNotEqual(
            originalEntity.InstanceID,
            clonedEntity.InstanceID,
            "InstanceID should not be copied during cloning."
        );
        Assert.AreEqual(
            originalEntity.TypeID,
            clonedEntity.TypeID,
            "TypeID should be copied correctly during cloning."
        );
        Assert.AreEqual(
            originalEntity.DisplayName,
            clonedEntity.DisplayName,
            "DisplayName should be copied correctly during cloning."
        );
        Assert.AreEqual(
            originalEntity.Description,
            clonedEntity.Description,
            "Description should be copied correctly during cloning."
        );
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsPropertyValues()
    {
        BaseGameEntity originalEntity = new BaseGameEntity
        {
            InstanceID = "customInstanceID123",
            TypeID = "TestType",
            DisplayName = "Test Entity",
            Description = "This is a test description.",
        };

        string serializedXml = SerializationHelper.Serialize(originalEntity);
        BaseGameEntity deserializedEntity = SerializationHelper.Deserialize<BaseGameEntity>(
            serializedXml
        );

        Assert.IsNotNull(deserializedEntity, "Deserialized entity should not be null.");
        Assert.AreEqual(
            originalEntity.InstanceID,
            deserializedEntity.InstanceID,
            "InstanceID should match."
        );
        Assert.AreEqual(originalEntity.TypeID, deserializedEntity.TypeID, "TypeID should match.");
        Assert.AreEqual(
            originalEntity.DisplayName,
            deserializedEntity.DisplayName,
            "DisplayName should match."
        );
        Assert.AreEqual(
            originalEntity.Description,
            deserializedEntity.Description,
            "Description should match."
        );
    }
}
