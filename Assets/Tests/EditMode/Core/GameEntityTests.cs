using NUnit.Framework;
using System;
using System.Collections.Generic;

[TestFixture]
public class GameEntityTests
{
    [Test]
    public void TestInstanceIDCreated()
    {
        GameEntity entity = new GameEntity();
        Assert.IsNotNull(entity.InstanceID, "InstanceID should not be null.");

        // Ensure the InstanceID is a valid GUID.
        Assert.IsTrue(Guid.TryParse(entity.InstanceID, out _), "InstanceID should be a valid GUID.");
    }

    [Test]
    public void TestTypeID()
    {
        GameEntity entity = new GameEntity();
        string typeId = "TestTypeID";
        entity.TypeID = typeId;

        // Ensure the TypeID is set correctly.
        Assert.AreEqual(typeId, entity.TypeID, "TypeID should be set correctly.");
    }

    [Test]
    public void TestDisplayName()
    {
        GameEntity entity = new GameEntity();
        string displayName = "TestDisplayName";
        entity.DisplayName = displayName;

        // Ensure the DisplayName is set correctly.
        Assert.AreEqual(displayName, entity.DisplayName, "DisplayName should be set correctly.");
    }

    [Test]
    public void TestDescription()
    {
        GameEntity entity = new GameEntity();
        string description = "TestDescription";
        entity.Description = description;

        // Ensure the Description is set correctly.
        Assert.AreEqual(description, entity.Description, "Description should be set correctly.");
    }

    [Test]
    public void TestOwnerTypeID()
    {
        GameEntity entity = new GameEntity();
        string ownerTypeID = "TestOwnerTypeID";
        entity.AllowedOwnerTypeIDs = new List<string> { ownerTypeID };
        entity.OwnerTypeID = ownerTypeID;

        // Ensure the OwnerTypeID is set correctly.
        Assert.AreEqual(ownerTypeID, entity.OwnerTypeID, "OwnerTypeID should be set correctly.");
    }

    [Test]
    public void TestInvalidOwnerTypeID()
    {
        GameEntity entity = new GameEntity();
        entity.AllowedOwnerTypeIDs = new List<string> { "ValidOwnerTypeID" };

        // Ensure an invalid OwnerTypeID throws an exception.
        Assert.Throws<ArgumentException>(() => entity.OwnerTypeID = "InvalidOwnerTypeID", "Setting an invalid OwnerTypeID should throw an ArgumentException.");
    }

    [Test]
    public void TestAllowedOwnerTypeIDs()
    {
        GameEntity entity = new GameEntity();
        var allowedOwnerTypeIDs = new List<string> { "Owner1", "Owner2" };
        entity.AllowedOwnerTypeIDs = allowedOwnerTypeIDs;

        // Ensure the AllowedOwnerTypeIDs is set correctly.
        Assert.AreEqual(allowedOwnerTypeIDs, entity.AllowedOwnerTypeIDs, "AllowedOwnerTypeIDs should be set correctly.");
    }
}