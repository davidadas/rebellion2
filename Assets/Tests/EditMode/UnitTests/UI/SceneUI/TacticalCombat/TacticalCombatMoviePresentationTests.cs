using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatMoviePresentationTests
    {
        [Test]
        public void SetActive_Entering_HidesBattleBeforeTakingMovieInput()
        {
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = CreatePresentation(events);

            presentation.SetActive(true);

            CollectionAssert.AreEqual(new[] { "hide", "enter" }, events);
            Assert.IsTrue(presentation.IsActive);
        }

        [Test]
        public void SetActive_Returning_ReleasesInputBeforeReactivatingBattle()
        {
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = CreatePresentation(events);
            presentation.SetActive(true);
            events.Clear();

            presentation.SetActive(false);

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
        }

        [Test]
        public void SetActive_RepeatedTransition_RetainsSingleInputOwner()
        {
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = CreatePresentation(events);

            presentation.SetActive(true);
            presentation.SetActive(true);
            presentation.SetActive(false);
            presentation.SetActive(false);

            CollectionAssert.AreEqual(new[] { "hide", "enter", "leave", "show" }, events);
        }

        [Test]
        public void Dispose_ActiveMovie_ReleasesOnceBeforeShowingBattle()
        {
            List<string> events = new();
            TacticalCombatMoviePresentation presentation = CreatePresentation(events);
            presentation.SetActive(true);
            events.Clear();

            presentation.Dispose();
            presentation.Dispose();

            CollectionAssert.AreEqual(new[] { "leave", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
        }

        [Test]
        public void SetActive_InputAcquisitionFails_RestoresBattleWithoutReleasingUnownedContext()
        {
            List<string> events = new();
            using TacticalCombatMoviePresentation presentation = new(
                active => events.Add(active ? "show" : "hide"),
                () => throw new InvalidOperationException("Input unavailable."),
                () => events.Add("leave")
            );

            Assert.Throws<InvalidOperationException>(() => presentation.SetActive(true));

            CollectionAssert.AreEqual(new[] { "hide", "show" }, events);
            Assert.IsFalse(presentation.IsActive);
        }

        /// <summary>
        /// Records ordered transitions through independently owned battle and movie input contexts.
        /// </summary>
        /// <param name="events">The observable transition history.</param>
        /// <returns>The public modal presentation owner.</returns>
        private static TacticalCombatMoviePresentation CreatePresentation(List<string> events) =>
            new(
                active => events.Add(active ? "show" : "hide"),
                () => events.Add("enter"),
                () => events.Add("leave")
            );
    }
}
