using NUnit.Framework;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserContentSettingsTests
    {
        [Test]
        public void SetDisabledModsForPack_SelectedPackChanged_ReplacesSelectedPackAndPreservesOtherPacks()
        {
            UserContentSettings settings = new UserContentSettings
            {
                DisabledModIDs = new[] { "other-pack-mod", "selected-pack-old-mod" },
            };

            settings.SetDisabledModsForPack(
                new[] { "selected-pack-old-mod", "selected-pack-new-mod" },
                new[] { "selected-pack-new-mod", "unknown-mod" }
            );

            CollectionAssert.AreEqual(
                new[] { "other-pack-mod", "selected-pack-new-mod" },
                settings.DisabledModIDs
            );
        }
    }
}
