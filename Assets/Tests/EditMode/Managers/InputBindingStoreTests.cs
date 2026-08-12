using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class InputBindingStoreTests
    {
        [Test]
        public void BindingOverrides_TenPreviouslyEmptyActions_RestoreAcrossManagerRestart()
        {
            string[] actionNames =
            {
                "ShowPopularSupport",
                "ShowUprisings",
                "ShowIdleFleets",
                "ShowFleetsEnroute",
                "ShowIdlePersonnel",
                "ShowActivePersonnel",
                "ShowAvailableEnergy",
                "ShowAvailableRawMaterial",
                "ShowMines",
                "ShowRefineries",
            };
            string[] paths =
            {
                "<Keyboard>/f1",
                "<Keyboard>/f2",
                "<Keyboard>/f3",
                "<Keyboard>/f4",
                "<Keyboard>/f5",
                "<Keyboard>/f6",
                "<Keyboard>/f7",
                "<Keyboard>/f8",
                "<Keyboard>/f9",
                "<Keyboard>/f10",
            };
            GameObject firstRoot = new GameObject("FirstInputManager");
            GameObject secondRoot = null;
            try
            {
                InputManager first = firstRoot.AddComponent<InputManager>();
                Guid[] firstSlotIds = new Guid[actionNames.Length];
                for (int index = 0; index < actionNames.Length; index++)
                {
                    string actionPath = $"Strategy/{actionNames[index]}";
                    firstSlotIds[index] = first.GetBindingSlotId(actionPath, 0);
                    first.ApplyBindingSlotOverride(actionPath, 0, paths[index]);
                }

                string json = first.SaveBindingOverrides();
                json = json.Replace(firstSlotIds[0].ToString(), Guid.NewGuid().ToString());
                UnityEngine.Object.DestroyImmediate(firstRoot);
                firstRoot = null;

                secondRoot = new GameObject("SecondInputManager");
                InputManager second = secondRoot.AddComponent<InputManager>();
                second.LoadBindingOverrides(json);
                for (int index = 0; index < actionNames.Length; index++)
                {
                    string actionPath = $"Strategy/{actionNames[index]}";
                    Assert.AreEqual(firstSlotIds[index], second.GetBindingSlotId(actionPath, 0));
                    Assert.AreEqual(
                        paths[index],
                        second.GetEffectiveBindingSlotPath(actionPath, 0)
                    );
                }
            }
            finally
            {
                if (firstRoot != null)
                    UnityEngine.Object.DestroyImmediate(firstRoot);
                if (secondRoot != null)
                    UnityEngine.Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void IsModifierControlName_RecognizesAggregateKeyboardControls()
        {
            Assert.IsTrue(InputManager.IsModifierControlName("ctrl"));
            Assert.IsTrue(InputManager.IsModifierControlName("shift"));
            Assert.IsTrue(InputManager.IsModifierControlName("alt"));
            Assert.IsTrue(InputManager.IsModifierControlName("leftMeta"));
            Assert.IsFalse(InputManager.IsModifierControlName("b"));
        }
    }
}
