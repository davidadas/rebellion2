using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatAudioTests
    {
        [Test]
        public void Enqueue_SingleVariant_DoesNotDrawFromBattleStream()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 1, 100);
            audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.AreEqual(0, random.CallCount);
        }

        [Test]
        public void Enqueue_FullPlaybackCapacity_SelectsVariantBeforeDeferring()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 4, 100);
            int played = 0;
            audio.SoundRequested += _ => played++;
            for (int index = 0; index < 6; index++)
                audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.AreEqual(5, played);
            Assert.AreEqual(6, random.CallCount);
        }

        [Test]
        public void Advance_PlaybackCapacityAvailable_ReselectsPendingVariant()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 4, 100);
            int played = 0;
            audio.SoundRequested += _ => played++;
            for (int index = 0; index < 6; index++)
                audio.Enqueue(TacticalCombatSound.CapitalLaser);
            audio.Advance(100);
            Assert.AreEqual(6, played);
            Assert.AreEqual(7, random.CallCount);
        }

        [Test]
        public void Advance_PendingSoundExpired_DropsRequestWithoutAnotherDraw()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 4, 1000);
            int played = 0;
            audio.SoundRequested += _ => played++;
            for (int index = 0; index < 6; index++)
                audio.Enqueue(TacticalCombatSound.CapitalLaser);
            audio.Advance(226);
            audio.Advance(1000);
            Assert.AreEqual(5, played);
            Assert.AreEqual(6, random.CallCount);
        }

        [Test]
        public void Enqueue_UnauthoredSound_DoesNotChangeBattleStream()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 4, 100);
            audio.Enqueue(TacticalCombatSound.SmallExplosion);
            Assert.AreEqual(0, random.CallCount);
        }

        [Test]
        public void Enqueue_ReadySound_UsesOneOfItsAuthoredVariants()
        {
            SystemRandomProvider random = new(42);
            TacticalCombatAudio audio = CreateAudio(random, 4, 100);
            TacticalCombatSoundClip selected = null;
            audio.SoundRequested += clip => selected = clip;
            audio.Enqueue(TacticalCombatSound.CapitalLaser);
            Assert.IsNotNull(selected);
            StringAssert.StartsWith("clip-", selected.Path);
        }

        /// <summary>
        /// Creates an isolated sound queue with one authored sound and synthetic clip durations.
        /// </summary>
        /// <param name="random">The shared random-number provider.</param>
        /// <param name="count">The number of authored variants.</param>
        /// <param name="duration">Each clip's duration in milliseconds.</param>
        /// <returns>The queue under test.</returns>
        private static TacticalCombatAudio CreateAudio(
            IRandomNumberProvider random,
            int count,
            float duration
        )
        {
            List<TacticalCombatSoundClip> clips = new();
            for (int index = 0; index < count; index++)
                clips.Add(
                    new TacticalCombatSoundClip
                    {
                        Path = "clip-" + index,
                        DurationMilliseconds = duration,
                    }
                );
            return new TacticalCombatAudio(
                new[]
                {
                    new TacticalCombatSoundDefinition
                    {
                        Kind = TacticalCombatSound.CapitalLaser,
                        Variants = clips,
                    },
                },
                random
            );
        }
    }
}
