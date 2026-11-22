using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor
{
    [TestFixture]
    public sealed class ContentEditorArtworkRecipeTests
    {
        [Test]
        public void ReadForOutput_OtherSizesHaveNoSidecars_RestoresThisOutput()
        {
            var saved = new ContentEditorArtworkRecipe { OutputMember = "InTransitSmallImagePath" };
            saved.Layers.Add(new ContentEditorArtworkLayer { Source = 0, Hidden = true });

            var reopened = ContentEditorArtworkRecipe.ReadForOutput(
                JsonUtility.ToJson(saved),
                "InTransitSmallImagePath",
                0,
                3,
                true
            );

            Assert.That(reopened.Layers[0].Hidden, Is.True);
        }

        [Test]
        public void ReadForOutput_LegacyMultiSizeSidecar_RetainsOnlyThisOutputSource()
        {
            var saved = new ContentEditorArtworkRecipe
            {
                UsesExisting = true,
                ExistingSources = new[] { "small", "large", "battle" },
            };

            var reopened = ContentEditorArtworkRecipe.ReadForOutput(
                JsonUtility.ToJson(saved),
                "InTransitImagePath",
                1,
                3,
                true
            );

            Assert.That(reopened.ExistingSources, Is.EqualTo(new[] { "large" }));
        }

        [Test]
        public void ReadForOutput_ScaledWake_RestoresSavedSize()
        {
            var saved = new ContentEditorArtworkRecipe { OutputMember = "InTransitImagePath" };
            saved.Layers.Add(new ContentEditorArtworkLayer { Source = 0, ScalePercent = 60 });
            var reopened = ContentEditorArtworkRecipe.ReadForOutput(
                JsonUtility.ToJson(saved),
                "InTransitImagePath",
                0,
                1,
                true
            );

            Assert.That(reopened.Layers[0].EffectiveScalePercent, Is.EqualTo(60));
        }

        [Test]
        public void ReadForOutput_SidecarBelongsToAnotherOutput_OpensAsImportedArtwork()
        {
            var saved = new ContentEditorArtworkRecipe { OutputMember = "InTransitSmallImagePath" };

            var reopened = ContentEditorArtworkRecipe.ReadForOutput(
                JsonUtility.ToJson(saved),
                "InTransitImagePath",
                1,
                3,
                true
            );

            Assert.That(reopened, Is.Null);
        }
    }
}
