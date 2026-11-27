using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.SceneGraph
{
    [TestFixture]
    public sealed class GameplayArtworkOrderTests
    {
        [Test]
        public void Resolve_UnconfiguredUnit_PreservesEstablishedDrawingOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    GameplayArtworkLayer.Entity,
                    GameplayArtworkLayer.Travel,
                    GameplayArtworkLayer.Damage,
                    GameplayArtworkLayer.Captured,
                },
                GameplayArtworkOrder.Resolve(null)
            );
        }

        [Test]
        public void Resolve_PartialOrder_KeepsEveryStatusAvailable()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    GameplayArtworkLayer.Captured,
                    GameplayArtworkLayer.Entity,
                    GameplayArtworkLayer.Travel,
                    GameplayArtworkLayer.Damage,
                },
                GameplayArtworkOrder.Resolve(
                    new[]
                    {
                        GameplayArtworkLayer.Captured,
                        GameplayArtworkLayer.Captured,
                        (GameplayArtworkLayer)100,
                    }
                )
            );
        }

        [Test]
        public void SerializeAndDeserialize_ArtworkOrder_PreservesOrder()
        {
            var unit = new BaseGameEntity
            {
                ArtworkLayerOrder = new List<GameplayArtworkLayer>
                {
                    GameplayArtworkLayer.Entity,
                    GameplayArtworkLayer.Captured,
                    GameplayArtworkLayer.Travel,
                    GameplayArtworkLayer.Damage,
                },
            };
            BaseGameEntity restored = SerializationHelper.Deserialize<BaseGameEntity>(
                SerializationHelper.Serialize(unit)
            );

            CollectionAssert.AreEqual(unit.ArtworkLayerOrder, restored.ArtworkLayerOrder);
        }

        [Test]
        public void CreateCopy_OfficerArtworkOrder_DoesNotShareMutableSettings()
        {
            var officer = new Officer
            {
                ArtworkLayerOrder = new List<GameplayArtworkLayer>
                {
                    GameplayArtworkLayer.Entity,
                    GameplayArtworkLayer.Captured,
                    GameplayArtworkLayer.Travel,
                    GameplayArtworkLayer.Damage,
                },
            };
            var copy = (Officer)officer.CreateCopy();
            copy.ArtworkLayerOrder.Reverse();

            Assert.AreEqual(GameplayArtworkLayer.Entity, officer.ArtworkLayerOrder[0]);
        }

        [Test]
        public void Deserialize_OlderEntityWithoutArtworkOrder_UsesEstablishedOrder()
        {
            var unit = SerializationHelper.Deserialize<BaseGameEntity>("<BaseGameEntity />");

            CollectionAssert.AreEqual(
                GameplayArtworkOrder.Resolve(null),
                GameplayArtworkOrder.Resolve(unit.ArtworkLayerOrder)
            );
        }
    }
}
