using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Rebellion.Game.Results;
using Rebellion.Multiplayer;

namespace Rebellion.Tactical.Networking
{
    /// <summary>
    /// Encodes complete tactical projections and effects without shared compression baselines.
    /// </summary>
    internal static class MultiplayerTacticalFrameCodec
    {
        internal const int MaxFramePayloadBytes = MultiplayerPacketCodec.MaximumTacticalFrameBytes;
        internal const int MaxEffectsPayloadBytes =
            MultiplayerPacketCodec.MaximumTacticalEffectsBytes;
        private const int _maximumExpandedBytes = 4 * 1024 * 1024;
        private const int _headerBytes = 18;
        private const uint _magic = 0x43543252;
        private const byte _version = 1;
        private static readonly Encoding _utf8 = new UTF8Encoding(false, true);
        private static readonly uint[] _checksums = CreateChecksumTable();

        /// <summary>
        /// Encodes one independently decodable complete projection.
        /// </summary>
        /// <param name="frame">The complete captured projection.</param>
        /// <returns>The versioned compressed binary payload.</returns>
        internal static byte[] Encode(TacticalCombatNetworkFrame frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            return Pack(1, writer => new Encoder(writer).Frame(frame));
        }

        /// <summary>
        /// Decodes one bounded independently compressed projection.
        /// </summary>
        /// <param name="payload">The complete versioned payload.</param>
        /// <returns>The detached tactical projection.</returns>
        internal static TacticalCombatNetworkFrame Decode(byte[] payload)
        {
            using MemoryStream stream = Unpack(payload, 1);
            using BinaryReader reader = new(stream, _utf8, true);
            TacticalCombatNetworkFrame frame = new Decoder(reader).Frame();
            RequireEnd(stream);
            return frame;
        }

        /// <summary>
        /// Encodes an ordered effect batch independently of state projections.
        /// </summary>
        /// <param name="effects">The ordered captured effects.</param>
        /// <returns>The versioned compressed effect batch.</returns>
        internal static byte[] EncodeEffects(TacticalCombatNetworkEffect[] effects)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            return Pack(
                2,
                writer =>
                {
                    Encoder encoder = new(writer);
                    encoder.Array(effects, encoder.Effect, 65536);
                }
            );
        }

        /// <summary>
        /// Decodes one bounded ordered effect batch.
        /// </summary>
        /// <param name="payload">The versioned effect payload.</param>
        /// <returns>The detached ordered effects.</returns>
        internal static TacticalCombatNetworkEffect[] DecodeEffects(byte[] payload)
        {
            using MemoryStream stream = Unpack(payload, 2);
            using BinaryReader reader = new(stream, _utf8, true);
            Decoder decoder = new(reader);
            TacticalCombatNetworkEffect[] effects = decoder.Array(decoder.Effect, 65536);
            RequireEnd(stream);
            return effects;
        }

