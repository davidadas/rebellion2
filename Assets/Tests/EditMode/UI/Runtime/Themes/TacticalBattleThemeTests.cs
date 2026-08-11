using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public sealed class TacticalBattleThemeTests
    {
        [Test]
        public void GetAudio_NumberedTaskForce_ReturnsTaskForceResponse()
        {
            TacticalGroupVoiceTheme theme = CreateGroupTheme();

            string audio = theme.GetAudio(TacticalUnitKind.CapitalShip, 1);

            Assert.AreEqual("task-force-2", audio);
        }

        [Test]
        public void GetAudio_NumberedFighterGroup_ReturnsFighterGroupResponse()
        {
            TacticalGroupVoiceTheme theme = CreateGroupTheme();

            string audio = theme.GetAudio(TacticalUnitKind.Fighters, 0);

            Assert.AreEqual("fighter-group-red", audio);
        }

        [Test]
        public void GetAudio_GroupOutsideConfiguredRange_ReturnsGenericShipResponse()
        {
            TacticalGroupVoiceTheme theme = CreateGroupTheme();

            string audio = theme.GetAudio(TacticalUnitKind.CapitalShip, 7);

            Assert.AreEqual("ship", audio);
        }

        [Test]
        public void GetAudio_NegativeGroupIndex_ThrowsArgumentOutOfRangeException()
        {
            TacticalGroupVoiceTheme theme = CreateGroupTheme();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                theme.GetAudio(TacticalUnitKind.CapitalShip, -1)
            );
        }

        [Test]
        public void GetAudioPaths_DuplicateAudioNames_ReturnsDistinctRootedAddresses()
        {
            TacticalGroupVoiceTheme group = new TacticalGroupVoiceTheme
            {
                Ship = "shared",
                TaskForces = new List<string> { "shared", "task-force-2" },
            };
            TacticalVoiceTheme theme = new TacticalVoiceTheme
            {
                AudioRoot = "Pack/Faction/Tactical/Audio/Voice/",
                FleetReady = "shared",
                ManeuverAcknowledged = group,
            };

            string[] paths = theme.GetAudioPaths().ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Faction/Tactical/Audio/Voice/shared",
                    "Pack/Faction/Tactical/Audio/Voice/task-force-2",
                },
                paths
            );
        }

        [Test]
        public void GetAudio_ActiveAgainstWithdrawn_ReturnsWithdrawalVictory()
        {
            TacticalOutcomeVoiceTheme theme = CreateOutcomeTheme();

            string audio = theme.GetAudio(
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            Assert.AreEqual("victory-withdrawal", audio);
        }

        [Test]
        public void GetAudio_ActiveAgainstDestroyed_ReturnsDestructionVictory()
        {
            TacticalOutcomeVoiceTheme theme = CreateOutcomeTheme();

            string audio = theme.GetAudio(
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );

            Assert.AreEqual("victory-destruction", audio);
        }

        [Test]
        public void GetAudio_DestroyedAgainstActive_ReturnsFleetDefeat()
        {
            TacticalOutcomeVoiceTheme theme = CreateOutcomeTheme();

            string audio = theme.GetAudio(
                SpaceCombatSideOutcome.Destroyed,
                SpaceCombatSideOutcome.Active
            );

            Assert.AreEqual("defeat", audio);
        }

        /// <summary>
        /// Creates a minimal command-group response set for resolution tests.
        /// </summary>
        /// <returns>The configured response set.</returns>
        private static TacticalGroupVoiceTheme CreateGroupTheme()
        {
            return new TacticalGroupVoiceTheme
            {
                Ship = "ship",
                TaskForces = new List<string> { "task-force-1", "task-force-2" },
                FighterGroups = new List<string> { "fighter-group-red" },
            };
        }

        /// <summary>
        /// Creates the three original terminal tactical reports.
        /// </summary>
        /// <returns>The configured outcome reports.</returns>
        private static TacticalOutcomeVoiceTheme CreateOutcomeTheme()
        {
            return new TacticalOutcomeVoiceTheme
            {
                EnemyWithdrew = "victory-withdrawal",
                EnemyDestroyed = "victory-destruction",
                FleetDestroyed = "defeat",
            };
        }
    }
}
