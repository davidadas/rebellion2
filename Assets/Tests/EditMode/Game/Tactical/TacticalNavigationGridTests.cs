using System;
using NUnit.Framework;
using Rebellion.Game.Tactical;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public sealed class TacticalNavigationGridTests
    {
        [Test]
        public void Constructor_NonPositiveBattlefieldScale_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalNavigationGrid(0f));
        }

        [Test]
        public void SetCount_NewGrid_HasFourSets()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            Assert.AreEqual(4, grid.SetCount);
        }

        [Test]
        public void GetPoints_InnermostSet_IncludesOrigin()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            Assert.That(
                grid.GetPoints(0),
                Has.Some.Matches<TacticalNavPoint>(point =>
                    point.X == 0f && point.Y == 0f && point.Z == 0f
                )
            );
        }

        [TestCase(0, 27)]
        [TestCase(1, 26)]
        [TestCase(2, 26)]
        [TestCase(3, 26)]
        public void GetPoints_FixedSet_HasExpectedPointCount(int setIndex, int expectedCount)
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            Assert.AreEqual(expectedCount, grid.GetPoints(setIndex).Count);
        }

        [Test]
        public void GetPoints_OutermostSet_ReachesBattlefieldScale()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            Assert.That(
                grid.GetPoints(3),
                Has.Some.Matches<TacticalNavPoint>(point =>
                    point.X == 100f && point.Y == 100f && point.Z == 100f
                )
            );
        }

        [TestCase(0, 3)]
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        [TestCase(3, 0)]
        public void GetSetIndexForButton_FixedButton_MapsOuterToInner(
            int buttonIndex,
            int expectedSetIndex
        )
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            int setIndex = grid.GetSetIndexForButton(buttonIndex);

            Assert.AreEqual(expectedSetIndex, setIndex);
        }

        [Test]
        public void ToggleVisibility_HiddenSet_MakesSetVisible()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            bool visible = grid.ToggleVisibility(2);

            Assert.IsTrue(visible);
        }

        [Test]
        public void ToggleVisibility_OneSet_DoesNotChangeAnotherSet()
        {
            TacticalNavigationGrid grid = new TacticalNavigationGrid(100f);

            grid.ToggleVisibility(2);

            Assert.IsFalse(grid.IsVisible(1));
        }
    }
}
