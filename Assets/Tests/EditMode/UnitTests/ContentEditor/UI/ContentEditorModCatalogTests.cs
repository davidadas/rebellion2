using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorModCatalogTests
    {
        [Test]
        public void Find_LocalFilter_IncludesInstallationsWithoutPublicListings()
        {
            InstalledModPackage local = Installation("local-pack", true);
            var result = ContentEditorModCatalog.Find(
                new[] { Listing("Remote") },
                new[] { local, Installation("Team-Removed", false) },
                false,
                "",
                new ContentEditorModFilterOptions { Installation = ContentEditorModFilter.Local },
                ContentEditorModSort.Name
            );

            Assert.That(result.Select(entry => entry.ID), Is.EqualTo(new[] { local.PackageID }));
            Assert.That(result.Single().Installation, Is.SameAs(local));
        }

        [Test]
        public void Find_AllFilter_MergesUnlistedInstallationsWithoutDuplicatingPublicListings()
        {
            InstalledModPackage remote = Installation("Team-Remote", false);
            var result = ContentEditorModCatalog.Find(
                new[] { Listing("Remote") },
                new[]
                {
                    remote,
                    Installation("local-pack", true),
                    Installation("Team-Removed", false),
                },
                false,
                "",
                new ContentEditorModFilterOptions(),
                ContentEditorModSort.Name
            );

            CollectionAssert.AreEquivalent(
                new[] { "Team-Remote", "local-pack", "Team-Removed" },
                result.Select(entry => entry.ID)
            );
            Assert.That(
                result.Single(entry => entry.ID == remote.PackageID).Installation,
                Is.SameAs(remote)
            );
        }

        [TestCase(true, 2)]
        [TestCase(false, 1)]
        public void Find_InstallationFilter_FiltersPublicAndLocalEntries(bool installed, int count)
        {
            var result = ContentEditorModCatalog.Find(
                new[] { Listing("Installed"), Listing("Available") },
                new[] { Installation("Team-Installed", false), Installation("local-pack", true) },
                false,
                "",
                new ContentEditorModFilterOptions
                {
                    Installation = installed
                        ? ContentEditorModFilter.Installed
                        : ContentEditorModFilter.NotInstalled,
                },
                ContentEditorModSort.Name
            );

            Assert.That(result.Count, Is.EqualTo(count));
            Assert.That(result.All(entry => entry.IsInstalled == installed), Is.True);
        }

        [Test]
        public void Find_SearchForLocalName_ReturnsMatchingUnlistedInstallation()
        {
            var result = ContentEditorModCatalog.Find(
                new[] { Listing("Remote") },
                new[] { Installation("local-pack", true) },
                false,
                "local",
                new ContentEditorModFilterOptions(),
                ContentEditorModSort.Name
            );

            Assert.That(result.Select(entry => entry.ID), Is.EqualTo(new[] { "local-pack" }));
        }

        /// <summary>
        /// Creates a public listing with valid display and version metadata.
        /// </summary>
        /// <param name="name">The package name.</param>
        /// <returns>The public listing.</returns>
        private static ThunderstorePackage Listing(string name) =>
            new()
            {
                name = name,
                full_name = "Team-" + name,
                owner = "Team",
                versions = new[]
                {
                    new ThunderstorePackageVersion
                    {
                        version_number = "1.0.0",
                        description = "Public package",
                    },
                },
            };

        /// <summary>
        /// Creates an installed package independently of public catalog membership.
        /// </summary>
        /// <param name="id">The installed identity.</param>
        /// <param name="local">Whether the package was installed without a public listing.</param>
        /// <returns>The installation record.</returns>
        private static InstalledModPackage Installation(string id, bool local) =>
            new()
            {
                PackageID = id,
                Name = id,
                Author = "Team",
                Version = "1.0.0",
                Local = local,
            };
    }
}
