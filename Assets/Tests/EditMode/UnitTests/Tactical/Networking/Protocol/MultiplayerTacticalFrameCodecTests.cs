using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Tactical;
using Rebellion.Tactical.Networking;
using Encoding = global::System.Text.Encoding;
using JsonUtility = global::UnityEngine.JsonUtility;

namespace Rebellion.Tests.Tactical.Networking
{
    public sealed class MultiplayerTacticalFrameCodecTests
    {
        [Test]
        public void Encode_CompleteFrame_RoundTripsEveryFieldAndNullableIdentity()
        {
            TacticalCombatNetworkFrame original = CreateFrame();
            TacticalCombatNetworkFrame decoded = MultiplayerTacticalFrameCodec.Decode(
                MultiplayerTacticalFrameCodec.Encode(original)
            );
            Assert.That(JsonUtility.ToJson(decoded), Is.EqualTo(JsonUtility.ToJson(original)));
            Assert.That(decoded.TimestampMilliseconds, Is.EqualTo(original.TimestampMilliseconds));
            Assert.That(
                decoded.Units[0].ComponentDamage,
                Is.EqualTo(original.Units[0].ComponentDamage)
            );
            Assert.That(decoded.Groups[0].Members, Is.EqualTo(original.Groups[0].Members));
            Assert.That(decoded.Groups[0].Members[1], Is.Null);
            Assert.That(decoded.Groups[0].Members[2], Is.Empty);
        }

        [Test]
        public void Encode_FloatBitPatterns_PreservesNegativeZeroInfinityAndNanPayload()
        {
            TacticalCombatNetworkFrame original = CreateFrame();
            original.Units[0].Charge = new[]
            {
                BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0),
                float.PositiveInfinity,
                float.NegativeInfinity,
                BitConverter.ToSingle(BitConverter.GetBytes(0x7fc01234), 0),
            };
            TacticalCombatNetworkFrame decoded = MultiplayerTacticalFrameCodec.Decode(
                MultiplayerTacticalFrameCodec.Encode(original)
            );
            for (int index = 0; index < 4; index++)
                Assert.That(
                    BitConverter.GetBytes(decoded.Units[0].Charge[index]),
                    Is.EqualTo(BitConverter.GetBytes(original.Units[0].Charge[index]))
                );
        }

        [Test]
        public void Encode_IndependentLaterSnapshot_DecodesWithoutEarlierPayload()
        {
            TacticalCombatNetworkFrame first = CreateFrame();
            MultiplayerTacticalFrameCodec.Encode(first);
            TacticalCombatNetworkFrame later = CreateFrame();
            later.Sequence++;
            later.Units[0].Position.X += 12.5f;
            TacticalCombatNetworkFrame decoded = MultiplayerTacticalFrameCodec.Decode(
                MultiplayerTacticalFrameCodec.Encode(later)
            );
            Assert.That(decoded.Sequence, Is.EqualTo(later.Sequence));
            Assert.That(decoded.Units[0].Position.X, Is.EqualTo(later.Units[0].Position.X));
        }

        [Test]
        public void Encode_MinimalFrame_RetainsEmptyCollections()
        {
            TacticalCombatNetworkFrame decoded = MultiplayerTacticalFrameCodec.Decode(
                MultiplayerTacticalFrameCodec.Encode(new TacticalCombatNetworkFrame())
            );
            Assert.That(decoded.Units, Is.Empty);
            Assert.That(decoded.Groups, Is.Empty);
            Assert.That(decoded.Effects, Is.Empty);
        }

