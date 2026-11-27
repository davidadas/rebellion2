using System.Linq;
using NUnit.Framework;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public sealed class GameplayArtworkLayoutTests
    {
        private GameObject root;
        private Transform[] images;

        /// <summary>
        /// Creates one image group with background and selection outside its editable stack.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Card");
            images = new[]
            {
                "Background",
                "Entity",
                "Stats",
                "Travel",
                "Damage",
                "Captured",
                "Selection",
            }
                .Select(name => new GameObject(name).transform)
                .ToArray();
            foreach (Transform image in images)
                image.SetParent(root.transform);
        }

        /// <summary>
        /// Releases the fixture hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Apply_InjuryAboveCapture_PreservesBackgroundSelectionAndStats()
        {
            GameplayArtworkLayout.Apply(
                GameplayArtworkOrder.Resolve(
                    new[]
                    {
                        GameplayArtworkLayer.Entity,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Damage,
                    }
                ),
                images[1],
                images[3],
                images[4],
                images[5],
                images[2]
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    "Background",
                    "Entity",
                    "Stats",
                    "Travel",
                    "Captured",
                    "Damage",
                    "Selection",
                },
                root.transform.Cast<Transform>().Select(image => image.name)
            );
        }

        [Test]
        public void Apply_ShipHeaderWithoutCaptureLayer_PreservesConfiguredOrder()
        {
            Object.DestroyImmediate(images[2].gameObject);
            Object.DestroyImmediate(images[5].gameObject);
            GameplayArtworkLayout.Apply(
                GameplayArtworkOrder.Resolve(
                    new[]
                    {
                        GameplayArtworkLayer.Damage,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Entity,
                        GameplayArtworkLayer.Captured,
                    }
                ),
                images[1],
                images[3],
                images[4]
            );

            CollectionAssert.AreEqual(
                new[] { "Background", "Damage", "Travel", "Entity", "Selection" },
                root.transform.Cast<Transform>().Select(image => image.name)
            );
        }

        [Test]
        public void Apply_PooledCardReturnsToDefault_RemovesPreviousUnitsOrder()
        {
            GameplayArtworkLayout.Apply(
                GameplayArtworkOrder.Resolve(
                    new[]
                    {
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Damage,
                        GameplayArtworkLayer.Travel,
                        GameplayArtworkLayer.Entity,
                    }
                ),
                images[1],
                images[3],
                images[4],
                images[5],
                images[2]
            );
            GameplayArtworkLayout.Apply(
                GameplayArtworkOrder.Resolve(null),
                images[1],
                images[3],
                images[4],
                images[5],
                images[2]
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    "Background",
                    "Entity",
                    "Stats",
                    "Travel",
                    "Damage",
                    "Captured",
                    "Selection",
                },
                root.transform.Cast<Transform>().Select(image => image.name)
            );
        }
    }
}
