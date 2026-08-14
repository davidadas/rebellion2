using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class DisplayManagerTests
    {
        /// <summary>
        /// Verifies that display discovery filters aspect ratios, removes duplicates, and sorts modes.
        /// </summary>
        [Test]
        public void GetSupportedResolutions_MixedModes_ReturnsDistinctSortedSixteenByNineModes()
        {
            DisplayManager manager = CreateManager(
                new[]
                {
                    new Vector2Int(1920, 1200),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(1280, 720),
                    new Vector2Int(1920, 1080),
                }
            );

            CollectionAssert.AreEqual(
                new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) },
                manager.GetSupportedResolutions()
            );
        }

        /// <summary>
        /// Verifies that an unavailable ultrawide request selects the largest fitting 16:9 mode.
        /// </summary>
        [Test]
        public void ResolveResolution_UltrawideRequest_ReturnsLargestFittingMode()
        {
            DisplayManager manager = CreateManager(
                new[]
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                    new Vector2Int(3840, 2160),
                },
                new Vector2Int(3840, 1600)
            );

            Assert.AreEqual(new Vector2Int(2560, 1440), manager.ResolveResolution(3840, 1600));
        }

        /// <summary>
        /// Verifies an explicitly requested supported mode is selected exactly.
        /// </summary>
        [Test]
        public void ResolveResolution_SupportedRequest_ReturnsExactMode()
        {
            DisplayManager manager = CreateManager(
                new[]
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                },
                new Vector2Int(2560, 1440)
            );

            Assert.AreEqual(new Vector2Int(1920, 1080), manager.ResolveResolution(1920, 1080));
        }

        /// <summary>
        /// Verifies discovery falls back to a fitting common 16:9 mode when none are reported.
        /// </summary>
        [Test]
        public void GetSupportedResolutions_NoReportedModes_ReturnsNativeFittingFallback()
        {
            DisplayManager manager = CreateManager(
                System.Array.Empty<Vector2Int>(),
                new Vector2Int(3440, 1440)
            );

            CollectionAssert.AreEqual(
                new[] { new Vector2Int(2560, 1440) },
                manager.GetSupportedResolutions()
            );
        }

        /// <summary>
        /// Verifies that applying settings delegates exactly one resolved mode to the display API.
        /// </summary>
        [Test]
        public void Apply_Settings_UpdatesSettingsAndDelegatesResolvedMode()
        {
            int appliedWidth = 0;
            int appliedHeight = 0;
            FullScreenMode appliedMode = default;
            DisplayManager manager = new DisplayManager(
                () => new[] { new Vector2Int(1920, 1080) },
                () => new Vector2Int(1920, 1080),
                (width, height, mode) =>
                {
                    appliedWidth = width;
                    appliedHeight = height;
                    appliedMode = mode;
                }
            );
            UserVideoSettings settings = new UserVideoSettings
            {
                FullScreenMode = (int)FullScreenMode.Windowed,
            };

            manager.Apply(settings);

            Assert.AreEqual(1920, settings.ResolutionWidth);
            Assert.AreEqual(1080, settings.ResolutionHeight);
            Assert.AreEqual(1920, appliedWidth);
            Assert.AreEqual(1080, appliedHeight);
            Assert.AreEqual(FullScreenMode.Windowed, appliedMode);
        }

        /// <summary>
        /// Creates a display manager over deterministic test modes.
        /// </summary>
        private static DisplayManager CreateManager(
            IReadOnlyList<Vector2Int> modes,
            Vector2Int? native = null
        )
        {
            return new DisplayManager(
                () => modes,
                () => native ?? new Vector2Int(1920, 1080),
                (_, _, _) => { }
            );
        }
    }
}
