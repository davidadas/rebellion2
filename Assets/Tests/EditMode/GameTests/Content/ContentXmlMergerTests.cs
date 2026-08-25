using System.IO;
using System.Xml;
using NUnit.Framework;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentXmlMergerTests
    {
        [Test]
        public void ApplyOverrides_MatchedLeaf_ReplacesDefaultValue()
        {
            XmlDocument defaults = Load("<Config><Speed>10</Speed><Size>5</Size></Config>");
            XmlDocument overrides = Load("<Config><Speed>20</Speed></Config>");

            ContentXmlMerger.ApplyOverrides(defaults, overrides);

            Assert.AreEqual("20", SelectText(defaults, "/Config/Speed"));
            Assert.AreEqual("5", SelectText(defaults, "/Config/Size"));
        }

        [Test]
        public void ApplyOverrides_MatchedBranch_MergesRecursively()
        {
            XmlDocument defaults = Load(
                "<Config><Combat><Rolls>3</Rolls><Range>8</Range></Combat></Config>"
            );
            XmlDocument overrides = Load("<Config><Combat><Range>12</Range></Combat></Config>");

            ContentXmlMerger.ApplyOverrides(defaults, overrides);

            Assert.AreEqual("3", SelectText(defaults, "/Config/Combat/Rolls"));
            Assert.AreEqual("12", SelectText(defaults, "/Config/Combat/Range"));
        }

        [Test]
        public void ApplyOverrides_UnmatchedElement_AppendsToDefaults()
        {
            XmlDocument defaults = Load("<Config><Speed>10</Speed></Config>");
            XmlDocument overrides = Load(
                "<Config><Jedi><Threshold>80</Threshold></Jedi></Config>"
            );

            ContentXmlMerger.ApplyOverrides(defaults, overrides);

            Assert.AreEqual("10", SelectText(defaults, "/Config/Speed"));
            Assert.AreEqual("80", SelectText(defaults, "/Config/Jedi/Threshold"));
        }

        [Test]
        public void ApplyOverrides_RepeatedNamesInDefaults_ReplacesTableWholesale()
        {
            XmlDocument defaults = Load(
                "<Config><Table><Entry>1</Entry><Entry>2</Entry></Table></Config>"
            );
            XmlDocument overrides = Load("<Config><Table><Entry>9</Entry></Table></Config>");

            ContentXmlMerger.ApplyOverrides(defaults, overrides);

            XmlNodeList entries = defaults.SelectNodes("/Config/Table/Entry");
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("9", entries[0].InnerText);
        }

        [Test]
        public void ApplyOverrides_RepeatedNamesInOverride_ReplacesTableWholesale()
        {
            XmlDocument defaults = Load("<Config><Table><Entry>1</Entry></Table></Config>");
            XmlDocument overrides = Load(
                "<Config><Table><Entry>7</Entry><Entry>8</Entry></Table></Config>"
            );

            ContentXmlMerger.ApplyOverrides(defaults, overrides);

            XmlNodeList entries = defaults.SelectNodes("/Config/Table/Entry");
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("7", entries[0].InnerText);
            Assert.AreEqual("8", entries[1].InnerText);
        }

        [Test]
        public void ApplyOverrides_MismatchedRootName_Throws()
        {
            XmlDocument defaults = Load("<Config><Speed>10</Speed></Config>");
            XmlDocument overrides = Load("<Other><Speed>20</Speed></Other>");

            Assert.Throws<InvalidDataException>(() =>
                ContentXmlMerger.ApplyOverrides(defaults, overrides)
            );
        }

        private static XmlDocument Load(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            return document;
        }

        private static string SelectText(XmlDocument document, string xpath)
        {
            return document.SelectSingleNode(xpath)?.InnerText;
        }
    }
}
