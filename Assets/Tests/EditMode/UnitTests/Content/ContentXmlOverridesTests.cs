using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentXmlOverridesTests
    {
        private string folder;

        /// <summary>
        /// Creates an isolated content root for layer-resolution checks.
        /// </summary>
        [SetUp]
        public void SetUp() =>
            folder = Path.Combine(
                Path.GetTempPath(),
                "content-overrides-" + Guid.NewGuid().ToString("N")
            );

        /// <summary>
        /// Removes the isolated content layers.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void Merge_OneFactionSetting_PreservesOtherSettingsAndFactions()
        {
            string baseline =
                "<FactionThemes><FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>green</FactionPrimaryColorHex><IntroCutscenePath>intro</IntroCutscenePath></FactionTheme><FactionTheme><FactionInstanceID>FNALL1</FactionInstanceID><FactionPrimaryColorHex>red</FactionPrimaryColorHex></FactionTheme></FactionThemes>";
            string overlay =
                "<FactionThemes Inherit='true'><FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>blue</FactionPrimaryColorHex></FactionTheme></FactionThemes>";
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));
            Assert.AreEqual(2, result.Elements().Count());
            Assert.AreEqual(
                "intro",
                result.Element("FactionTheme").Element("IntroCutscenePath").Value
            );
            Assert.AreEqual(
                "blue",
                result.Element("FactionTheme").Element("FactionPrimaryColorHex").Value
            );
            Assert.IsNull(result.Attribute("Inherit"));
        }

        [Test]
        public void Merge_ExplicitFalseZeroAndEmpty_PreservesAuthoredValues()
        {
            string baseline =
                "<FactionTheme><UseUpperButtonLayout>true</UseUpperButtonLayout><IntroCutscenePath>intro</IntroCutscenePath><WindowTitleTheme><Height>10</Height></WindowTitleTheme></FactionTheme>";
            string overlay =
                "<FactionTheme Inherit='true'><UseUpperButtonLayout>false</UseUpperButtonLayout><IntroCutscenePath/><WindowTitleTheme><Height>0</Height></WindowTitleTheme></FactionTheme>";
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));
            Assert.AreEqual("false", result.Element("UseUpperButtonLayout").Value);
            Assert.AreEqual(string.Empty, result.Element("IntroCutscenePath").Value);
            Assert.AreEqual("0", result.Element("WindowTitleTheme").Element("Height").Value);
        }

        [Test]
        public void Merge_EmptyRankList_ClearsInheritedList()
        {
            string baseline =
                "<Officers><Officer><TypeID>one</TypeID><AllowedRanks><OfficerRank>Admiral</OfficerRank></AllowedRanks></Officer></Officers>";
            string overlay =
                "<Officers Inherit='true'><Officer><TypeID>one</TypeID><AllowedRanks/></Officer></Officers>";
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));
            Assert.IsEmpty(result.Element("Officer").Element("AllowedRanks").Elements());
        }

        [Test]
        public void Merge_ActionList_ReplacesOrderedActionsWithoutMergingVariableKeys()
        {
            string baseline =
                "<GameEvents><GameEvent><InstanceID>one</InstanceID><Actions><SetEventVariable><Key>old</Key><Operand>1</Operand></SetEventVariable></Actions></GameEvent></GameEvents>";
            string overlay =
                "<GameEvents Inherit='true'><GameEvent><InstanceID>one</InstanceID><Actions><SetEventVariable><Key>new</Key><Operand>2</Operand></SetEventVariable></Actions></GameEvent></GameEvents>";
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));
            XElement actions = result.Element("GameEvent").Element("Actions");
            Assert.AreEqual(1, actions.Elements().Count());
            Assert.AreEqual("new", actions.Element("SetEventVariable").Element("Key").Value);
        }

        [Test]
        public void Update_OneDisplayedEdit_OmitsUnchangedInheritedMembers()
        {
            string baseline =
                "<FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>green</FactionPrimaryColorHex><IntroCutscenePath>intro</IntroCutscenePath></FactionTheme>";
            string edited = baseline.Replace("green", "blue");
            XElement result = XElement.Parse(
                ContentXmlOverrides.Update(baseline, edited, ContentXmlOverrides.Create(baseline))
            );
            Assert.IsNull(result.Element("IntroCutscenePath"));
            Assert.AreEqual("blue", result.Element("FactionPrimaryColorHex").Value);
        }

        [Test]
        public void Update_PreviouslyPinnedDefault_KeepsExplicitOverride()
        {
            string baseline =
                "<FactionTheme><UseUpperButtonLayout>false</UseUpperButtonLayout><FactionPrimaryColorHex>green</FactionPrimaryColorHex></FactionTheme>";
            string authored =
                "<FactionTheme Inherit='true'><UseUpperButtonLayout>false</UseUpperButtonLayout></FactionTheme>";
            XElement result = XElement.Parse(
                ContentXmlOverrides.Update(baseline, baseline.Replace("green", "blue"), authored)
            );
            Assert.AreEqual("false", result.Element("UseUpperButtonLayout").Value);
        }

        [Test]
        public void Update_RemovedRecord_DoesNotResurrectInheritedRecord()
        {
            string baseline =
                "<Officers><Officer><TypeID>one</TypeID><DisplayName>One</DisplayName></Officer><Officer><TypeID>two</TypeID><DisplayName>Two</DisplayName></Officer></Officers>";
            string edited =
                "<Officers><Officer><TypeID>two</TypeID><DisplayName>Two</DisplayName></Officer></Officers>";
            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));
            Assert.AreEqual(1, result.Elements().Count());
            Assert.AreEqual("two", result.Element("Officer").Element("TypeID").Value);
        }

        [Test]
        public void Reset_OneOverride_PreservesOtherOverrides()
        {
            string baseline =
                "<FactionTheme><UseUpperButtonLayout>true</UseUpperButtonLayout><FactionPrimaryColorHex>green</FactionPrimaryColorHex></FactionTheme>";
            string authored =
                "<FactionTheme Inherit='true'><UseUpperButtonLayout>false</UseUpperButtonLayout><FactionPrimaryColorHex>blue</FactionPrimaryColorHex></FactionTheme>";
            XElement effective = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));
            string reset = ContentXmlOverrides.Reset(
                authored,
                effective.Element("UseUpperButtonLayout")
            );
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, reset));
            Assert.AreEqual("true", result.Element("UseUpperButtonLayout").Value);
            Assert.AreEqual("blue", result.Element("FactionPrimaryColorHex").Value);
        }

        [Test]
        public void Merge_MissingInheritedDocument_RejectsOverlay()
        {
            Assert.Throws<InvalidDataException>(() =>
                ContentXmlOverrides.Merge(null, "<FactionTheme Inherit='true'/>")
            );
        }

        [Test]
        public void Merge_EmptyOverlayWithDifferentRoot_RejectsOverlay()
        {
            Assert.Throws<InvalidDataException>(() =>
                ContentXmlOverrides.Merge("<Officers/>", "<Buildings Inherit='true'/>")
            );
        }

        [Test]
        public void Merge_AliasedSetting_OverridesTheSamePersistedMember()
        {
            string baseline =
                "<ReadAliasItem><Name>base</Name><CurrentValue>1</CurrentValue></ReadAliasItem>";
            string overlay =
                "<ReadAliasItem Inherit='true'><LegacyValue>2</LegacyValue></ReadAliasItem>";
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));
            Assert.AreEqual("2", (string)result.Element("CurrentValue"));
            Assert.IsNull(result.Element("LegacyValue"));
        }

        [Test]
        public void Load_MultipleInheritedPacks_AppliesOverridesInLayerOrder()
        {
            string baseline = WriteLayer(
                "Base",
                "<FactionTheme><UseUpperButtonLayout>true</UseUpperButtonLayout><FactionPrimaryColorHex>red</FactionPrimaryColorHex></FactionTheme>"
            );
            string middle = WriteLayer(
                "Middle",
                "<FactionTheme Inherit='true'><FactionPrimaryColorHex>blue</FactionPrimaryColorHex></FactionTheme>"
            );
            string local = WriteLayer(
                "Local",
                "<FactionTheme Inherit='true'><UseUpperButtonLayout>false</UseUpperButtonLayout></FactionTheme>"
            );
            ContentFileResolver resolver = new ContentFileResolver(
                folder,
                local,
                fallbackPackRootPaths: new[] { middle, baseline }
            );

            XElement result = XElement.Parse(ContentXmlOverrides.Load(resolver, "Pack/theme.xml"));

            Assert.AreEqual("blue", (string)result.Element("FactionPrimaryColorHex"));
            Assert.AreEqual("false", (string)result.Element("UseUpperButtonLayout"));
        }

        [Test]
        public void Load_FullReplacement_DoesNotReadMalformedOverriddenBase()
        {
            string baseline = WriteLayer("Base", "malformed hidden XML");
            string replacement =
                "<FactionTheme><FactionPrimaryColorHex>blue</FactionPrimaryColorHex></FactionTheme>";
            string local = WriteLayer("Local", replacement);
            ContentFileResolver resolver = new ContentFileResolver(
                folder,
                local,
                fallbackPackRootPaths: new[] { baseline }
            );

            Assert.AreEqual(replacement, ContentXmlOverrides.Load(resolver, "Pack/theme.xml"));
        }

        [Test]
        public void Update_RemovedAttribute_PreservesItsAbsenceAfterMerge()
        {
            string baseline =
                "<GameEvents><GameEvent MaximumActivations='1'><InstanceID>one</InstanceID></GameEvent></GameEvents>";
            string edited =
                "<GameEvents><GameEvent><InstanceID>one</InstanceID></GameEvent></GameEvents>";
            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );

            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));

            Assert.IsNull(result.Element("GameEvent").Attribute("MaximumActivations"));
            Assert.IsNull(result.Element("GameEvent").Attribute("RemoveAttributes"));
        }

        [Test]
        public void Reset_RemovedAttribute_RestoresTheInheritedValue()
        {
            XElement baseline = XElement.Parse(
                "<GameEvents><GameEvent MaximumActivations='1'><InstanceID>one</InstanceID></GameEvent></GameEvents>"
            );
            string authored =
                "<GameEvents Inherit='true'><GameEvent RemoveAttributes='MaximumActivations'><InstanceID>one</InstanceID></GameEvent></GameEvents>";
            string reset = ContentXmlOverrides.Reset(
                authored,
                baseline.Element("GameEvent"),
                "MaximumActivations"
            );

            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline.ToString(), reset));

            Assert.AreEqual(
                "1",
                (string)result.Element("GameEvent").Attribute("MaximumActivations")
            );
        }

        [Test]
        public void Update_RemovedRecordAlsoMissingFromBase_PreservesExplicitDeletion()
        {
            string previous =
                "<Officers Inherit='true'><Officer Remove='true'><TypeID>one</TypeID></Officer></Officers>";
            string authored = ContentXmlOverrides.Update("<Officers/>", "<Officers/>", previous);

            XElement result = XElement.Parse(
                ContentXmlOverrides.Merge(
                    "<Officers><Officer><TypeID>one</TypeID><DisplayName>Restored</DisplayName></Officer></Officers>",
                    authored
                )
            );

            Assert.IsEmpty(result.Elements());
        }

        [Test]
        public void Merge_SingleAnonymousRootRecord_ReplacesTheCollection()
        {
            string baseline =
                "<MessageDefinitions><MessageDefinition><ResultType>FleetArrived</ResultType><Subject>Old</Subject><Body>Old body</Body></MessageDefinition></MessageDefinitions>";
            string overlay =
                "<MessageDefinitions Inherit='true'><MessageDefinition><ResultType>FleetDestroyed</ResultType><Subject>New</Subject></MessageDefinition></MessageDefinitions>";

            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, overlay));

            Assert.AreEqual(
                "FleetDestroyed",
                (string)result.Element("MessageDefinition").Element("ResultType")
            );
            Assert.IsNull(result.Element("MessageDefinition").Element("Body"));
        }

        [Test]
        public void Update_RemovingLastAnonymousRootRecord_WritesAnEmptyReplacement()
        {
            string baseline =
                "<MessageDefinitions><MessageDefinition><ResultType>FleetArrived</ResultType><Subject>Old</Subject></MessageDefinition></MessageDefinitions>";

            string authored = ContentXmlOverrides.Update(
                baseline,
                "<MessageDefinitions/>",
                ContentXmlOverrides.Create(baseline)
            );

            Assert.IsFalse(ContentXmlOverrides.IsOverlay(authored));
            Assert.IsEmpty(XElement.Parse(authored).Elements());
        }

        /// <summary>
        /// Writes one isolated layer without installing a global content selection.
        /// </summary>
        /// <param name="name">The layer name.</param>
        /// <param name="xml">The layer's authored XML.</param>
        /// <returns>The layer root.</returns>
        private string WriteLayer(string name, string xml)
        {
            string root = Path.Combine(folder, name);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "theme.xml"), xml);
            return root;
        }
    }
}
