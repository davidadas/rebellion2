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
        public void Update_OneMessagePicture_WritesOnlyThatSettingAndIdentity()
        {
            string baseline = Messages(
                Report("arrived", "FleetArrived"),
                Report("report", "MissionReport")
            );
            XElement edited = XElement.Parse(baseline);
            edited
                .Elements()
                .First()
                .Element("BackgroundImage")
                .SetAttributeValue("Path", "custom.png");

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited.ToString(),
                ContentXmlOverrides.Create(baseline)
            );

            Assert.IsTrue(ContentXmlOverrides.IsOverlay(authored));
            XElement saved = XElement.Parse(authored);
            Assert.AreEqual(1, saved.Elements().Count());
            Assert.AreEqual(
                "arrived",
                saved.Element("MessageDefinition").Element("InstanceID").Value
            );
            Assert.IsFalse(saved.Descendants("Body").Any());
            Assert.AreEqual(
                "custom.png",
                (string)saved.Descendants("BackgroundImage").Single().Attribute("Path")
            );
        }

        [Test]
        public void Merge_MessagePictureOverride_InheritsUpdatedTextAndNewReportTypes()
        {
            string baseline = Messages(
                Report("arrived", "FleetArrived"),
                Report("report", "MissionReport")
            );
            XElement edited = XElement.Parse(baseline);
            edited
                .Elements()
                .First()
                .Element("BackgroundImage")
                .SetAttributeValue("Path", "custom.png");
            string authored = ContentXmlOverrides.Update(
                baseline,
                edited.ToString(),
                ContentXmlOverrides.Create(baseline)
            );
            string updated = Messages(
                Report("arrived", "FleetArrived", "Updated arrival"),
                Report("report", "MissionReport", "Updated report"),
                Report("killed", "MissionReport", "Target killed")
            );

            XElement result = XElement.Parse(ContentXmlOverrides.Merge(updated, authored));

            Assert.AreEqual(3, result.Elements().Count());
            Assert.AreEqual("Updated arrival", result.Elements().First().Element("Body").Value);
            Assert.AreEqual(
                "custom.png",
                (string)result.Elements().First().Element("BackgroundImage").Attribute("Path")
            );
            Assert.AreEqual("Target killed", result.Elements().Last().Element("Body").Value);
        }

        [Test]
        public void Update_RemovedIdentifiedMessage_KeepsRemovalAndInheritsFutureAdditions()
        {
            string baseline = Messages(
                Report("arrived", "FleetArrived"),
                Report("report", "MissionReport")
            );
            string edited = Messages(Report("arrived", "FleetArrived"));

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );
            XElement result = XElement.Parse(
                ContentXmlOverrides.Merge(
                    Messages(
                        Report("arrived", "FleetArrived"),
                        Report("report", "MissionReport"),
                        Report("new", "BlockadeDetected")
                    ),
                    authored
                )
            );

            Assert.IsTrue(ContentXmlOverrides.IsOverlay(authored));
            CollectionAssert.AreEqual(
                new[] { "arrived", "new" },
                result.Elements().Select(record => record.Element("InstanceID").Value)
            );
            Assert.AreEqual(
                "true",
                (string)XElement.Parse(authored).Element("MessageDefinition").Attribute("Remove")
            );
        }

        [Test]
        public void Reset_MessagePicture_PrunesTheOverrideAndRestoresInheritance()
        {
            string baseline = Messages(Report("arrived", "FleetArrived"));
            XElement edited = XElement.Parse(baseline);
            XElement picture = edited.Element("MessageDefinition").Element("BackgroundImage");
            picture.SetAttributeValue("Path", "custom.png");
            string authored = ContentXmlOverrides.Update(
                baseline,
                edited.ToString(),
                ContentXmlOverrides.Create(baseline)
            );
            Assert.IsTrue(ContentXmlOverrides.CanResetSetting(picture, "Path"));
            Assert.IsFalse(
                ContentXmlOverrides.CanResetSetting(
                    edited.Element("MessageDefinition").Element("InstanceID")
                )
            );

            string reset = ContentXmlOverrides.Reset(authored, picture, "Path");

            Assert.IsTrue(ContentXmlOverrides.IsEmpty(reset));
            Assert.AreEqual(baseline, ContentXmlOverrides.Merge(baseline, reset));
        }

        [Test]
        public void Update_ChangedMessageOutcome_KeepsIdentityAndLookupOrder()
        {
            string baseline = Messages(
                Report("first", "MissionReport"),
                Report("second", "MissionReport")
            );
            XElement edited = XElement.Parse(baseline);
            edited.Elements().First().SetElementValue("Outcome", "Failed");

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited.ToString(),
                ContentXmlOverrides.Create(baseline)
            );
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));

            Assert.IsTrue(ContentXmlOverrides.IsOverlay(authored));
            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                result.Elements().Select(record => record.Element("InstanceID").Value)
            );
            Assert.AreEqual("Failed", result.Elements().First().Element("Outcome").Value);
        }

        [Test]
        public void Update_InsertedMessage_PreservesItsPositionWithoutCopyingNeighbors()
        {
            string baseline = Messages(
                Report("first", "FleetArrived"),
                Report("last", "MissionReport")
            );
            string edited = Messages(
                Report("first", "FleetArrived"),
                Report("inserted", "BlockadeDetected"),
                Report("last", "MissionReport")
            );

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );
            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));

            Assert.IsTrue(ContentXmlOverrides.IsOverlay(authored));
            CollectionAssert.AreEqual(
                new[] { "first", "inserted", "last" },
                result.Elements().Select(record => record.Element("InstanceID").Value)
            );
            XElement anchor = XElement
                .Parse(authored)
                .Elements()
                .Single(record => (string)record.Element("InstanceID") == "last");
            Assert.AreEqual(1, anchor.Elements().Count());
        }

        [Test]
        public void Merge_RemovedInsertionAnchor_DoesNotCreateAnIncompleteReport()
        {
            string baseline = Messages(
                Report("first", "FleetArrived"),
                Report("last", "MissionReport")
            );
            string edited = Messages(
                Report("first", "FleetArrived"),
                Report("inserted", "BlockadeDetected"),
                Report("last", "MissionReport")
            );
            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );

            XElement result = XElement.Parse(
                ContentXmlOverrides.Merge(Messages(Report("first", "FleetArrived")), authored)
            );

            CollectionAssert.AreEqual(
                new[] { "first", "inserted" },
                result.Elements().Select(record => record.Element("InstanceID").Value)
            );
        }

        [Test]
        public void Update_ReorderedMessages_PreservesExplicitReplacementOrder()
        {
            string baseline = Messages(
                Report("first", "FleetArrived"),
                Report("last", "MissionReport")
            );
            string edited = Messages(
                Report("last", "MissionReport"),
                Report("first", "FleetArrived")
            );

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited,
                ContentXmlOverrides.Create(baseline)
            );

            Assert.IsFalse(ContentXmlOverrides.IsOverlay(authored));
            Assert.IsTrue(XNode.DeepEquals(XElement.Parse(edited), XElement.Parse(authored)));
        }

        [Test]
        public void Merge_AnonymousOverlayAboveIdentifiedMessages_PreservesCompleteReplacement()
        {
            string baseline = Messages(
                Report("first", "FleetArrived"),
                Report("last", "MissionReport")
            );
            string authored =
                "<MessageDefinitions Inherit='true'><MessageDefinition><ResultType>FleetDestroyed</ResultType><Body>Local</Body></MessageDefinition></MessageDefinitions>";

            XElement result = XElement.Parse(ContentXmlOverrides.Merge(baseline, authored));

            Assert.AreEqual(1, result.Elements().Count());
            Assert.AreEqual(
                "FleetDestroyed",
                result.Element("MessageDefinition").Element("ResultType").Value
            );
            Assert.IsFalse(result.Descendants("InstanceID").Any());
        }

        [Test]
        public void Update_MessageLosesItsIdentity_PreservesCompleteReplacement()
        {
            string baseline = Messages(Report("first", "FleetArrived"));
            XElement edited = XElement.Parse(baseline);
            edited.Element("MessageDefinition").Element("InstanceID").Remove();

            string authored = ContentXmlOverrides.Update(
                baseline,
                edited.ToString(),
                ContentXmlOverrides.Create(baseline)
            );

            Assert.IsFalse(ContentXmlOverrides.IsOverlay(authored));
            Assert.IsFalse(XElement.Parse(authored).Descendants("InstanceID").Any());
        }

        [Test]
        public void Load_LegacyMessageReplacement_DoesNotReintroduceOmittedDefinitions()
        {
            string baseline = WriteLayer(
                "Base",
                Messages(Report("first", "FleetArrived"), Report("new", "MissionReport"))
            );
            string authored =
                "<MessageDefinitions><MessageDefinition><ResultType>FleetArrived</ResultType><Body>Local</Body></MessageDefinition></MessageDefinitions>";
            string local = WriteLayer("Local", authored);
            ContentFileResolver resolver = new(
                folder,
                local,
                fallbackPackRootPaths: new[] { baseline }
            );

            string result = ContentXmlOverrides.Load(resolver, "Pack/theme.xml");

            Assert.AreEqual(authored, result);
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

        [Test]
        public void Merge_EmptyIdentifiedCollection_DoesNotCreateRemovedRecords()
        {
            string resolved = ContentXmlOverrides.Merge(
                "<FactionThemes/>",
                "<FactionThemes Inherit='true'><FactionTheme Remove='true'><FactionInstanceID>FNEMP1</FactionInstanceID></FactionTheme></FactionThemes>"
            );
            Assert.IsEmpty(XElement.Parse(resolved).Elements());
        }

        [Test]
        public void Update_EmptyIdentifiedCollection_PreservesRecordRemovalIntent()
        {
            string previous =
                "<FactionThemes Inherit='true'><FactionTheme Remove='true'><FactionInstanceID>FNEMP1</FactionInstanceID></FactionTheme></FactionThemes>";
            string updated = ContentXmlOverrides.Update(
                "<FactionThemes/>",
                "<FactionThemes/>",
                previous
            );
            Assert.AreEqual(
                "true",
                (string)XElement.Parse(updated).Element("FactionTheme")?.Attribute("Remove")
            );
        }

        [Test]
        public void Load_RulesOverlayAboveLegacySparseRules_InheritsDefaultsBeforeApplyingRemovals()
        {
            string inherited = WriteLayer(
                "Base",
                "<GameConfig><Movement><DistanceDivisor>7</DistanceDivisor></Movement></GameConfig>"
            );
            string local = WriteLayer(
                "Local",
                "<GameConfig Inherit='true'><DifficultyModifiers Remove='true'/></GameConfig>"
            );
            ContentFileResolver resolver = new ContentFileResolver(
                folder,
                local,
                fallbackPackRootPaths: new[] { inherited }
            );
            XElement resolved = XElement.Parse(
                ContentXmlOverrides.Load(
                    resolver,
                    "Pack/theme.xml",
                    "<GameConfig><Movement><DistanceDivisor>5</DistanceDivisor></Movement><Research><BaseResearchPoints>1</BaseResearchPoints></Research><DifficultyModifiers/></GameConfig>"
                )
            );
            Assert.AreEqual("7", resolved.Element("Movement").Element("DistanceDivisor").Value);
            Assert.AreEqual("1", resolved.Element("Research").Element("BaseResearchPoints").Value);
            Assert.IsNull(resolved.Element("DifficultyModifiers"));
        }

        [TestCase("Officer", "TypeID")]
        [TestCase("Planet", "InstanceID")]
        [TestCase("FactionTheme", "FactionInstanceID")]
        [TestCase("Entry", "Key")]
        public void CanResetSetting_OverlaySelector_DisallowsIndependentInheritance(
            string record,
            string member
        )
        {
            XElement xml = XElement.Parse(
                "<Records><"
                    + record
                    + "><"
                    + member
                    + ">one</"
                    + member
                    + "></"
                    + record
                    + "></Records>"
            );
            Assert.IsFalse(
                ContentXmlOverrides.CanResetSetting(xml.Element(record).Element(member))
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Reset_OverlaySelector_RejectsRemovingTheSelector(bool attribute)
        {
            XElement xml = XElement.Parse(
                attribute
                    ? "<OfficerTraits><Trait ID='one'><PersonnelTravelTimePercent>80</PersonnelTravelTimePercent></Trait></OfficerTraits>"
                    : "<FactionThemes><FactionTheme><FactionInstanceID>FNEMP1</FactionInstanceID><FactionPrimaryColorHex>blue</FactionPrimaryColorHex></FactionTheme></FactionThemes>"
            );
            XElement member = attribute
                ? xml.Element("Trait")
                : xml.Element("FactionTheme").Element("FactionInstanceID");
            Assert.Throws<InvalidOperationException>(() =>
                ContentXmlOverrides.Reset(xml.ToString(), member, attribute ? "ID" : null)
            );
        }

        [Test]
        public void CanResetSetting_CompositeKeyMember_DisallowsPartialSelectorReset()
        {
            XElement xml = XElement.Parse(
                "<Entries><Entry><Key><PrimaryWeaponType>Turbolaser</PrimaryWeaponType></Key><Value>40</Value></Entry></Entries>"
            );
            Assert.IsFalse(
                ContentXmlOverrides.CanResetSetting(xml.Descendants("PrimaryWeaponType").Single())
            );
        }

        [Test]
        public void Reset_MissingSetting_PreservesTheAuthoredOverlay()
        {
            string overlay = "<FactionThemes Inherit='true'/>";
            Assert.AreEqual(overlay, ContentXmlOverrides.Reset(overlay, null));
        }

        [Test]
        public void IsOverridden_MissingSetting_ReturnsFalse()
        {
            Assert.IsFalse(
                ContentXmlOverrides.IsOverridden("<FactionThemes Inherit='true'/>", null)
            );
        }

        /// <summary>
        /// Builds an identified report with separate editable text and image settings.
        /// </summary>
        /// <param name="id">The stable message identity.</param>
        /// <param name="result">The existing runtime result selector.</param>
        /// <param name="body">The report text.</param>
        /// <returns>The complete report definition.</returns>
        private static XElement Report(string id, string result, string body = "Inherited body") =>
            new(
                "MessageDefinition",
                new XElement("InstanceID", id),
                new XElement("ResultType", result),
                new XElement("Body", body),
                new XElement("BackgroundImage", new XAttribute("Path", "base.png"))
            );

        /// <summary>
        /// Wraps report definitions in their serialized collection.
        /// </summary>
        /// <param name="reports">The definitions in authored lookup order.</param>
        /// <returns>The complete message document.</returns>
        private static string Messages(params XElement[] reports) =>
            new XElement("MessageDefinitions", reports).ToString();

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
