using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;

namespace TND.Common
{
    public static class PlayerLoopHelper
    {
        public static void InsertPlayerLoopSystem<TPlayerLoop, TRelativeTo>(PlayerLoopSystem.UpdateFunction updateDelegate, bool before = false)
            where TRelativeTo : struct
        {
            PlayerLoopSystem currentLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem newPlayerLoop = new()
            {
                subSystemList = null,
                updateDelegate = updateDelegate,
                type = typeof(TPlayerLoop),
            };

            PlayerLoopSystem updatedLoop = InsertPlayerLoopSystem<TRelativeTo>(currentLoop, newPlayerLoop, before);
            PlayerLoop.SetPlayerLoop(updatedLoop);
        }

        private static PlayerLoopSystem InsertPlayerLoopSystem<TRelativeTo>(in PlayerLoopSystem loopSystem, in PlayerLoopSystem newSystem, bool before)
            where TRelativeTo : struct
        {
            // Create a new root PlayerLoopSystem
            PlayerLoopSystem newPlayerLoop = new()
            {
                loopConditionFunction = loopSystem.loopConditionFunction,
                type = loopSystem.type,
                updateDelegate = loopSystem.updateDelegate,
                updateFunction = loopSystem.updateFunction
            };

            // Go through all the subsystems recursively and attempt to insert the new system at the desired location
            newPlayerLoop.subSystemList = InsertPlayerLoopSystemRecursive<TRelativeTo>(loopSystem.subSystemList, newSystem, before);
            return newPlayerLoop;
        }

        private static PlayerLoopSystem[] InsertPlayerLoopSystemRecursive<TRelativeTo>(PlayerLoopSystem[] subSystemList, in PlayerLoopSystem newSystem, bool before)
            where TRelativeTo : struct
        {
            if (subSystemList == null || subSystemList.Length == 0)
            {
                // End recursion
                return subSystemList;
            }

            // Attempt to insert the new system into the current subsystem list
            subSystemList = TryInsertPlayerLoopSystem<TRelativeTo>(subSystemList, newSystem, before);

            // Recursively go through the child subsystems and attempt to insert the new system there
            for (int i = 0; i < subSystemList.Length; ++i)
            {
                subSystemList[i].subSystemList = InsertPlayerLoopSystemRecursive<TRelativeTo>(subSystemList[i].subSystemList, newSystem, before);
            }

            return subSystemList;
        }

        private static PlayerLoopSystem[] TryInsertPlayerLoopSystem<TRelativeTo>(PlayerLoopSystem[] subSystemList, in PlayerLoopSystem newSystem, bool before)
            where TRelativeTo : struct
        {
            // Check if the desired injection point exists within this list
            int foundInjectionPoint = -1, foundExisting = -1;
            for (int i = 0; i < subSystemList.Length; i++)
            {
                if (subSystemList[i].type == typeof(TRelativeTo))
                {
                    foundInjectionPoint = i;
                }

                // Find duplicates of the new system
                if (subSystemList[i].type == newSystem.type)
                {
                    foundExisting = i;
                }
            }

            if (foundInjectionPoint < 0 && foundExisting < 0)
            {
                // Desired injection point is not within this list, and no duplicates found; return early
                return subSystemList;
            }

            // Create a new list to populate with subsystems, and insert the custom system at the desired offset
            List<PlayerLoopSystem> newSubSystemList = new(subSystemList);

            if (foundExisting >= 0)
            {
                // Remove duplicate instances of the new system to be added
                Type newSystemType = newSystem.type;
                newSubSystemList.RemoveAll(pls => pls.type == newSystemType);
            }

            if (foundInjectionPoint >= 0)
            {
                // List size may have changed, so find the correct injection point again
                foundInjectionPoint = newSubSystemList.FindIndex(pls => pls.type == typeof(TRelativeTo));
                newSubSystemList.Insert(before ? foundInjectionPoint : foundInjectionPoint + 1, newSystem);
            }

            return newSubSystemList.ToArray();
        }
    }
}
