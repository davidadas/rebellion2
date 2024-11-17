// using NUnit.Framework;
// using System.Collections.Generic;

// // [TestFixture]
// public class ConditionalFactoryTests
// {
//     private Game mockGame;
//     private IServiceLocator serviceLocator;
//     private Planet tatooine;
//     private Officer luke;
//     private Officer vader;
//     private Officer han;

//     [SetUp]
//     public void SetUp()
//     {
//         // Initialize a mock game instance.
//         mockGame = new Game();

//         // Initialize a mock planet.
//         tatooine = new Planet
//         {
//             InstanceID = "TATOOINE",
//             DisplayName = "Tatooine",
//             OwnerTypeID = "FNALL1"
//         };

//         // Initialize mock officers.
//         luke = new Officer
//         {
//             InstanceID = "LUKE_SKYWALKER",
//             DisplayName = "Luke Skywalker",
//             OwnerTypeID = "FNALL1"
//         };

//         vader = new Officer
//         {
//             InstanceID = "DARTH_VADER",
//             DisplayName = "Darth Vader",
//             OwnerTypeID = "FNEMP1"
//         };

//         han = new Officer
//         {
//             InstanceID = "HAN_SOLO",
//             DisplayName = "Han Solo",
//             OwnerTypeID = "FNALL1"
//         };

//         // Add Luke and Han to the planet.
//         tatooine.AddChild(luke);
//         tatooine.AddChild(han);

//         // Add the officers and planet to the game's NodesByInstanceID.
//         mockGame.NodesByInstanceID.Add(tatooine.InstanceID, tatooine);
//         mockGame.NodesByInstanceID.Add(luke.InstanceID, luke);
//         mockGame.NodesByInstanceID.Add(vader.InstanceID, vader);
//         mockGame.NodesByInstanceID.Add(han.InstanceID, han);

//         // Initialize the service locator.
//         serviceLocator = ServiceLocatorHelper.GetServiceLocator(mockGame);
//     }

// //     [Test]
// //     public void TestAreOnSamePlanetConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet conditional (Luke and Han are on the same planet)
// //         Dictionary<string, object> parameters = new Dictionary<string, object>
// //         {
// //             { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } }
// //         };

// //         // Create the conditional using the factory
// //         GameConditional conditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", parameters);

// //         // Verify that the conditional is met (Luke and Han are on Tatooine)
// //         Assert.IsTrue(conditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestAreOnOpposingFactionsConditional()
// //     {
// //         // Create parameters for AreOnOpposingFactions conditional
// //         Dictionary<string, object> parameters = new Dictionary<string, object>
// //         {
// //             { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } }
// //         };

// //         // Create the conditional using the factory
// //         GameConditional conditional = ConditionalFactory.CreateConditional("AreOnOpposingFactions", parameters);

// //         // Verify that the conditional is met (Luke and Vader are from opposing factions)
// //         Assert.IsTrue(conditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestAndConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet and AreOnOpposingFactions conditionals
// //         Dictionary<string, object> samePlanetParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } } };
// //         Dictionary<string, object> opposingFactionsParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } } };

// //         GameConditional samePlanetConditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", samePlanetParams);
// //         GameConditional opposingFactionsConditional = ConditionalFactory.CreateConditional("AreOnOpposingFactions", opposingFactionsParams);

// //         // Combine these into an And conditional
// //         Dictionary<string, object> andParameters = new Dictionary<string, object>
// //         {
// //             { "Conditionals", new List<GameConditional> { samePlanetConditional, opposingFactionsConditional } }
// //         };

// //         GameConditional andConditional = ConditionalFactory.CreateConditional("And", andParameters);

// //         // Verify that the combined conditional is not met (Han and Luke are on the same planet, but Han and Vader are not)
// //         Assert.IsFalse(andConditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestOrConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet and AreOnOpposingFactions conditionals
// //         Dictionary<string, object> samePlanetParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } } };
// //         Dictionary<string, object> opposingFactionsParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } } };

// //         GameConditional samePlanetConditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", samePlanetParams);
// //         GameConditional opposingFactionsConditional = ConditionalFactory.CreateConditional("AreOnOpposingFactions", opposingFactionsParams);

// //         // Combine these into an Or conditional
// //         Dictionary<string, object> orParameters = new Dictionary<string, object>
// //         {
// //             { "Conditionals", new List<GameConditional> { samePlanetConditional, opposingFactionsConditional } }
// //         };

// //         GameConditional orConditional = ConditionalFactory.CreateConditional("Or", orParameters);

// //         // Verify that the combined conditional is met (Luke and Han are on the same planet, or Luke and Vader are from opposing factions)
// //         Assert.IsTrue(orConditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestNotConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet conditional
// //         Dictionary<string, object> samePlanetParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } } };

// //         GameConditional samePlanetConditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", samePlanetParams);

