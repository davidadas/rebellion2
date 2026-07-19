using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.SaveMenu.Presentation
{
    [TestFixture]
    public class SaveMenuViewDataTests
    {
        [Test]
        public void WindowRenderData_NullTacticalOptions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuWindowRenderData(
                    null,
                    null,
                    0f,
                    0f,
                    null,
                    null,
                    Array.Empty<SaveSlotRenderData>(),
                    null
                )
            );
        }

        [Test]
        public void WindowRenderData_NullSlots_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SaveMenuWindowRenderData(
                    null,
                    null,
                    0f,
                    0f,
                    null,
                    new Dictionary<UserTacticalOption, bool>(),
                    null,
                    null
                )
            );
        }

        [Test]
        public void WindowRenderData_CompletePresentation_ClampsAndCopiesCollections()
        {
            Texture2D upTexture = new Texture2D(4, 4);
            Texture2D downTexture = new Texture2D(4, 4);
            Dictionary<UserTacticalOption, bool> options = new Dictionary<UserTacticalOption, bool>
            {
                { UserTacticalOption.Starfield, true },
            };
            List<SaveSlotRenderData> slots = new List<SaveSlotRenderData>
            {
                new SaveSlotRenderData(0, "Campaign", true, true, null),
            };

            SaveMenuWindowRenderData data = new SaveMenuWindowRenderData(
                upTexture,
                downTexture,
                -1f,
                2f,
                null,
                options,
                slots,
                "Confirm"
            );
            options[UserTacticalOption.Starfield] = false;
            slots.Clear();

            Assert.AreSame(upTexture, data.ReturnStrategyButtonUpTexture);
            Assert.AreSame(downTexture, data.ReturnStrategyButtonDownTexture);
            Assert.AreEqual(0f, data.MusicVolume);
            Assert.AreEqual(1f, data.SfxVolume);
            Assert.AreEqual(string.Empty, data.VersionText);
            Assert.IsTrue(data.TacticalOptions[UserTacticalOption.Starfield]);
            Assert.AreEqual(1, data.Slots.Count);
            Assert.AreEqual("Campaign", data.Slots[0].Label);
            Assert.AreEqual("Confirm", data.ConfirmationMessage);
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<UserTacticalOption, bool>)data.TacticalOptions).Add(
                    UserTacticalOption.Planet,
                    true
                )
            );
            Assert.Throws<NotSupportedException>(() =>
                ((IList<SaveSlotRenderData>)data.Slots).Add(SaveSlotRenderData.Empty(1))
            );

            UnityEngine.Object.DestroyImmediate(downTexture);
            UnityEngine.Object.DestroyImmediate(upTexture);
        }

        [Test]
        public void SaveSlotRenderData_CompletePresentation_StoresAllValues()
        {
            Texture2D texture = new Texture2D(4, 4);

            SaveSlotRenderData data = new SaveSlotRenderData(2, null, true, false, texture);

            Assert.AreEqual(2, data.Slot);
            Assert.AreEqual(string.Empty, data.Label);
            Assert.IsTrue(data.CanSave);
            Assert.IsFalse(data.CanLoad);
            Assert.AreSame(texture, data.FactionIconTexture);

            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void SaveSlotRenderData_EmptySlot_ReturnsDisabledPresentation()
        {
            SaveSlotRenderData data = SaveSlotRenderData.Empty(4);

            Assert.AreEqual(4, data.Slot);
            Assert.AreEqual(string.Empty, data.Label);
            Assert.IsFalse(data.CanSave);
            Assert.IsFalse(data.CanLoad);
            Assert.IsNull(data.FactionIconTexture);
        }
    }
}
