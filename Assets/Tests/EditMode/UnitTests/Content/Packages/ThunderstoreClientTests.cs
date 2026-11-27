using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.Content.Packages
{
    [TestFixture]
    public sealed class ThunderstoreClientTests
    {
        private const string _release =
            "{\"version_number\":\"1.0.0\",\"download_url\":\"https://thunderstore.io/package/download/Team/Pack/1.0.0/\"}";
        private const string _package =
            "{\"owner\":\"Team\",\"name\":\"Pack\",\"full_name\":\"Team-Pack\",\"versions\":["
            + _release
            + "]}";

        [TestCase("[null]")]
        [TestCase("[" + _package + "," + _package + "]")]
        [TestCase(
            "[{\"owner\":\"Team\",\"name\":\"Pack\",\"full_name\":\"Team-Pack\",\"versions\":[null]}]"
        )]
        [TestCase(
            "[{\"owner\":\"Team\",\"name\":\"Pack\",\"full_name\":\"Team-Pack\",\"versions\":["
                + _release
                + ","
                + _release
                + "]}]"
        )]
        public void ParseCatalog_InvalidEntries_RejectsUnusableCatalog(string json)
        {
            Assert.Throws<InvalidDataException>(() => ThunderstoreClient.ParseCatalog(json));
        }

        [Test]
        public void ParseCatalog_ValidPackage_ReadsReleaseAndNormalizesDependencies()
        {
            ThunderstorePackage[] packages = ThunderstoreClient.ParseCatalog("[" + _package + "]");

            Assert.That(packages.Length, Is.EqualTo(1));
            Assert.That(packages[0].full_name, Is.EqualTo("Team-Pack"));
            Assert.That(packages[0].Latest.version_number, Is.EqualTo("1.0.0"));
            Assert.That(packages[0].Latest.dependencies, Is.Empty);
        }
    }
}