// //         // Combine this into a Not conditional
// //         Dictionary<string, object> notParameters = new Dictionary<string, object>
// //         {
// //             { "Conditionals", samePlanetConditional }
// //         };

// //         GameConditional notConditional = ConditionalFactory.CreateConditional("Not", notParameters);

// //         // Verify that the Not conditional is not met (Luke and Han are on the same planet, so negating this should return false)
// //         Assert.IsFalse(notConditional.IsMet(mockGame));
// //     }

// //     // [Test]
// //     // public void TestNotConditional()
// //     // {
// //     //     var innerParameters = new Dictionary<string, object>();
// //     //     var innerConditional = new GenericConditional(Conditionals.AreOnSamePlanet, innerParameters);

// //     //     var parameters = new Dictionary<string, object>
// //     //     {
// //     //         { "Condition", innerConditional }
// //     //     };
// //     //     var notConditional = new GenericConditional(Conditionals.Not, parameters);

// //     //     var game = new Game();
// //     //     Assert.IsFalse(notConditional.IsMet(game));
// //     // }


// //     [Test]
// //     public void TestXorConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet and AreOnOpposingFactions conditionals
// //         Dictionary<string, object> samePlanetParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } } };
// //         Dictionary<string, object> opposingFactionsParams = new Dictionary<string, object> { { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } } };

// //         GameConditional samePlanetConditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", samePlanetParams);
// //         GameConditional opposingFactionsConditional = ConditionalFactory.CreateConditional("AreOnOpposingFactions", opposingFactionsParams);

// //         // Combine these into an Xor conditional
// //         Dictionary<string, object> xorParameters = new Dictionary<string, object>
// //         {
// //             { "Conditionals", new List<GameConditional> { samePlanetConditional, opposingFactionsConditional } }
// //         };

// //         GameConditional xorConditional = ConditionalFactory.CreateConditional("Xor", xorParameters);

// //         // Verify that the XOR conditional is met (only one of these is true: Luke and Han are on the same planet, but Luke and Vader are from opposing factions).
// //         Assert.IsTrue(xorConditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestSerializeAndDeserializeAreOnSamePlanetConditional()
// //     {
// //         // Create parameters for AreOnSamePlanet conditional (Luke and Han are on the same planet).
// //         Dictionary<string, object> parameters = new Dictionary<string, object>
// //         {
// //             { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } }
// //         };

// //         // Create the conditional using the factory.
// //         GameConditional conditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", parameters);

// //         // Serialize the conditional.
// //         string xml = SerializationHelper.Serialize(conditional);

// //         // Deserialize the conditional.
// //         GameConditional deserializedConditional = SerializationHelper.Deserialize<GameConditional>(xml);

// //         // Verify that the deserialized conditional still works.
// //         Assert.IsTrue(deserializedConditional.IsMet(mockGame));
// //     }

// //     [Test]
// //     public void TestSerializeAndDeserializeAreOnOpposingFactionsConditional()
// //     {
// //         // Create parameters for AreOnOpposingFactions conditional (Luke and Vader).
// //         Dictionary<string, object> parameters = new Dictionary<string, object>
// //         {
// //             { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "DARTH_VADER" } }
// //         };

// //         // Create the conditional using the factory.
// //         GameConditional conditional = ConditionalFactory.CreateConditional("AreOnOpposingFactions", parameters);

// //         // Serialize the conditional.
// //         string xml = SerializationHelper.Serialize(conditional);

// //         // Deserialize the conditional.
// //         GameConditional deserializedConditional = SerializationHelper.Deserialize<GameConditional>(xml);

// //         // Verify that the deserialized conditional still works
// //         Assert.IsTrue(deserializedConditional.IsMet(mockGame));
// //     }

//     [Test]
//     public void TestSerializeAndDeserializeAndConditional()
//     {
//         // Create a sample And conditional.
//         Dictionary<string, object> samePlanetParams = new Dictionary<string, object>
//         {
//             { "UnitInstanceIDs", new List<string> { "LUKE_SKYWALKER", "HAN_SOLO" } }
//         };

//         GameConditional samePlanetConditional = ConditionalFactory.CreateConditional("AreOnSamePlanet", samePlanetParams);

//         List<GameConditional> conditionalList = new List<GameConditional> { samePlanetConditional };
//         Dictionary<string, object> andParams = new Dictionary<string, object>
//         {
//             { "Conditionals", conditionalList }
//         };

//         GameConditional conditional = ConditionalFactory.CreateConditional("And", andParams);

//         // Serialize and then deserialize the conditional.
//         string serializedConditional = SerializationHelper.Serialize(conditional);
//         GameConditional deserializedConditional = SerializationHelper.Deserialize<GameConditional>(serializedConditional);

//         // Ensure the deserialized conditional is not null and is of the expected type.
//         Assert.IsNotNull(deserializedConditional);
//         Assert.IsInstanceOf<GenericConditional>(deserializedConditional);
//     }
// }