        /// <summary>
        /// Writes and compresses a closed payload with expansion and integrity bounds.
        /// </summary>
        /// <param name="kind">The closed payload kind.</param>
        /// <param name="write">The explicit schema writer.</param>
        /// <returns>The independently compressed envelope.</returns>
        private static byte[] Pack(byte kind, Action<BinaryWriter> write)
        {
            using MemoryStream raw = new();
            using (BinaryWriter writer = new(raw, _utf8, true))
                write(writer);
            if (raw.Length > _maximumExpandedBytes)
                throw new InvalidDataException("The tactical payload exceeds its expansion limit.");
            byte[] bytes = raw.ToArray();
            using MemoryStream compressed = new();
            using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, true))
                gzip.Write(bytes, 0, bytes.Length);
            if (compressed.Length > MaxFramePayloadBytes - _headerBytes)
                throw new InvalidDataException("The tactical payload exceeds its wire limit.");
            using MemoryStream destination = new();
            using BinaryWriter header = new(destination, _utf8, true);
            header.Write(_magic);
            header.Write(_version);
            header.Write(kind);
            header.Write(bytes.Length);
            header.Write((int)compressed.Length);
            header.Write(Checksum(bytes));
            compressed.Position = 0;
            compressed.CopyTo(destination);
            return destination.ToArray();
        }

        /// <summary>
        /// Validates the envelope before bounded expansion and complete checksum verification.
        /// </summary>
        /// <param name="payload">The complete compressed envelope.</param>
        /// <param name="kind">The required payload kind.</param>
        /// <returns>The verified raw schema stream.</returns>
        private static MemoryStream Unpack(byte[] payload, byte kind)
        {
            if (
                payload == null
                || payload.Length < _headerBytes
                || payload.Length > MaxFramePayloadBytes
            )
                throw new InvalidDataException("The tactical envelope length is invalid.");
            using MemoryStream source = new(payload, false);
            using BinaryReader header = new(source, _utf8, true);
            if (
                header.ReadUInt32() != _magic
                || header.ReadByte() != _version
                || header.ReadByte() != kind
            )
                throw new InvalidDataException(
                    "The tactical envelope version or kind is unsupported."
                );
            int expanded = header.ReadInt32();
            int compressed = header.ReadInt32();
            uint checksum = header.ReadUInt32();
            if (
                expanded < 0
                || expanded > _maximumExpandedBytes
                || compressed <= 0
                || compressed != payload.Length - _headerBytes
            )
                throw new InvalidDataException("The tactical envelope bounds are invalid.");
            byte[] bytes = new byte[expanded];
            using GZipStream gzip = new(source, CompressionMode.Decompress, true);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = gzip.Read(bytes, offset, bytes.Length - offset);
                if (count == 0)
                    throw new InvalidDataException("The tactical payload is truncated.");
                offset += count;
            }
            if (gzip.ReadByte() != -1 || Checksum(bytes) != checksum)
                throw new InvalidDataException(
                    "The tactical payload length or checksum is invalid."
                );
            return new MemoryStream(bytes, false);
        }

        /// <summary>
        /// Rejects trailing unparsed schema bytes.
        /// </summary>
        /// <param name="stream">The decoded schema stream.</param>
        private static void RequireEnd(MemoryStream stream)
        {
            if (stream.Position != stream.Length)
                throw new InvalidDataException("The tactical schema contains trailing bytes.");
        }

        /// <summary>
        /// Builds the fixed byte-indexed checksum table.
        /// </summary>
        /// <returns>The CRC table.</returns>
        private static uint[] CreateChecksumTable()
        {
            uint[] values = new uint[256];
            for (uint index = 0; index < values.Length; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                values[index] = value;
            }
            return values;
        }

        /// <summary>
        /// Checksums all raw schema bytes.
        /// </summary>
        /// <param name="bytes">The raw payload.</param>
        /// <returns>The CRC checksum.</returns>
        private static uint Checksum(byte[] bytes)
        {
            uint value = uint.MaxValue;
            foreach (byte item in bytes)
                value = _checksums[(value ^ item) & 255] ^ (value >> 8);
            return ~value;
        }

        /// <summary>
        /// Writes the closed schema with payload-local exact identity interning.
        /// </summary>
        private sealed class Encoder
        {
            private readonly BinaryWriter _writer;
            private readonly Dictionary<string, int> _identities = new(StringComparer.Ordinal);

            /// <summary>
            /// Attaches the explicit schema writer.
            /// </summary>
            /// <param name="writer">The raw destination.</param>
            internal Encoder(BinaryWriter writer) => _writer = writer;

            /// <summary>
            /// Writes a bounded ordered collection.
            /// </summary>
            /// <typeparam name="T">The closed element type.</typeparam>
            /// <param name="values">The ordered elements.</param>
            /// <param name="write">The explicit element writer.</param>
            /// <param name="maximum">The admitted count limit.</param>
            internal void Array<T>(T[] values, Action<T> write, int maximum)
            {
                if (values == null || values.Length > maximum)
                    throw new InvalidDataException("The tactical collection length is invalid.");
                _writer.Write(values.Length);
                foreach (T value in values)
                {
                    write(value);
                    if (_writer.BaseStream.Position > _maximumExpandedBytes)
                        throw new InvalidDataException(
                            "The tactical payload exceeds its expansion limit."
                        );
                }
            }

            /// <summary>
            /// Writes a nullable identity or an earlier payload-local index.
            /// </summary>
            /// <param name="value">The optional exact identity.</param>
            private void ID(string value)
            {
                if (value == null)
                {
                    _writer.Write(-1);
                    return;
                }
                if (_identities.TryGetValue(value, out int index))
                {
                    _writer.Write(index);
                    return;
                }
                if (_utf8.GetByteCount(value) > 1024 || _identities.Count >= 65536)
                    throw new InvalidDataException("The tactical identity bound is invalid.");
                byte[] bytes = _utf8.GetBytes(value);
                _identities.Add(value, _identities.Count);
                _writer.Write(-2);
                _writer.Write((ushort)bytes.Length);
                _writer.Write(bytes);
            }

            /// <summary>
            /// Writes one frame through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured frame.</param>
            internal void Frame(TacticalCombatNetworkFrame value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                _writer.Write(value.Sequence);
                _writer.Write(value.TimestampMilliseconds);
                _writer.Write(value.SystemTime);
                _writer.Write(value.Paused);
                _writer.Write(value.Complete);
                _writer.Write((int)value.Winner);
                _writer.Write((int)value.AttackerOutcome);
                _writer.Write((int)value.DefenderOutcome);
                Array(value.TerminalFlags, item => _writer.Write(item), 7);
                _writer.Write(value.StationCharge);
                _writer.Write(value.StationDeparted);
                _writer.Write(value.MovieID);
                _writer.Write(value.MovieGroupID);
                _writer.Write((int)value.MovieSide);
                ID(value.MovieUnitID);
                _writer.Write(value.MovieCategory);
                _writer.Write((int)value.MovieState);
                Array(value.Units, item => Unit(item), 4096);
                Array(value.Groups, item => Group(item), 4096);
                Array(value.Sides, item => Side(item), 2);
                Array(value.Selections, item => Selection(item), 2);
                Array(value.Effects, item => Effect(item), 65536);
            }

            /// <summary>
            /// Writes one unit through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured unit.</param>
            internal void Unit(TacticalCombatNetworkUnitState value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                ID(value.UnitID);
                Vector(value.Position);
                Vector(value.Heading);
                Vector(value.Velocity);
                Vector(value.DesiredHeading);
                Vector(value.DesiredMovement);
                _writer.Write((int)value.Status);
                ID(value.CarrierID);
                ID(value.TargetID);
                _writer.Write(value.Hull);
                _writer.Write(value.Shields);
                _writer.Write(value.ShieldLimit);
                _writer.Write(value.AttackDelay);
                _writer.Write(value.MovementDelay);
                _writer.Write(value.WeaponRechargeSample);
                _writer.Write(value.ShieldRechargeSample);
                Array(value.ComponentDamage, item => _writer.Write(item), 5);
                Array(value.Charge, item => _writer.Write(item), 4);
                Array(value.Ready, item => _writer.Write(item), 4);
                _writer.Write(value.HyperspaceMilliseconds);
                _writer.Write(value.TractorRestraint);
                _writer.Write(value.HasTractorSources);
                _writer.Write(value.CommandBudget);
                _writer.Write(value.TurnRate);
                _writer.Write(value.HasCollision);
                _writer.Write(value.HasGroup);
                _writer.Write(value.GroupID);
                _writer.Write(value.HasRequestedGroup);
                _writer.Write(value.RequestedGroupID);
            }

            /// <summary>
            /// Writes one group through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured group.</param>
            internal void Group(TacticalCombatNetworkGroupState value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                _writer.Write(value.GroupID);
                _writer.Write((int)value.Side);
                _writer.Write((int)value.Kind);
                _writer.Write(value.Applied);
                Array(value.Members, item => ID(item), 65536);
                Array(value.RequestedMembers, item => ID(item), 65536);
                ID(value.PrimaryMemberID);
                ID(value.RequestedLeaderID);
                Array(value.Targets, item => ID(item), 65536);
                Array(value.RequestedTargets, item => ID(item), 65536);
                Array(value.ComputedTargets, item => ID(item), 65536);
                Array(value.Waypoints, item => Vector(item), 65536);
                Array(value.RequestedWaypoints, item => Vector(item), 65536);
                _writer.Write((int)value.Order);
                _writer.Write((int)value.ActiveOrder);
                _writer.Write(value.CloseAttack);
                _writer.Write(value.ActiveCloseAttack);
                _writer.Write((int)value.Formation);
                _writer.Write((int)value.RequestedFormation);
                Vector(value.Position);
                Vector(value.Heading);
                Vector(value.Velocity);
                _writer.Write(value.MovementSpeed);
                _writer.Write(value.TurnRate);
                _writer.Write(value.StableUpdates);
                _writer.Write(value.FighterCategory);
                _writer.Write(value.FighterSlot);
                _writer.Write(value.ObjectCategory);
                _writer.Write(value.HasAttackRun);
                _writer.Write((int)value.AttackRunState);
                _writer.Write(value.AttackRunMilliseconds);
            }

            /// <summary>
            /// Writes one side through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured side.</param>
            internal void Side(TacticalCombatNetworkSideState value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                _writer.Write((int)value.Side);
                _writer.Write(value.Automated);
                _writer.Write(value.CommandsSuspended);
                _writer.Write(value.Withdrawing);
                _writer.Write(value.WithdrawalMilliseconds);
                _writer.Write(value.HasWithdrawalPhase);
                _writer.Write((int)value.WithdrawalPhase);
                _writer.Write(value.AttackBudget);
            }

            /// <summary>
            /// Writes one selection through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured selection.</param>
            internal void Selection(TacticalCombatNetworkSelectionState value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                _writer.Write((int)value.Side);
                _writer.Write((int)value.Kind);
                _writer.Write(value.HasGroup);
                _writer.Write(value.GroupID);
                _writer.Write(value.Category);
                _writer.Write(value.HasWaypoint);
                Vector(value.PrimaryWaypoint);
                ID(value.PrimaryID);
                ID(value.InspectedID);
                Array(value.Members, item => ID(item), 65536);
                Array(value.Highlights, item => ID(item), 65536);
                Array(value.Targets, item => ID(item), 65536);
                Array(value.Waypoints, item => Vector(item), 65536);
                Array(value.Groups, item => _writer.Write(item), 65536);
            }

            /// <summary>
            /// Writes one effect through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured effect.</param>
            internal void Effect(TacticalCombatNetworkEffect value)
            {
                if (value == null)
                    throw new InvalidDataException("The tactical record is missing.");
                _writer.Write((int)value.Kind);
                ID(value.SourceID);
                ID(value.TargetID);
                _writer.Write((int)value.Weapon);
                _writer.Write((int)value.Impact);
                _writer.Write(value.Strength);
                _writer.Write(value.Enabled);
                _writer.Write(value.MessageID);
                _writer.Write(value.MovieID);
                _writer.Write(value.HasRecipient);
                _writer.Write((int)value.Recipient);
            }

            /// <summary>
            /// Writes one vector through its explicit field schema.
            /// </summary>
            /// <param name="value">The captured vector.</param>
            internal void Vector(TacticalCombatNetworkVector value)
            {
                _writer.Write(value.X);
                _writer.Write(value.Y);
                _writer.Write(value.Z);
            }
        }

        /// <summary>
        /// Reads the closed schema while bounding collections and identity references.
        /// </summary>
        private sealed class Decoder
        {
            private readonly BinaryReader _reader;
            private readonly List<string> _identities = new();

            /// <summary>
            /// Attaches the explicit schema reader.
            /// </summary>
            /// <param name="reader">The verified raw source.</param>
            internal Decoder(BinaryReader reader) => _reader = reader;

            /// <summary>
            /// Reads a bounded ordered collection.
            /// </summary>
            /// <typeparam name="T">The closed element type.</typeparam>
            /// <param name="read">The explicit element reader.</param>
            /// <param name="maximum">The admitted count limit.</param>
            /// <returns>The detached ordered elements.</returns>
            internal T[] Array<T>(Func<T> read, int maximum)
            {
                int count = _reader.ReadInt32();
                if (
                    count < 0
                    || count > maximum
                    || count > _reader.BaseStream.Length - _reader.BaseStream.Position
                )
                    throw new InvalidDataException("The tactical collection length is invalid.");
                T[] values = new T[count];
                for (int index = 0; index < count; index++)
                    values[index] = read();
                return values;
            }

            /// <summary>
            /// Reads an explicitly encoded Boolean.
            /// </summary>
            /// <returns>The decoded Boolean.</returns>
            private bool Boolean()
            {
                byte value = _reader.ReadByte();
                if (value > 1)
                    throw new InvalidDataException("The tactical Boolean value is invalid.");
                return value != 0;
            }

            /// <summary>
            /// Reads a nullable identity or a validated earlier identity reference.
            /// </summary>
            /// <returns>The optional exact identity.</returns>
            private string ID()
            {
                int index = _reader.ReadInt32();
                if (index == -1)
                    return null;
                if (index >= 0 && index < _identities.Count)
                    return _identities[index];
                if (index != -2 || _identities.Count >= 65536)
                    throw new InvalidDataException("The tactical identity reference is invalid.");
                int length = _reader.ReadUInt16();
                if (
                    length > 1024
                    || length > _reader.BaseStream.Length - _reader.BaseStream.Position
                )
                    throw new InvalidDataException("The tactical identity length is invalid.");
                string value = _utf8.GetString(_reader.ReadBytes(length));
                _identities.Add(value);
                return value;
            }

            /// <summary>
            /// Reads one frame through its explicit field schema.
            /// </summary>
            /// <returns>The detached frame.</returns>
            internal TacticalCombatNetworkFrame Frame() =>
                new()
                {
                    Sequence = _reader.ReadInt64(),
                    TimestampMilliseconds = _reader.ReadDouble(),
                    SystemTime = _reader.ReadInt32(),
                    Paused = Boolean(),
                    Complete = Boolean(),
                    Winner = (CombatSide)_reader.ReadInt32(),
                    AttackerOutcome = (SpaceCombatSideOutcome)_reader.ReadInt32(),
                    DefenderOutcome = (SpaceCombatSideOutcome)_reader.ReadInt32(),
                    TerminalFlags = Array(() => Boolean(), 7),
                    StationCharge = _reader.ReadSingle(),
                    StationDeparted = Boolean(),
                    MovieID = _reader.ReadInt32(),
                    MovieGroupID = _reader.ReadInt32(),
                    MovieSide = (CombatSide)_reader.ReadInt32(),
                    MovieUnitID = ID(),
                    MovieCategory = _reader.ReadInt32(),
                    MovieState = (TacticalCombatAttackRunState)_reader.ReadInt32(),
                    Units = Array(() => Unit(), 4096),
                    Groups = Array(() => Group(), 4096),
                    Sides = Array(() => Side(), 2),
                    Selections = Array(() => Selection(), 2),
                    Effects = Array(() => Effect(), 65536),
                };

            /// <summary>
            /// Reads one unit through its explicit field schema.
            /// </summary>
            /// <returns>The detached unit.</returns>
            internal TacticalCombatNetworkUnitState Unit() =>
                new()
                {
                    UnitID = ID(),
                    Position = Vector(),
                    Heading = Vector(),
                    Velocity = Vector(),
                    DesiredHeading = Vector(),
                    DesiredMovement = Vector(),
                    Status = (TacticalCombatUnitStatus)_reader.ReadInt32(),
                    CarrierID = ID(),
                    TargetID = ID(),
                    Hull = _reader.ReadSingle(),
                    Shields = _reader.ReadSingle(),
                    ShieldLimit = _reader.ReadSingle(),
                    AttackDelay = _reader.ReadSingle(),
                    MovementDelay = _reader.ReadSingle(),
                    WeaponRechargeSample = _reader.ReadSingle(),
                    ShieldRechargeSample = _reader.ReadSingle(),
                    ComponentDamage = Array(() => _reader.ReadUInt32(), 5),
                    Charge = Array(() => _reader.ReadSingle(), 4),
                    Ready = Array(() => Boolean(), 4),
                    HyperspaceMilliseconds = _reader.ReadSingle(),
                    TractorRestraint = _reader.ReadSingle(),
                    HasTractorSources = Boolean(),
                    CommandBudget = _reader.ReadSingle(),
                    TurnRate = _reader.ReadSingle(),
                    HasCollision = Boolean(),
                    HasGroup = Boolean(),
                    GroupID = _reader.ReadInt32(),
                    HasRequestedGroup = Boolean(),
                    RequestedGroupID = _reader.ReadInt32(),
                };

            /// <summary>
            /// Reads one group through its explicit field schema.
            /// </summary>
            /// <returns>The detached group.</returns>
            internal TacticalCombatNetworkGroupState Group() =>
                new()
                {
                    GroupID = _reader.ReadInt32(),
                    Side = (CombatSide)_reader.ReadInt32(),
                    Kind = (TacticalCombatGroupKind)_reader.ReadInt32(),
                    Applied = Boolean(),
                    Members = Array(() => ID(), 65536),
                    RequestedMembers = Array(() => ID(), 65536),
                    PrimaryMemberID = ID(),
                    RequestedLeaderID = ID(),
                    Targets = Array(() => ID(), 65536),
                    RequestedTargets = Array(() => ID(), 65536),
                    ComputedTargets = Array(() => ID(), 65536),
                    Waypoints = Array(() => Vector(), 65536),
                    RequestedWaypoints = Array(() => Vector(), 65536),
                    Order = (TacticalCombatOrder)_reader.ReadInt32(),
                    ActiveOrder = (TacticalCombatOrder)_reader.ReadInt32(),
                    CloseAttack = Boolean(),
                    ActiveCloseAttack = Boolean(),
                    Formation = (TacticalCombatFormationKind)_reader.ReadInt32(),
                    RequestedFormation = (TacticalCombatFormationKind)_reader.ReadInt32(),
                    Position = Vector(),
                    Heading = Vector(),
                    Velocity = Vector(),
                    MovementSpeed = _reader.ReadSingle(),
                    TurnRate = _reader.ReadSingle(),
                    StableUpdates = _reader.ReadInt32(),
                    FighterCategory = _reader.ReadInt32(),
                    FighterSlot = _reader.ReadInt32(),
                    ObjectCategory = _reader.ReadInt32(),
                    HasAttackRun = Boolean(),
                    AttackRunState = (TacticalCombatAttackRunState)_reader.ReadInt32(),
                    AttackRunMilliseconds = _reader.ReadSingle(),
                };

            /// <summary>
            /// Reads one side through its explicit field schema.
            /// </summary>
            /// <returns>The detached side.</returns>
            internal TacticalCombatNetworkSideState Side() =>
                new()
                {
                    Side = (CombatSide)_reader.ReadInt32(),
                    Automated = Boolean(),
                    CommandsSuspended = Boolean(),
                    Withdrawing = Boolean(),
                    WithdrawalMilliseconds = _reader.ReadSingle(),
                    HasWithdrawalPhase = Boolean(),
                    WithdrawalPhase = (TacticalCombatWithdrawalPhase)_reader.ReadInt32(),
                    AttackBudget = _reader.ReadSingle(),
                };

            /// <summary>
            /// Reads one selection through its explicit field schema.
            /// </summary>
            /// <returns>The detached selection.</returns>
            internal TacticalCombatNetworkSelectionState Selection() =>
                new()
                {
                    Side = (CombatSide)_reader.ReadInt32(),
                    Kind = (TacticalCombatSelectionKind)_reader.ReadInt32(),
                    HasGroup = Boolean(),
                    GroupID = _reader.ReadInt32(),
                    Category = _reader.ReadInt32(),
                    HasWaypoint = Boolean(),
                    PrimaryWaypoint = Vector(),
                    PrimaryID = ID(),
                    InspectedID = ID(),
                    Members = Array(() => ID(), 65536),
                    Highlights = Array(() => ID(), 65536),
                    Targets = Array(() => ID(), 65536),
                    Waypoints = Array(() => Vector(), 65536),
                    Groups = Array(() => _reader.ReadInt32(), 65536),
                };

            /// <summary>
            /// Reads one effect through its explicit field schema.
            /// </summary>
            /// <returns>The detached effect.</returns>
            internal TacticalCombatNetworkEffect Effect() =>
                new()
                {
                    Kind = (TacticalCombatNetworkEffectKind)_reader.ReadInt32(),
                    SourceID = ID(),
                    TargetID = ID(),
                    Weapon = (TacticalWeaponType)_reader.ReadInt32(),
                    Impact = (TacticalCombatImpactKind)_reader.ReadInt32(),
                    Strength = _reader.ReadSingle(),
                    Enabled = Boolean(),
                    MessageID = _reader.ReadInt32(),
                    MovieID = _reader.ReadInt32(),
                    HasRecipient = Boolean(),
                    Recipient = (CombatSide)_reader.ReadInt32(),
                };

            /// <summary>
            /// Reads one vector through its explicit field schema.
            /// </summary>
            /// <returns>The detached vector.</returns>
            internal TacticalCombatNetworkVector Vector() =>
                new()
                {
                    X = _reader.ReadSingle(),
                    Y = _reader.ReadSingle(),
                    Z = _reader.ReadSingle(),
                };
        }
    }
}
