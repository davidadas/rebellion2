// using System.Collections.Generic;
// using System.Linq;
// using System;
// using DependencyInjectionExtensions;

// /// <summary>
// /// Static class that contains methods for evaluating game conditionals.
// /// </summary>
// static class Conditionals
// {
//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool And(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         List<GameConditional> conditionals = (List<GameConditional>)parameters["Conditionals"];
//         return conditionals.All(conditional => conditional.IsMet(serviceLocator));
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool Or(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         List<GameConditional> conditionals = (List<GameConditional>)parameters["Conditionals"];
//         return conditionals.Any(conditional => conditional.IsMet(serviceLocator));
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool Not(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         List<GameConditional> conditionals = (List<GameConditional>)parameters["Conditionals"];
//         return conditionals.All(conditional => !conditional.IsMet(serviceLocator));
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool Xor(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         List<GameConditional> conditionals = (List<GameConditional>)parameters["Conditionals"];
//         return conditionals.Count(conditional => conditional.IsMet(serviceLocator)) == 1;
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool AreOnSamePlanet(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         LookupService lookupService = serviceLocator.GetService<LookupService>();

//         List<string> instanceIDs = (List<string>)parameters["UnitInstanceIDs"];
//         List<SceneNode> sceneNodes = lookupService.GetSceneNodesByInstanceIDs(instanceIDs);
//         Planet comparator = null;

//         // Check if all units are on the same planet.
//         foreach (SceneNode node in sceneNodes)
//         {
//             if (node == null)
//             {
//                 return false;
//             }

//             Planet planet = node.GetClosestParentOfType<Planet>();
//             comparator ??= planet;

//             if (comparator != planet)
//             {
//                 return false;
//             }
//         }

//         return true;
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool AreOnOpposingFactions(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         LookupService lookupService = serviceLocator.GetService<LookupService>();
        
//         List<string> instanceIDs = (List<string>)parameters["UnitInstanceIDs"];
        
//         // Get the scene nodes for the units.
//         List<SceneNode> sceneNodes = lookupService.GetSceneNodesByInstanceIDs(instanceIDs);

//         // Check if the units are on opposing factions.
//         return sceneNodes.Count == 2 && sceneNodes[0].OwnerTypeID != sceneNodes[1].OwnerTypeID;
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool IsOnMission(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         LookupService lookupService = serviceLocator.GetService<LookupService>();

//         string instanceID = (string)parameters["UnitInstanceID"];
//         SceneNode sceneNode = lookupService.GetSceneNodeByInstanceID(instanceID);

//         // Check if the unit is on a mission.
//         return sceneNode != null && sceneNode.GetParent() is Mission;
//     }

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="serviceLocator"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     public static bool AreOnPlanet(IServiceLocator serviceLocator, SerializableDictionary<string, object> parameters)
//     {
//         LookupService lookupService = serviceLocator.GetService<LookupService>();

//         // Get the instance IDs of the units to check.
//         List<string> instanceIDs = (List<string>)parameters["UnitInstanceIDs"];

//         // Check if all units are on a planet.
//         return instanceIDs.All((string instanceID) => {
//             SceneNode sceneNode = lookupService.GetSceneNodeByInstanceID(instanceID);
//             return sceneNode.GetClosestParentOfType<Planet>() != null;
//         });
//     }
// }

// /// <summary>
// /// Factory class for creating conditionals used in game events.
// /// </summary>
// /// <remarks>
// /// The ConditionalFactory is responsible for constructing conditionals that evaluate specific game conditions, 
// /// such as whether two units are on the same planet or are engaged in a mission. Conditionals can also be operators 
// /// like "And", "Or", and "Not" to combine multiple conditionals into one.
// /// </remarks>
// public static class ConditionalFactory
// {
//     private delegate GameConditional ConditionalCreator(SerializableDictionary<string, object> parameters);
//     private static readonly Dictionary<string, ConditionalCreator> conditionCreators = new Dictionary<string, ConditionalCreator>
//     {
//         // Operators
//         { "And", (parameters) => new GenericConditional(Conditionals.And, parameters) },
//         { "Xor", (parameters) => new GenericConditional(Conditionals.Xor, parameters) },
//         { "Or", (parameters) => new GenericConditional(Conditionals.Or, parameters) },
//         { "Not", (parameters) => new GenericConditional(Conditionals.Not, parameters) },
        
//         // Unit Conditionals
//         { "AreOnSamePlanet", (parameters) => new GenericConditional(Conditionals.AreOnSamePlanet, parameters) },
//         { "AreOnOpposingFactions", (parameters) => new GenericConditional(Conditionals.AreOnOpposingFactions, parameters) },
//         { "IsOnMission", (parameters) => new GenericConditional(Conditionals.IsOnMission, parameters) },

//         // Game State Conditionals
//     };

//     /// <summary>
//     /// Creates a conditional based on the specified type and parameters.
//     /// </summary>
//     /// <param name="conditionType">The type of the conditional (e.g., "OnSamePlanet").</param>
//     /// <param name="parameters">The parameters required by the conditional.</param>
//     /// <returns>A GameConditional object that can evaluate the specified condition.</returns>
//     /// <exception cref="ArgumentException">Thrown when an invalid condition type is passed.</exception>
//     public static GameConditional CreateConditional(string conditionType, SerializableDictionary<string, object> parameters)
//     {
//         if (conditionCreators.TryGetValue(conditionType, out var creator))
//         {
//             return creator(parameters);
//         }
//         else
//         {
//             throw new ArgumentException($"Invalid condition type: {conditionType}");
//         }
//     }
// }
