using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Multiplayer.Tactical;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;
using Encoding = global::System.Text.Encoding;
using JsonUtility = global::UnityEngine.JsonUtility;

namespace Rebellion.Tests.Multiplayer.Tactical
{
    public sealed class MultiplayerTacticalSnapshotCodecTests
    {
        [Test]
        public void Encode_CompleteSnapshot_RoundTripsEveryFieldAndNullableIdentity()
        {
            TacticalCombatSnapshot original = CreateSnapshot();
            TacticalCombatSnapshot decoded = MultiplayerTacticalSnapshotCodec.Decode(
                MultiplayerTacticalSnapshotCodec.Encode(original)
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
            TacticalCombatSnapshot original = CreateSnapshot();
            original.Units[0].Charge = new[]
            {
                BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0),
                float.PositiveInfinity,
                float.NegativeInfinity,
                BitConverter.ToSingle(BitConverter.GetBytes(0x7fc01234), 0),
            };
            TacticalCombatSnapshot decoded = MultiplayerTacticalSnapshotCodec.Decode(
                MultiplayerTacticalSnapshotCodec.Encode(original)
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
            TacticalCombatSnapshot first = CreateSnapshot();
            MultiplayerTacticalSnapshotCodec.Encode(first);
            TacticalCombatSnapshot later = CreateSnapshot();
            later.Sequence++;
            later.Units[0].Position.X += 12.5f;
            TacticalCombatSnapshot decoded = MultiplayerTacticalSnapshotCodec.Decode(
                MultiplayerTacticalSnapshotCodec.Encode(later)
            );
            Assert.That(decoded.Sequence, Is.EqualTo(later.Sequence));
            Assert.That(decoded.Units[0].Position.X, Is.EqualTo(later.Units[0].Position.X));
        }

        [Test]
        public void Encode_MinimalSnapshot_RetainsEmptyCollections()
        {
            TacticalCombatSnapshot decoded = MultiplayerTacticalSnapshotCodec.Decode(
                MultiplayerTacticalSnapshotCodec.Encode(new TacticalCombatSnapshot())
            );
            Assert.That(decoded.Units, Is.Empty);
            Assert.That(decoded.Groups, Is.Empty);
            Assert.That(decoded.Effects, Is.Empty);
        }

        [Test]
        public void EncodeEffects_AllKinds_PreservesOrderedEffectFields()
        {
            TacticalCombatEvent[] effects = Enum.GetValues(typeof(TacticalCombatEventKind))
                .Cast<TacticalCombatEventKind>()
                .Select(kind =>
                {
                    TacticalCombatEvent effect = CreateEffect();
                    effect.Kind = kind;
                    return effect;
                })
                .ToArray();
            TacticalCombatEvent[] decoded = MultiplayerTacticalSnapshotCodec.DecodeEffects(
                MultiplayerTacticalSnapshotCodec.EncodeEffects(effects)
            );
            Assert.That(decoded.Length, Is.EqualTo(effects.Length));
            for (int index = 0; index < effects.Length; index++)
                Assert.That(
                    JsonUtility.ToJson(decoded[index]),
                    Is.EqualTo(JsonUtility.ToJson(effects[index]))
                );
        }

        [TestCase(HardpointWeaponType.LaserCannon, 0)]
        [TestCase(HardpointWeaponType.IonCannon, 1)]
        [TestCase(HardpointWeaponType.Turbolaser, 2)]
        [TestCase(HardpointWeaponType.TorpedoLauncher, 3)]
        public void EncodeEffects_ProjectileType_PreservesEstablishedWireCode(
            HardpointWeaponType weapon,
            int expectedCode
        )
        {
            TacticalCombatEvent fired = new()
            {
                Kind = TacticalCombatEventKind.Shot,
                Weapon = weapon,
            };
            byte[] payload = MultiplayerTacticalSnapshotCodec.EncodeEffects(new[] { fired });
            using MemoryStream envelope = new(payload);
            envelope.Position = 18;
            using GZipStream content = new(envelope, CompressionMode.Decompress);
            using BinaryReader reader = new(content);

            Assert.AreEqual(1, reader.ReadInt32());
            Assert.AreEqual((int)TacticalCombatEventKind.Shot, reader.ReadInt32());
            Assert.AreEqual(-1, reader.ReadInt32());
            Assert.AreEqual(-1, reader.ReadInt32());
            Assert.AreEqual(expectedCode, reader.ReadInt32());
            Assert.AreEqual(
                weapon,
                MultiplayerTacticalSnapshotCodec.DecodeEffects(payload).Single().Weapon
            );
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(14)]
        public void Decode_ChangedMagicVersionKindOrChecksum_RejectsPayload(int offset)
        {
            byte[] payload = MultiplayerTacticalSnapshotCodec.Encode(CreateSnapshot());
            payload[offset] ^= 0x7f;
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Decode(payload)
            );
        }

