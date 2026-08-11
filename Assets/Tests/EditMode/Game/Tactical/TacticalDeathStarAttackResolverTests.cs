using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public sealed class TacticalDeathStarAttackResolverTests
    {
        [Test]
        public void Resolve_SuccessfulAttackRun_DestroysHalfOfCommittedSquadrons()
        {
            TacticalUnitState firstSquadron = CreateFighters(12, 10);
            TacticalUnitState secondSquadron = CreateFighters(12, 10);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0d, 0d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(
                new[] { firstSquadron, secondSquadron },
                1f
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, firstSquadron.Hull);
            Assert.AreEqual(12, secondSquadron.Hull);
        }

        [Test]
        public void Resolve_FailedAttackRun_DestroysCommittedFighters()
        {
            TacticalUnitState fighters = CreateFighters(12, 10);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0.99d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 1f);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, fighters.Hull);
        }

        [Test]
        public void Resolve_FightersWithoutAttackStrength_CannotCompleteAttackRun()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 1f);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, fighters.Hull);
        }

        [Test]
        public void Resolve_FighterCommanderCombat_ContributesToSuccessChance()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0d, 0.02d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 6f);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(12, fighters.Hull);
        }

        [Test]
        public void Resolve_FailedFirstAttempt_RetriesSurvivingSquadron()
        {
            TacticalUnitState fighters = CreateFighters(12, 10);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0d, 0.99d, 0d, 0d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 1f);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(12, fighters.Hull);
        }

        [Test]
        public void Resolve_ApproachFireDamagesSuccessfulSquadron_RestoresSquadronSize()
        {
            TacticalUnitState fighters = CreateFighters(12, 10);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0.5d, 0.25d, 0d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 1f);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.TookApproachDamage);
            Assert.AreEqual(12, fighters.Hull);
        }

        [Test]
        public void Resolve_ImmobileSquadronUnderApproachFire_IsDestroyedBeforeAttack()
        {
            TacticalUnitState fighters = CreateFighters(1, 100, sublightSpeed: 0);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0.99d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 9f);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.TookApproachDamage);
            Assert.AreEqual(0, fighters.Hull);
        }

        [Test]
        public void Resolve_MaximumAgilitySquadron_AvoidsApproachFire()
        {
            TacticalUnitState fighters = CreateFighters(1, 100, agility: 8);
            TacticalDeathStarAttackResolver resolver = new TacticalDeathStarAttackResolver(
                new FixedRandomProvider(new[] { 0.8d, 0d })
            );

            TacticalDeathStarAttackResult result = resolver.Resolve(new[] { fighters }, 1f);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, fighters.Hull);
        }

        private static TacticalUnitState CreateFighters(
            int squadronSize,
            int attackStrength,
            int agility = 0,
            int sublightSpeed = 1
        )
        {
            return TacticalUnitState.FromFighters(
                new Starfighter
                {
                    CurrentSquadronSize = squadronSize,
                    MaxSquadronSize = squadronSize,
                    LaserCannon = attackStrength,
                    Agility = agility,
                    SublightSpeed = sublightSpeed,
                },
                TacticalBattleSide.Attacker
            );
        }
    }
}
