using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class ItemSelectionTests
    {
        [Test]
        public void ApplyTo_UnchangedQuantities_PreservesInterleavedDuplicatesAndOrder()
        {
            string[] original = { "a", "b", "a", "c", "b" };
            CollectionAssert.AreEqual(
                original,
                new ItemSelection(ItemSelection.Count(original)).ApplyTo(original)
            );
        }

        [Test]
        public void ApplyTo_ChangedQuantities_RetainsExistingOrderAndAppendsNewEntries()
        {
            var selection = new ItemSelection(
                new Dictionary<string, int>
                {
                    ["a"] = 1,
                    ["b"] = 3,
                    ["d"] = 1,
                }
            );
            CollectionAssert.AreEqual(
                new[] { "a", "b", "b", "b", "d" },
                selection.ApplyTo(new[] { "a", "b", "a", "c" })
            );
        }

        [Test]
        public void SetCount_Zero_RemovesTheItemWithoutChangingTheSource()
        {
            var source = new Dictionary<string, int> { ["a"] = 3 };
            var selection = new ItemSelection(source);
            selection.SetCount("a", 0);
            Assert.IsEmpty(selection.Counts);
            Assert.AreEqual(3, source["a"]);
        }

        [Test]
        public void SetCount_NegativeQuantity_RejectsTheEdit()
        {
            var selection = new ItemSelection(new Dictionary<string, int>());
            Assert.Throws<ArgumentOutOfRangeException>(() => selection.SetCount("a", -1));
            Assert.IsEmpty(selection.Counts);
        }
    }
}
