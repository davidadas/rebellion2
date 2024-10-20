// using System.Collections.Generic;
// using System.Linq;
// using System;
// using DependencyInjectionExtensions;

// /// <summary>
// /// Static class that contains methods for evaluating game conditionals.
// /// </summary>
// static class Actions
// {
//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="locator"></param>
//     /// <param name="parameters"></param>
//     public static void CreateMission(IServiceLocator locator, SerializableDictionary<string, object> parameters)
//     {
//         MissionService missionService = locator.GetService<MissionService>();
//         LookupService lookupService = locator.GetService<LookupService>();

//         // Get the parameters for the action.
//         List<string> mainParticipantIds = (List<string>)parameters["MainParticipantInstanceIDs"];
//         string missionType = (string)parameters["MissionType"];
//         string targetId = (string)parameters["TargetInstanceID"];
//     }    
// }

// /// <summary>
// /// Static class that contains methods for creating game conditionals.
// /// </summary>
// public static class ActionFactory
// {
//     private delegate GameAction ActionCreator(SerializableDictionary<string, object> parameters);
//     private static readonly SerializableDictionary<string, ActionCreator> actionCreators = new SerializableDictionary<string, ActionCreator>
//     {
//         { "CreateMission", (parameters) => new GenericAction(Actions.CreateMission, parameters) },
//     };

//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="actionType"></param>
//     /// <param name="parameters"></param>
//     /// <returns></returns>
//     /// <exception cref="ArgumentException"></exception>
//     public static GameAction CreateAction(string actionType, SerializableDictionary<string, object> parameters)
//     {
//         if (actionCreators.TryGetValue(actionType, out var creator))
//         {
//             return creator(parameters);
//         }
//         else
//         {
//             throw new ArgumentException($"Invalid condition type: {actionType}");
//         }
//     }
// }
