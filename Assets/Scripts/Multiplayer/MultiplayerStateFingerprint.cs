using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Serialization;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Checks persisted state with cached serializer member plans and direct scalar hashing.
    /// It performs one bounded traversal at the existing network cadence, with no XML or compression.
    /// </summary>
    internal static class MultiplayerStateFingerprint
    {
        private static readonly ConcurrentDictionary<Type, TypePlan> _plans = new();
        private static readonly ConcurrentDictionary<Type, TypePlan> _simulationPlans = new();
        private static readonly ConditionalWeakTable<string, StringDigest> _strings = new();
        private static readonly Func<Type, TypePlan> _createPlan = CreatePlan;
        private static readonly AccessorFactory _accessors = new();
        private static readonly ConditionalWeakTable<
            string,
            StringDigest
        >.CreateValueCallback _digestString = DigestString;

        /// <summary>
        /// Computes a local change-detection fingerprint using the serializer's exact member selection.
        /// </summary>
        /// <param name="value">The stable persisted root to inspect.</param>
        /// <returns>The local fingerprint; transmitted snapshots retain SHA-256 validation.</returns>
        public static string Compute(object value)
        {
            Accumulator accumulator = new Accumulator();
            Visit(value, ref accumulator, new HashContext(_plans, _createPlan));
            return accumulator.First.ToString("X16") + accumulator.Second.ToString("X16");
        }

        /// <summary>
        /// Verifies the model and continuation facts while excluding participant-owned UI state.
        /// This traversal belongs to a settled network barrier, never to a rendered-frame poll.
        /// </summary>
        /// <param name="session">The session at its admitted verification boundary.</param>
        /// <returns>The simulation continuation fingerprint.</returns>
        public static string ComputeSimulation(GameSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            VerificationState state = new()
            {
                Continuation = session.CaptureRecoveryState(),
                FactionRules = session
                    .Game.GetFactions()
                    .Select(faction => faction.Settings)
                    .ToArray(),
            };
            Accumulator accumulator = new();
            Visit(state, ref accumulator, new HashContext(_simulationPlans, CreateSimulationPlan));
            return accumulator.First.ToString("X16") + accumulator.Second.ToString("X16");
        }

        /// <summary>
        /// Admits a restored runtime only when its actual continuation matches the source boundary.
        /// </summary>
        /// <param name="session">The restored session after local presentation is reapplied.</param>
        /// <param name="expected">The source simulation fingerprint.</param>
        /// <returns>The independently computed continuation fingerprint.</returns>
        public static string VerifyRecovery(GameSession session, string expected)
        {
            string actual = ComputeSimulation(session);
            if (
                string.IsNullOrEmpty(expected)
                || !string.Equals(actual, expected, StringComparison.Ordinal)
            )
                throw new InvalidOperationException(
                    "The restored simulation does not match the host state."
                );
            return actual;
        }

        /// <summary>
        /// Omits only the UI state explicitly owned by an individual participant.
        /// Durable message actions and shared player settings remain part of verification.
        /// </summary>
        /// <param name="type">The model type being inspected.</param>
        /// <returns>The simulation-specific cached member plan.</returns>
        private static TypePlan CreateSimulationPlan(Type type)
        {
            TypePlan plan =
                typeof(GameResult).IsAssignableFrom(type) || type == typeof(SpaceCombatDecision)
                    ? CreateObjectPlan(type)
                    : CreatePlan(type);
            if (typeof(BaseSceneNode).IsAssignableFrom(type))
                return new TypePlan(
                    type,
                    plan.Scalar,
                    plan.Code,
                    plan.Members.Concat(
                            new[]
                            {
                                typeof(BaseSceneNode).GetProperty(nameof(BaseSceneNode.ParentNode)),
                                typeof(BaseSceneNode).GetProperty(
                                    nameof(BaseSceneNode.LastParentNode)
                                ),
                            }
                        )
                        .ToArray(),
                    plan.InlineCollection
                );
            return type == typeof(Player)
                ? new TypePlan(
                    type,
                    plan.Scalar,
                    plan.Code,
                    plan.Members.Where(member => member.Name != nameof(Player.UIState)).ToArray(),
                    plan.InlineCollection
                )
                : plan;
        }

        /// <summary>
        /// Visits a value through the same scalar, point, dictionary, collection, and persistable boundaries as XML.
        /// </summary>
        /// <param name="value">The current persisted value.</param>
        /// <param name="hash">The current local accumulator.</param>
        /// <param name="context">The member policy and actual references owned by this operation.</param>
        /// <param name="knownPlan">The declared scalar member plan, when its boxed type is fixed.</param>
        private static void Visit(
            object value,
            ref Accumulator hash,
            HashContext context,
            TypePlan knownPlan = null
        )
        {
            if (value == null)
            {
                hash.Add(0);
                return;
            }
            TypePlan plan = knownPlan ?? context.GetPlan(value.GetType());
            hash.Add(plan.TypeName.First);
            hash.Add(plan.TypeName.Second);
            if (plan.Scalar)
            {
                AddScalar(value, plan.Code, ref hash);
                hash.Add(ulong.MaxValue);
                return;
            }
            if (value is ISceneNode)
            {
                if (context.References.TryGetValue(value, out int reference))
                {
                    hash.Add(ulong.MaxValue - 1);
                    hash.Add((ulong)reference);
                    return;
                }
                context.References.Add(value, context.References.Count);
            }
            if (value is Point point)
            {
                hash.Add(unchecked((ulong)point.X));
                hash.Add(unchecked((ulong)point.Y));
            }
            else if (value is IDictionary dictionary)
            {
                hash.Add((ulong)dictionary.Count);
                foreach (object key in dictionary.Keys)
                {
                    Visit(key, ref hash, context);
                    Visit(dictionary[key], ref hash, context);
                }
            }
            else if (value is IEnumerable collection)
            {
                ulong count = 0;
                foreach (object item in collection)
                {
                    Visit(item, ref hash, context);
                    count++;
                }
                hash.Add(count);
            }
            else
            {
                for (int index = 0; index < plan.Members.Length; index++)
                {
                    MemberInfo member = plan.Members[index];
                    object child = plan.RawIdentity[index]
                        ? ((BaseGameEntity)value).GetRecoveryInstanceID()
                        : plan.Getters[index].GetValue(value);
                    if (child == null)
                    {
                        if (member == plan.InlineCollection)
                            throw new InvalidOperationException(
                                "An inline collection cannot be null."
                            );
                        continue;
                    }
                    hash.Add((ulong)index + 1);
                    Visit(child, ref hash, context, plan.ScalarMembers[index]);
                }
            }
            hash.Add(ulong.MaxValue);
        }

        /// <summary>
        /// Carries an admitted continuation through the existing member visitor.
        /// </summary>
        [PersistableObject]
        private sealed class VerificationState
        {
            public GameSessionRecoveryState Continuation { get; set; }
            public FactionSettings[] FactionRules { get; set; }
        }

        /// <summary>
        /// Keeps reference identity and member selection local to one traversal.
        /// </summary>
        private sealed class HashContext
        {
            public readonly ConcurrentDictionary<Type, TypePlan> Plans;
            public readonly Func<Type, TypePlan> CreatePlan;
            public readonly Dictionary<object, int> References = new(ReferenceIdentity.Instance);
            private readonly Dictionary<Type, TypePlan> _localPlans = new();

            /// <summary>
            /// Binds a traversal to its cached model policy.
            /// </summary>
            /// <param name="plans">The cached type plans.</param>
            /// <param name="createPlan">The selected member-policy factory.</param>
            public HashContext(
                ConcurrentDictionary<Type, TypePlan> plans,
                Func<Type, TypePlan> createPlan
            )
            {
                Plans = plans;
                CreatePlan = createPlan;
            }

            /// <summary>
            /// Resolves each encountered runtime type from the shared cache once per traversal.
            /// </summary>
            /// <param name="type">The actual boxed or model type.</param>
            /// <returns>The immutable admitted member plan.</returns>
            public TypePlan GetPlan(Type type)
            {
                if (!_localPlans.TryGetValue(type, out TypePlan plan))
                {
                    plan = Plans.GetOrAdd(type, CreatePlan);
                    _localPlans.Add(type, plan);
                }
                return plan;
            }
        }

        /// <summary>
        /// Identifies actual instances without invoking gameplay equality or allocating identifiers.
        /// </summary>
        private sealed class ReferenceIdentity : IEqualityComparer<object>
        {
            public static readonly ReferenceIdentity Instance = new();

            /// <summary>
            /// Compares the actual object instances.
            /// </summary>
            /// <param name="x">The first instance.</param>
            /// <param name="y">The second instance.</param>
            /// <returns>True when both references identify the same object.</returns>
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            /// <summary>
            /// Computes a reference hash without model callbacks.
            /// </summary>
            /// <param name="obj">The object instance.</param>
            /// <returns>The runtime identity hash.</returns>
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Caches the existing serializer's write members once per runtime type.
        /// </summary>
        /// <param name="type">The persisted runtime type.</param>
        /// <returns>The cached member plan.</returns>
        private static TypePlan CreatePlan(Type type)
        {
            bool scalar = TypeHelper.IsScalar(type);
            if (scalar || type == typeof(Point) || typeof(IEnumerable).IsAssignableFrom(type))
                return new TypePlan(
                    type,
                    scalar,
                    Type.GetTypeCode(type),
                    Array.Empty<MemberInfo>(),
                    null
                );
            if (type.GetCustomAttribute<PersistableObjectAttribute>() == null)
                throw new ArgumentException("Object type " + type + " is not persistable.");
            return CreateObjectPlan(type);
        }

        /// <summary>
        /// Selects the model members also admitted by the explicit recovery schema.
        /// Result records use this policy even though they are not disk-save roots.
        /// </summary>
        /// <param name="type">The admitted model or result type.</param>
        /// <returns>The cached member plan.</returns>
        private static TypePlan CreateObjectPlan(Type type)
        {
            MemberInfo[] members = ReflectionHelper
                .GetPersistableAttributes(type, ReflectionHelper.OperationType.Write)
                .Concat(
                    ReflectionHelper.GetPersistableMembers(
                        type,
                        ReflectionHelper.OperationType.Write
                    )
                )
                .ToArray();
            return new TypePlan(
                type,
                false,
                TypeCode.Object,
                members,
                ReflectionHelper.GetInlineCollectionMember(
                    type,
                    ReflectionHelper.OperationType.Write
                )
            );
        }

        /// <summary>
        /// Hashes scalar values directly, avoiding XML conversion and temporary numeric strings.
        /// </summary>
        /// <param name="value">The boxed persisted scalar.</param>
        /// <param name="code">The cached scalar type code.</param>
        /// <param name="hash">The local accumulator.</param>
        private static void AddScalar(object value, TypeCode code, ref Accumulator hash)
        {
            switch (code)
            {
                case TypeCode.String:
                    hash.AddString((string)value);
                    break;
                case TypeCode.Boolean:
                    hash.Add((bool)value ? 1UL : 0UL);
                    break;
                case TypeCode.Char:
                    hash.Add((char)value);
                    break;
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int64:
                    hash.Add(unchecked((ulong)Convert.ToInt64(value)));
                    break;
                case TypeCode.Int32:
                    hash.Add(
                        unchecked((ulong)(value is int number ? number : Convert.ToInt64(value)))
                    );
                    break;
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    hash.Add(Convert.ToUInt64(value));
                    break;
                case TypeCode.Single:
                    hash.Add(unchecked((ulong)BitConverter.SingleToInt32Bits((float)value)));
                    break;
                case TypeCode.Double:
                    hash.Add(unchecked((ulong)BitConverter.DoubleToInt64Bits((double)value)));
                    break;
                case TypeCode.Decimal:
                    foreach (int part in decimal.GetBits((decimal)value))
                        hash.Add(unchecked((ulong)part));
                    break;
                case TypeCode.DateTime:
                    hash.Add(unchecked((ulong)((DateTime)value).ToBinary()));
                    break;
                default:
                    if (value is Guid id)
                        foreach (byte part in id.ToByteArray())
                            hash.Add(part);
                    else if (value is TimeSpan span)
                        hash.Add(unchecked((ulong)span.Ticks));
                    else if (value is DateTimeOffset date)
                    {
                        hash.Add(unchecked((ulong)date.Ticks));
                        hash.Add(unchecked((ulong)date.Offset.Ticks));
                    }
                    else
                        throw new ArgumentException(
                            "Unsupported persisted scalar " + value.GetType()
                        );
                    break;
            }
        }

        /// <summary>
        /// Computes a string digest once per live string object without retaining replaced scene graphs.
        /// </summary>
        /// <param name="value">The exact persisted string.</param>
        /// <returns>The cached string digest.</returns>
        private static StringDigest DigestString(string value)
        {
            Accumulator hash = new Accumulator();
            hash.Add((ulong)value.Length);
            int index = 0;
            while (index + 4 <= value.Length)
            {
                hash.Add(
                    value[index]
                        | ((ulong)value[index + 1] << 16)
                        | ((ulong)value[index + 2] << 32)
                        | ((ulong)value[index + 3] << 48)
                );
                index += 4;
            }
            ulong tail = 0;
            int shift = 0;
            while (index < value.Length)
            {
                tail |= (ulong)value[index++] << shift;
                shift += 16;
            }
            hash.Add(tail);
            return new StringDigest(hash.First, hash.Second);
        }

        /// <summary>
        /// Holds the immutable serializer plan for one runtime type.
        /// </summary>
        private sealed class TypePlan
        {
            public StringDigest TypeName { get; }
            public bool Scalar { get; }
            public TypeCode Code { get; }
            public MemberInfo[] Members { get; }
            public IValueProvider[] Getters { get; }
            public bool[] RawIdentity { get; }
            public TypePlan[] ScalarMembers { get; }
            public MemberInfo InlineCollection { get; }

            /// <summary>
            /// Captures a serializer member plan.
            /// </summary>
            /// <param name="type">The actual model type.</param>
            /// <param name="scalar">Whether the type is a scalar.</param>
            /// <param name="code">The scalar code.</param>
            /// <param name="members">The exact write members.</param>
            /// <param name="inlineCollection">The inline collection, when present.</param>
            public TypePlan(
                Type type,
                bool scalar,
                TypeCode code,
                MemberInfo[] members,
                MemberInfo inlineCollection
            )
            {
                TypeName = DigestString(type.FullName);
                Scalar = scalar;
                Code = code;
                Members = members;
                Getters = members.Select(_accessors.CreateAccessor).ToArray();
                RawIdentity = members
                    .Select(member =>
                        member.DeclaringType == typeof(BaseGameEntity)
                        && member.Name == nameof(BaseGameEntity.InstanceID)
                    )
                    .ToArray();
                ScalarMembers = members
                    .Select(member =>
                    {
                        Type memberType = ReflectionHelper.GetMemberType(member);
                        memberType = Nullable.GetUnderlyingType(memberType) ?? memberType;
                        return TypeHelper.IsScalar(memberType) ? CreatePlan(memberType) : null;
                    })
                    .ToArray();
                InlineCollection = inlineCollection;
            }
        }

        /// <summary>
        /// Reuses the JSON runtime's compiled member accessors and its supported AOT fallback.
        /// </summary>
        private sealed class AccessorFactory : DefaultContractResolver
        {
            /// <summary>
            /// Builds one cached value reader for an already admitted model member.
            /// </summary>
            /// <param name="member">The exact member selected by the model contract.</param>
            /// <returns>The reusable member reader.</returns>
            public IValueProvider CreateAccessor(MemberInfo member) =>
                CreateMemberValueProvider(member);
        }

        /// <summary>
        /// Stores an immutable weakly held string digest.
        /// </summary>
        private sealed class StringDigest
        {
            public ulong First { get; }
            public ulong Second { get; }

            /// <summary>
            /// Stores the two digest words.
            /// </summary>
            /// <param name="first">The first digest word.</param>
            /// <param name="second">The second digest word.</param>
            public StringDigest(ulong first, ulong second)
            {
                First = first;
                Second = second;
            }
        }

        /// <summary>
        /// Mixes typed persisted values into the local change fingerprint.
        /// </summary>
        private struct Accumulator
        {
            public ulong First;
            public ulong Second;

            /// <summary>
            /// Mixes one word without allocating or querying the scene graph.
            /// </summary>
            /// <param name="value">The next persisted word.</param>
            public void Add(ulong value)
            {
                unchecked
                {
                    First = (First ^ value) * 1099511628211UL;
                    Second ^= value + 0x9E3779B97F4A7C15UL + (Second << 6) + (Second >> 2);
                }
            }

            /// <summary>
            /// Mixes an exact persisted string through its weakly cached digest.
            /// </summary>
            /// <param name="value">The persisted string.</param>
            public void AddString(string value)
            {
                if (value == null)
                {
                    Add(0);
                    return;
                }
                StringDigest digest = _strings.GetValue(value, _digestString);
                Add(digest.First);
                Add(digest.Second);
            }
        }
    }
}
