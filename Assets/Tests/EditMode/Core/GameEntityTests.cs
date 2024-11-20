// using System;
// using System.Collections.Generic;
// using NUnit.Framework;

// [TestFixture]
// public class GameEntityTests
// {
//     [Test]
//     public void TestInstanceIDCreated()
//     {
//         GameEntity entity = new GameEntity();
//         Assert.IsNotNull(entity.InstanceID, "InstanceID should not be null.");

//         // Ensure the InstanceID is a valid GUID.
//         Assert.IsTrue(
//             Guid.TryParse(entity.InstanceID, out _),
//             "InstanceID should be a valid GUID."
//         );
//     }

//     [Test]
//     public void TestTypeID()
//     {
//         GameEntity entity = new GameEntity();
//         string typeId = "TestTypeID";
//         entity.TypeID = typeId;

//         // Ensure the TypeID is set correctly.
//         Assert.AreEqual(typeId, entity.TypeID, "TypeID should be set correctly.");
//     }

//     [Test]
//     public void TestDisplayName()
//     {
//         GameEntity entity = new GameEntity();
//         string displayName = "TestDisplayName";
//         entity.DisplayName = displayName;

//         // Ensure the DisplayName is set correctly.
//         Assert.AreEqual(displayName, entity.DisplayName, "DisplayName should be set correctly.");
//     }

//     [Test]
//     public void TestDescription()
//     {
//         GameEntity entity = new GameEntity();
//         string description = "TestDescription";
//         entity.Description = description;

//         // Ensure the Description is set correctly.
//         Assert.AreEqual(description, entity.Description, "Description should be set correctly.");
//     }

//     [Test]
//     public void TestOwnerInstanceID()
//     {
//         GameEntity entity = new GameEntity();
//         string ownerInstanceId = "TestOwnerInstanceID";
//         entity.AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId };
//         entity.OwnerInstanceID = ownerInstanceId;

//         // Ensure the OwnerInstanceID is set correctly.
//         Assert.AreEqual(ownerInstanceId, entity.OwnerInstanceID, "OwnerInstanceID should be set correctly.");
//     }

//     [Test]
//     public void TestInvalidOwnerInstanceID()
//     {
//         GameEntity entity = new GameEntity();
//         entity.AllowedOwnerInstanceIDs = new List<string> { "ValidOwnerInstanceID" };

//         // Ensure an invalid OwnerInstanceID throws an exception.
//         Assert.Throws<ArgumentException>(
//             () => entity.OwnerInstanceID = "InvalidOwnerInstanceID",
//             "Setting an invalid OwnerInstanceID should throw an ArgumentException."
//         );
//     }

//     [Test]
//     public void TestAllowedOwnerInstanceIDs()
//     {
//         GameEntity entity = new GameEntity();
//         List<string> allowedOwnerInstanceIDs = new List<string> { "Owner1", "Owner2" };
//         entity.AllowedOwnerInstanceIDs = allowedOwnerInstanceIDs;

//         // Ensure the AllowedOwnerInstanceIDs is set correctly.
//         Assert.AreEqual(
//             allowedOwnerInstanceIDs,
//             entity.AllowedOwnerInstanceIDs,
//             "AllowedOwnerInstanceIDs should be set correctly."
//         );
//     }
// }
