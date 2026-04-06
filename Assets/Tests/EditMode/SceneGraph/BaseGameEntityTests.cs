using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.SceneGraph
{
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
        public void GetInstanceID_EntityWithInstanceID_ReturnsExpectedInstanceID()
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
        public void GetTypeID_EntityWithTypeID_ReturnsExpectedTypeID()
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
        public void GetDisplayName_EntityWithName_ReturnsExpectedName()
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
        public void DisplayImagePath_SetValue_ReturnsCorrectValue()
        {
            BaseGameEntity entity = new BaseGameEntity();
            string expectedImagePath = "Assets/Images/test_image.png";

            entity.DisplayImagePath = expectedImagePath;

            Assert.AreEqual(
                expectedImagePath,
                entity.DisplayImagePath,
                "DisplayImagePath should return the value that was set."
            );
        }

        [Test]
        public void DisplayImagePath_WhenNotSet_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            string imagePath = entity.DisplayImagePath;

            Assert.IsNull(imagePath, "DisplayImagePath should be null when not explicitly set.");
        }

        [Test]
        public void DisplayImagePath_SetToNull_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.DisplayImagePath = null;

            Assert.IsNull(
                entity.DisplayImagePath,
                "DisplayImagePath should return null when set to null."
            );
        }

        [Test]
        public void DisplayImagePath_SetToEmptyString_ReturnsEmptyString()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.DisplayImagePath = "";

            Assert.AreEqual(
                "",
                entity.DisplayImagePath,
                "DisplayImagePath should return empty string when set to empty string."
            );
        }

        [Test]
        public void GetDisplayImagePath_EntityWithImagePath_ReturnsExpectedPath()
        {
            BaseGameEntity entity = new BaseGameEntity();
            string expectedImagePath = "Assets/Images/test_image.png";

            entity.DisplayImagePath = expectedImagePath;

            string imagePath = entity.GetDisplayImagePath();

            Assert.AreEqual(
                expectedImagePath,
                imagePath,
                "GetDisplayImagePath should return the same value as DisplayImagePath."
            );
        }

        [Test]
        public void GetDisplayImagePath_WhenNotSet_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            string imagePath = entity.GetDisplayImagePath();

            Assert.IsNull(
                imagePath,
                "GetDisplayImagePath should return null when DisplayImagePath is not set."
            );
        }

        [Test]
        public void TypeID_WhenNotSet_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            string typeId = entity.TypeID;

            Assert.IsNull(typeId, "TypeID should be null when not explicitly set.");
        }

        [Test]
        public void TypeID_SetToNull_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.TypeID = null;

            Assert.IsNull(entity.TypeID, "TypeID should return null when set to null.");
        }

        [Test]
        public void TypeID_SetToEmptyString_ReturnsEmptyString()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.TypeID = "";

            Assert.AreEqual(
                "",
                entity.TypeID,
                "TypeID should return empty string when set to empty string."
            );
        }

        [Test]
        public void DisplayName_WhenNotSet_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            string displayName = entity.DisplayName;

            Assert.IsNull(displayName, "DisplayName should be null when not explicitly set.");
        }

        [Test]
        public void DisplayName_SetToNull_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.DisplayName = null;

            Assert.IsNull(entity.DisplayName, "DisplayName should return null when set to null.");
        }

        [Test]
        public void DisplayName_SetToEmptyString_ReturnsEmptyString()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.DisplayName = "";

            Assert.AreEqual(
                "",
                entity.DisplayName,
                "DisplayName should return empty string when set to empty string."
            );
        }

        [Test]
        public void Description_WhenNotSet_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            string description = entity.Description;

            Assert.IsNull(description, "Description should be null when not explicitly set.");
        }

        [Test]
        public void Description_SetToNull_ReturnsNull()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.Description = null;

            Assert.IsNull(entity.Description, "Description should return null when set to null.");
        }

        [Test]
        public void Description_SetToEmptyString_ReturnsEmptyString()
        {
            BaseGameEntity entity = new BaseGameEntity();

            entity.Description = "";

            Assert.AreEqual(
                "",
                entity.Description,
                "Description should return empty string when set to empty string."
            );
        }

        [Test]
        public void SerializeAndDeserialize_PopulatedEntity_MaintainsPropertyValues()
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
            Assert.AreEqual(
                originalEntity.TypeID,
                deserializedEntity.TypeID,
                "TypeID should match."
            );
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
} // namespace Rebellion.Tests.SceneGraph
