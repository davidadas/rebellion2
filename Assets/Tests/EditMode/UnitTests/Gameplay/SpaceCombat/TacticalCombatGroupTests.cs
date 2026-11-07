using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatGroupTests
    {
        [Test]
        public void GetMemberHeading_ThreeDimensionalFighterTarget_UsesRoundedDirectionScale()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            TacticalCombatUnit target = CreateFighter("target", CombatSide.Defender);
            target.Position = new Vector3(3, 4, 5);
            TacticalCombatGroup group = new(10, new[] { fighter }, Vector3.UnitZ);
            group.SetTarget(target);

            Vector3 heading = group.GetMemberHeading(fighter);

            Assert.AreEqual(new Vector3(0.424264073f, 0.565685451f, 0.707106829f), heading);
        }

        [Test]
        public void GetMemberHeading_StationaryUntargetedFighter_PreservesFormationHeading()
        {
            TacticalCombatUnit fighter = CreateFighter("fighter", CombatSide.Attacker);
            Vector3 heading = new(0.57735026f, 0.57735026f, 0.57735026f);
            TacticalCombatGroup group = new(10, new[] { fighter }, heading);

            Assert.AreEqual(heading, group.GetMemberHeading(fighter));
        }

        /// <summary>
        /// Captures an active fighter squadron for group heading behavior.
        /// </summary>
        /// <param name="id">The squadron identifier.</param>
        /// <param name="side">The owning combat side.</param>
        /// <returns>The captured tactical squadron.</returns>
        private static TacticalCombatUnit CreateFighter(string id, CombatSide side) =>
            new(
                new Starfighter
                {
                    InstanceID = id,
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                side,
                side == CombatSide.Attacker ? 1 : 1000001
            );
    }
}