        [Test]
        public void EncodeEffects_AllKinds_PreservesOrderedEffectFields()
        {
            TacticalCombatNetworkEffect[] effects = Enum.GetValues(
                    typeof(TacticalCombatNetworkEffectKind)
                )
                .Cast<TacticalCombatNetworkEffectKind>()
                .Select(kind =>
                {
                    TacticalCombatNetworkEffect effect = CreateEffect();
                    effect.Kind = kind;
                    return effect;
                })
                .ToArray();
            TacticalCombatNetworkEffect[] decoded = MultiplayerTacticalFrameCodec.DecodeEffects(
                MultiplayerTacticalFrameCodec.EncodeEffects(effects)
            );
            Assert.That(decoded.Length, Is.EqualTo(effects.Length));
            for (int index = 0; index < effects.Length; index++)
                Assert.That(
                    JsonUtility.ToJson(decoded[index]),
                    Is.EqualTo(JsonUtility.ToJson(effects[index]))
                );
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(14)]
        public void Decode_ChangedMagicVersionKindOrChecksum_RejectsPayload(int offset)
        {
            byte[] payload = MultiplayerTacticalFrameCodec.Encode(CreateFrame());
            payload[offset] ^= 0x7f;
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.Decode(payload)
            );
        }

        [Test]
        public void Decode_TruncatedOrTrailingEnvelope_RejectsPayload()
        {
            byte[] payload = MultiplayerTacticalFrameCodec.Encode(CreateFrame());
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.Decode(payload.Take(payload.Length - 1).ToArray())
            );
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.Decode(payload.Concat(new byte[] { 0 }).ToArray())
            );
        }

        [Test]
        public void Decode_ExcessiveExpandedLength_RejectsBeforeExpansion()
        {
            byte[] payload = MultiplayerTacticalFrameCodec.Encode(CreateFrame());
            Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue), 0, payload, 6, 4);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.Decode(payload)
            );
        }

        [Test]
        public void Decode_UnderreportedExpandedLength_RejectsAdditionalExpansion()
        {
            byte[] payload = MultiplayerTacticalFrameCodec.Encode(CreateFrame());
            Array.Clear(payload, 6, 4);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.Decode(payload)
            );
        }

        [Test]
        public void DecodeEffects_FramePayload_RejectsWrongSchema()
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.DecodeEffects(
                    MultiplayerTacticalFrameCodec.Encode(CreateFrame())
                )
            );
        }

        [Test]
        public void Encode_ExcessiveRosterOrIdentity_RejectsUnboundedInput()
        {
            TacticalCombatNetworkFrame frame = CreateFrame();
            frame.Units = new TacticalCombatNetworkUnitState[4097];
            Assert.Throws<InvalidDataException>(() => MultiplayerTacticalFrameCodec.Encode(frame));
            frame = CreateFrame();
            frame.Units[0].UnitID = new string('x', 1025);
            Assert.Throws<InvalidDataException>(() => MultiplayerTacticalFrameCodec.Encode(frame));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DecodeEffects_InvalidCollectionCountOrIdentityReference_RejectsMalformedSchema(
            bool invalidCount
        )
        {
            using MemoryStream raw = new();
            using (BinaryWriter writer = new(raw, Encoding.UTF8, true))
            {
                writer.Write(invalidCount ? int.MaxValue : 1);
                if (!invalidCount)
                {
                    writer.Write((int)TacticalCombatNetworkEffectKind.Shot);
                    writer.Write(0);
                }
            }
            byte[] payload = EffectsEnvelope(raw.ToArray());
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalFrameCodec.DecodeEffects(payload)
            );
        }

        /// <summary>
        /// Builds a complete checked effects envelope containing deliberately malformed schema bytes.
        /// </summary>
        /// <param name="raw">The malformed uncompressed schema.</param>
        /// <returns>The versioned independently compressed envelope.</returns>
        private static byte[] EffectsEnvelope(byte[] raw)
        {
            using MemoryStream compressed = new();
            using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, true))
                gzip.Write(raw, 0, raw.Length);
            uint checksum = uint.MaxValue;
            foreach (byte value in raw)
            {
                checksum ^= value;
                for (int bit = 0; bit < 8; bit++)
                    checksum = (checksum & 1) != 0 ? 0xedb88320u ^ (checksum >> 1) : checksum >> 1;
            }
            using MemoryStream envelope = new();
            using BinaryWriter writer = new(envelope);
            writer.Write(0x43543252u);
            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write(raw.Length);
            writer.Write((int)compressed.Length);
            writer.Write(~checksum);
            writer.Write(compressed.ToArray());
            return envelope.ToArray();
        }

        /// <summary>
        /// Creates a fully populated frame wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkFrame CreateFrame() =>
            new()
            {
                Sequence = long.MaxValue - 4,
                TimestampMilliseconds = 123456789.125,
                SystemTime = 3,
                Paused = true,
                Complete = false,
                Winner = (CombatSide)6,
                AttackerOutcome = (SpaceCombatSideOutcome)7,
                DefenderOutcome = (SpaceCombatSideOutcome)8,
                TerminalFlags = new[] { true, false, true, false, true, false, true },
                StationCharge = 10.125f,
                StationDeparted = false,
                MovieID = 12,
                MovieGroupID = 13,
                MovieSide = (CombatSide)14,
                MovieUnitID = "\u96ea-unit",
                MovieCategory = 16,
                MovieState = (TacticalCombatAttackRunState)17,
                Units = new[] { CreateUnit() },
                Groups = new[] { CreateGroup() },
                Sides = new[] { CreateSide() },
                Selections = new[] { CreateSelection() },
                Effects = new[] { CreateEffect() },
            };

        /// <summary>
        /// Creates a fully populated unit wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkUnitState CreateUnit() =>
            new()
            {
                UnitID = "\u96ea-unit",
                Position = CreateVector(),
                Heading = CreateVector(),
                Velocity = CreateVector(),
                DesiredHeading = CreateVector(),
                DesiredMovement = CreateVector(),
                Status = (TacticalCombatUnitStatus)7,
                CarrierID = "\u96ea-unit",
                TargetID = "\u96ea-unit",
                Hull = 10.125f,
                Shields = 11.125f,
                ShieldLimit = 12.125f,
                AttackDelay = 13.125f,
                MovementDelay = 14.125f,
                WeaponRechargeSample = 15.125f,
                ShieldRechargeSample = 16.125f,
                ComponentDamage = new[] { 0u, uint.MaxValue, 1u, 2u, 3u },
                Charge = new[] { 0.125f, 1.25f, 10.5f, 100.75f },
                Ready = new[] { true, false, true, false },
                HyperspaceMilliseconds = 20.125f,
                TractorRestraint = 21.125f,
                HasTractorSources = true,
                CommandBudget = 23.125f,
                TurnRate = 24.125f,
                HasCollision = false,
                HasGroup = true,
                GroupID = 27,
                HasRequestedGroup = true,
                RequestedGroupID = 29,
            };

        /// <summary>
        /// Creates a fully populated group wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkGroupState CreateGroup() =>
            new()
            {
                GroupID = 1,
                Side = (CombatSide)2,
                Kind = (TacticalCombatGroupKind)3,
                Applied = true,
                Members = new[] { "\u96ea-unit", null, string.Empty, "other-unit", "\u96ea-unit" },
                RequestedMembers = new[]
                {
                    "\u96ea-unit",
                    null,
                    string.Empty,
                    "other-unit",
                    "\u96ea-unit",
                },
                PrimaryMemberID = "\u96ea-unit",
                RequestedLeaderID = "\u96ea-unit",
                Targets = new[] { "\u96ea-unit", null, string.Empty, "other-unit", "\u96ea-unit" },
                RequestedTargets = new[]
                {
                    "\u96ea-unit",
                    null,
                    string.Empty,
                    "other-unit",
                    "\u96ea-unit",
                },
                ComputedTargets = new[]
                {
                    "\u96ea-unit",
                    null,
                    string.Empty,
                    "other-unit",
                    "\u96ea-unit",
                },
                Waypoints = new[] { CreateVector() },
                RequestedWaypoints = new[] { CreateVector() },
                Order = (TacticalCombatOrder)14,
                ActiveOrder = (TacticalCombatOrder)15,
                CloseAttack = true,
                ActiveCloseAttack = false,
                Formation = (TacticalCombatFormationKind)18,
                RequestedFormation = (TacticalCombatFormationKind)19,
                Position = CreateVector(),
                Heading = CreateVector(),
                Velocity = CreateVector(),
                MovementSpeed = 23.125f,
                TurnRate = 24.125f,
                StableUpdates = 25,
                FighterCategory = 26,
                FighterSlot = 27,
                ObjectCategory = 28,
                HasAttackRun = false,
                AttackRunState = (TacticalCombatAttackRunState)30,
                AttackRunMilliseconds = 31.125f,
            };

        /// <summary>
        /// Creates a fully populated side wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkSideState CreateSide() =>
            new()
            {
                Side = (CombatSide)1,
                Automated = true,
                CommandsSuspended = false,
                Withdrawing = true,
                WithdrawalMilliseconds = 5.125f,
                HasWithdrawalPhase = true,
                WithdrawalPhase = (TacticalCombatWithdrawalPhase)7,
                AttackBudget = 8.125f,
            };

        /// <summary>
        /// Creates a fully populated selection wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkSelectionState CreateSelection() =>
            new()
            {
                Side = (CombatSide)1,
                Kind = (TacticalCombatSelectionKind)2,
                HasGroup = false,
                GroupID = 4,
                Category = 5,
                HasWaypoint = true,
                PrimaryWaypoint = CreateVector(),
                PrimaryID = "\u96ea-unit",
                InspectedID = "\u96ea-unit",
                Members = new[] { "\u96ea-unit", null, string.Empty, "other-unit", "\u96ea-unit" },
                Highlights = new[]
                {
                    "\u96ea-unit",
                    null,
                    string.Empty,
                    "other-unit",
                    "\u96ea-unit",
                },
                Targets = new[] { "\u96ea-unit", null, string.Empty, "other-unit", "\u96ea-unit" },
                Waypoints = new[] { CreateVector() },
                Groups = new[] { 1, 2, 3 },
            };

        /// <summary>
        /// Creates a fully populated effect wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkEffect CreateEffect() =>
            new()
            {
                Kind = (TacticalCombatNetworkEffectKind)1,
                SourceID = "\u96ea-unit",
                TargetID = "\u96ea-unit",
                Weapon = (TacticalWeaponType)4,
                Impact = (TacticalCombatImpactKind)5,
                Strength = 6.125f,
                Enabled = false,
                MessageID = 8,
                MovieID = 9,
                HasRecipient = true,
                Recipient = (CombatSide)11,
            };

        /// <summary>
        /// Creates a fully populated vector wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatNetworkVector CreateVector() =>
            new()
            {
                X = 1.125f,
                Y = 2.125f,
                Z = 3.125f,
            };
    }
}
