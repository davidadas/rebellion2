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
    public void TestGameID()
    {
        GameEntity entity = new GameEntity();
        string gameID = "TestGameID";
        entity.GameID = gameID;

        // Ensure the GameID is set correctly.
        Assert.AreEqual(gameID, entity.GameID, "GameID should be set correctly.");
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
    public void TestOwnerGameID()
    {
        GameEntity entity = new GameEntity();
        string ownerGameID = "TestOwnerGameID";
        entity.AllowedOwnerGameIDs = new List<string> { ownerGameID };
        entity.OwnerGameID = ownerGameID;

        // Ensure the OwnerGameID is set correctly.
        Assert.AreEqual(ownerGameID, entity.OwnerGameID, "OwnerGameID should be set correctly.");
    }

    [Test]
    public void TestInvalidOwnerGameID()
    {
        GameEntity entity = new GameEntity();
        entity.AllowedOwnerGameIDs = new List<string> { "ValidOwnerGameID" };

        // Ensure an invalid OwnerGameID throws an exception.
        Assert.Throws<ArgumentException>(() => entity.OwnerGameID = "InvalidOwnerGameID", "Setting an invalid OwnerGameID should throw an ArgumentException.");
    }

    [Test]
    public void TestAllowedOwnerGameIDs()
    {
        GameEntity entity = new GameEntity();
        var allowedOwnerGameIDs = new List<string> { "Owner1", "Owner2" };
        entity.AllowedOwnerGameIDs = allowedOwnerGameIDs;

        // Ensure the AllowedOwnerGameIDs is set correctly.
        Assert.AreEqual(allowedOwnerGameIDs, entity.AllowedOwnerGameIDs, "AllowedOwnerGameIDs should be set correctly.");
    }
}