        [Test]
        public void Decode_TruncatedOrTrailingEnvelope_RejectsPayload()
        {
            byte[] payload = MultiplayerTacticalSnapshotCodec.Encode(CreateSnapshot());
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Decode(payload.Take(payload.Length - 1).ToArray())
            );
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Decode(payload.Concat(new byte[] { 0 }).ToArray())
            );
        }

        [Test]
        public void Decode_ExcessiveExpandedLength_RejectsBeforeExpansion()
        {
            byte[] payload = MultiplayerTacticalSnapshotCodec.Encode(CreateSnapshot());
            Buffer.BlockCopy(BitConverter.GetBytes(int.MaxValue), 0, payload, 6, 4);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Decode(payload)
            );
        }

        [Test]
        public void Decode_UnderreportedExpandedLength_RejectsAdditionalExpansion()
        {
            byte[] payload = MultiplayerTacticalSnapshotCodec.Encode(CreateSnapshot());
            Array.Clear(payload, 6, 4);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Decode(payload)
            );
        }

        [Test]
        public void DecodeEffects_SnapshotPayload_RejectsWrongSchema()
        {
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.DecodeEffects(
                    MultiplayerTacticalSnapshotCodec.Encode(CreateSnapshot())
                )
            );
        }

        [Test]
        public void Encode_ExcessiveRosterOrIdentity_RejectsUnboundedInput()
        {
            TacticalCombatSnapshot snapshot = CreateSnapshot();
            snapshot.Units = new TacticalCombatUnitSnapshot[4097];
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Encode(snapshot)
            );
            snapshot = CreateSnapshot();
            snapshot.Units[0].UnitID = new string('x', 1025);
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.Encode(snapshot)
            );
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
                    writer.Write((int)TacticalCombatEventKind.Shot);
                    writer.Write(0);
                }
            }
            byte[] payload = EffectsEnvelope(raw.ToArray());
            Assert.Throws<InvalidDataException>(() =>
                MultiplayerTacticalSnapshotCodec.DecodeEffects(payload)
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
            byte[] emptyEffects = MultiplayerTacticalSnapshotCodec.EncodeEffects(
                Array.Empty<TacticalCombatEvent>()
            );
            writer.Write(emptyEffects[4]);
            writer.Write((byte)2);
            writer.Write(raw.Length);
            writer.Write((int)compressed.Length);
            writer.Write(~checksum);
            writer.Write(compressed.ToArray());
            return envelope.ToArray();
        }

        /// <summary>
        /// Creates a fully populated snapshot wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatSnapshot CreateSnapshot() =>
            new()
            {
                Sequence = long.MaxValue - 4,
                TimestampMilliseconds = 123456789.125,
                SystemTime = 3,
                Paused = true,
                Complete = false,
                Winner = (SpaceCombatResult.BattleOutcome)6,
                AttackerOutcome = (SpaceCombatResult.SideOutcome)7,
                DefenderOutcome = (SpaceCombatResult.SideOutcome)8,
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
        private static TacticalCombatUnitSnapshot CreateUnit() =>
            new()
            {
                UnitID = "\u96ea-unit",
                Position = CreateVector(),
                Heading = CreateVector(),
                Velocity = CreateVector(),
                DesiredHeading = CreateVector(),
                DesiredMovement = CreateVector(),
                Status = (CombatUnitStatus)7,
                CarrierID = "\u96ea-unit",
                Hull = 10.125f,
                Shields = 11.125f,
                ShieldLimit = 12.125f,
                AttackDelay = 13.125f,
                MovementDelay = 14.125f,
                WeaponRechargeSample = 15.125f,
                ShieldRechargeSample = 16.125f,
                ComponentDamage = new[] { 0u, uint.MaxValue, 1u, 2u, 3u },
                HyperdriveDamage = new[] { 1, 0, 1, 1 },
                Charge = new[] { 0.125f, 1.25f, 10.5f, 100.75f },
                Ready = new[] { true, false, true, false },
                HyperspaceMilliseconds = 20.125f,
                TractorRestraint = 21.125f,
                HasTractorSources = true,
                CommandBudget = 23.125f,
                TurnRate = 24.125f,
                HasCollision = false,
            };

        /// <summary>
        /// Creates a fully populated group wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatGroupSnapshot CreateGroup() =>
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
        private static TacticalCombatSideSnapshot CreateSide() =>
            new()
            {
                Side = (CombatSide)1,
                Automated = true,
                CommandsSuspended = false,
                Withdrawing = true,
                WithdrawalMilliseconds = 5.125f,
                HasWithdrawalPhase = true,
                WithdrawalPhase = (TacticalCombatSide.DeparturePhase)7,
                AttackBudget = 8.125f,
            };

        /// <summary>
        /// Creates a fully populated selection wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatSelectionSnapshot CreateSelection() =>
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
                Targets = new[] { "\u96ea-unit", null, string.Empty, "other-unit", "\u96ea-unit" },
                Waypoints = new[] { CreateVector() },
                Groups = new[] { 1, 2, 3 },
            };

        /// <summary>
        /// Creates a fully populated effect wire fixture.
        /// </summary>
        /// <returns>The detached fixture.</returns>
        private static TacticalCombatEvent CreateEffect() =>
            new()
            {
                Kind = (TacticalCombatEventKind)1,
                SourceID = "\u96ea-unit",
                TargetID = "\u96ea-unit",
                Weapon = HardpointWeaponType.TorpedoLauncher,
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
        private static TacticalCombatSnapshotVector CreateVector() =>
            new()
            {
                X = 1.125f,
                Y = 2.125f,
                Z = 3.125f,
            };
    }
}
