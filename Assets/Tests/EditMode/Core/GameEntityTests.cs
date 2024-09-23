using NUnit.Framework;
using System;

[TestFixture]
public class GameEntityTests
{
    [Test]
    public void TestInstanceIDCreated()
    {
        GameEntity entity = new GameEntity();
        Assert.IsNotNull(entity.InstanceID, "InstanceID should not be null.");
        Assert.IsTrue(Guid.TryParse(entity.InstanceID, out _), "InstanceID should be a valid GUID.");
    }
}
