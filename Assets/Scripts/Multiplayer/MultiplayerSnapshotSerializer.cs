using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Encodes wire states through cached contracts containing only the save serializer's write members.
    /// </summary>
    internal static class MultiplayerSnapshotSerializer
    {
        private static readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings _settings = CreateSettings(
            new SnapshotContracts(),
            new SnapshotTypes()
        );

        /// <summary>
        /// Creates strict model serialization settings for an explicitly selected protocol schema.
        /// </summary>
        /// <param name="contracts">The admitted model member contracts.</param>
        /// <param name="types">The admitted nested type identities.</param>
        /// <returns>The independent serializer settings.</returns>
        internal static JsonSerializerSettings CreateSettings(
            IContractResolver contracts,
            ISerializationBinder types
        )
        {
            return new JsonSerializerSettings
            {
                ContractResolver = contracts,
                SerializationBinder = types,
                TypeNameHandling = TypeNameHandling.Auto,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
                DateParseHandling = DateParseHandling.None,
                MissingMemberHandling = MissingMemberHandling.Error,
                Culture = CultureInfo.InvariantCulture,
                CheckAdditionalContent = true,
                Converters = { new PointConverter() },
            };
        }

        /// <summary>
        /// Writes a complete state without the XML document construction used by disk saves.
        /// </summary>
        /// <param name="game">The stable game state.</param>
        /// <returns>The UTF-8 wire representation.</returns>
        public static byte[] Serialize(GameRoot game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return SerializeValue(game, typeof(GameRoot));
        }

        /// <summary>
        /// Writes a value whose root schema has been selected by the protocol.
        /// </summary>
        /// <param name="value">The model value to encode.</param>
        /// <param name="declaredType">The protocol's explicit root type.</param>
        /// <returns>The UTF-8 representation.</returns>
        public static byte[] SerializeValue(object value, Type declaredType)
        {
            return SerializeValue(value, declaredType, _settings);
        }

        /// <summary>
        /// Writes a protocol-selected value using an independent explicit serialization policy.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="declaredType">The protocol root schema.</param>
        /// <param name="settings">The strict schema and reference policy.</param>
        /// <returns>The UTF-8 representation.</returns>
        internal static byte[] SerializeValue(
            object value,
            Type declaredType,
            JsonSerializerSettings settings
        )
        {
            JsonSerializer serializer = JsonSerializer.Create(settings);
            using MemoryStream stream = new MemoryStream();
            using (StreamWriter text = new StreamWriter(stream, _encoding, 16384, true))
            using (JsonTextWriter writer = new JsonTextWriter(text))
                serializer.Serialize(writer, value, declaredType);
            return stream.ToArray();
        }

        /// <summary>
        /// Restores the persisted model while rejecting type names outside the model's known schema.
        /// </summary>
        /// <param name="payload">The validated, decompressed wire representation.</param>
        /// <returns>The restored game.</returns>
        public static GameRoot Deserialize(byte[] payload)
        {
            return (GameRoot)DeserializeValue(payload, typeof(GameRoot));
        }

        /// <summary>
        /// Reads a value using a protocol-selected root schema and the admitted nested model types.
        /// </summary>
        /// <param name="payload">The UTF-8 representation.</param>
        /// <param name="declaredType">The protocol's explicit root type.</param>
        /// <returns>The decoded non-null value.</returns>
        public static object DeserializeValue(byte[] payload, Type declaredType)
        {
            return DeserializeValue(payload, declaredType, _settings);
        }

        /// <summary>
        /// Reads a protocol-selected value using its strict schema and reference policy.
        /// </summary>
        /// <param name="payload">The UTF-8 representation.</param>
        /// <param name="declaredType">The protocol root schema.</param>
        /// <param name="settings">The strict schema and reference policy.</param>
        /// <returns>The independently restored non-null value.</returns>
        internal static object DeserializeValue(
            byte[] payload,
            Type declaredType,
            JsonSerializerSettings settings
        )
        {
            JsonSerializer serializer = JsonSerializer.Create(settings);
            using MemoryStream stream = new MemoryStream(payload, false);
            using StreamReader text = new StreamReader(stream, _encoding);
            using JsonTextReader reader = new JsonTextReader(text);
            object value =
                serializer.Deserialize(reader, declaredType)
                ?? throw new InvalidDataException("A multiplayer payload must contain a value.");
            return value;
        }

        /// <summary>
        /// Compiles and caches getters and setters for the existing persisted-member selection.
        /// </summary>
        internal class SnapshotContracts : DefaultContractResolver
        {
            /// <summary>
            /// Keeps framework serialization callbacks outside the model-member contract.
            /// </summary>
            public SnapshotContracts()
            {
                IgnoreSerializableInterface = true;
                IgnoreSerializableAttribute = true;
            }

            /// <summary>
            /// Preserves shared references to graph roots and scene nodes during snapshot transfer.
            /// </summary>
            /// <param name="objectType">The persisted model type.</param>
            /// <returns>The cached model contract with its reference policy.</returns>
            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                JsonObjectContract contract = base.CreateObjectContract(objectType);
                if (
                    objectType == typeof(GameRoot)
                    || typeof(BaseSceneNode).IsAssignableFrom(objectType)
                )
                    contract.IsReference = true;
                return contract;
            }

            /// <summary>
            /// Selects exactly the attributes and members included in current saved states.
            /// </summary>
            /// <param name="objectType">The model type.</param>
            /// <returns>The ordered persisted members.</returns>
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                return ReflectionHelper
                    .GetPersistableAttributes(objectType, ReflectionHelper.OperationType.Write)
                    .Concat(
                        ReflectionHelper.GetPersistableMembers(
                            objectType,
                            ReflectionHelper.OperationType.Write
                        )
                    )
                    .Distinct()
                    .ToList();
            }

            /// <summary>
            /// Allows the selected private members to use cached accessors rather than public-property inference.
            /// </summary>
            /// <param name="member">The persisted member.</param>
            /// <param name="memberSerialization">The enclosing contract's mode.</param>
            /// <returns>The wire property contract.</returns>
            protected override JsonProperty CreateProperty(
                MemberInfo member,
                MemberSerialization memberSerialization
            )
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                property.Readable = true;
                property.Writable = true;
                property.Ignored = false;
                if (
                    member.DeclaringType == typeof(GameRoot)
                    && ReflectionHelper.GetMemberType(member) == typeof(GalaxyMap)
                )
                    property.ValueProvider = new GalaxyValueProvider(property.ValueProvider);
                return property;
            }
        }

        /// <summary>
        /// Restores runtime parent links without treating graph loading as a new parent transition.
        /// </summary>
        private sealed class GalaxyValueProvider : IValueProvider
        {
            private readonly IValueProvider _inner;

            /// <summary>
            /// Wraps the model's galaxy setter while retaining its node-registration behavior.
            /// </summary>
            /// <param name="inner">The cached model accessor.</param>
            public GalaxyValueProvider(IValueProvider inner)
            {
                _inner = inner;
            }

            /// <summary>
            /// Reads the current graph through the model's cached accessor.
            /// </summary>
            /// <param name="target">The game being encoded.</param>
            /// <returns>The persisted galaxy.</returns>
            public object GetValue(object target)
            {
                return _inner.GetValue(target);
            }

            /// <summary>
            /// Retains persisted previous-parent identities around runtime hierarchy reconstruction.
            /// </summary>
            /// <param name="target">The game being restored.</param>
            /// <param name="value">The fully decoded galaxy hierarchy.</param>
            public void SetValue(object target, object value)
            {
                Dictionary<BaseSceneNode, string> previousParents = new();
                if (value is GalaxyMap galaxy)
                    galaxy.TraverseIncludingDisabled(node =>
                        previousParents.Add((BaseSceneNode)node, node.LastParentInstanceID)
                    );
                _inner.SetValue(target, value);
                GameRoot game = (GameRoot)target;
                foreach (KeyValuePair<BaseSceneNode, string> entry in previousParents)
                {
                    entry.Key.LastParentInstanceID = entry.Value;
                    entry.Key.LastParentNode = string.IsNullOrEmpty(entry.Value)
                        ? null
                        : game.GetSceneNodeByInstanceID<ISceneNode>(entry.Value, true);
                }
            }
        }

        /// <summary>
        /// Resolves polymorphic state types from the inspected model schema without loading wire-supplied assemblies.
        /// </summary>
        internal sealed class SnapshotTypes : ISerializationBinder
        {
            private readonly Dictionary<string, Type> _types = new Dictionary<string, Type>(
                StringComparer.Ordinal
            );

            /// <summary>
            /// Registers model types and the closed member containers reachable from their persistence contracts.
            /// </summary>
            /// <param name="additionalTypes">Additional explicit protocol roots and their reachable member types.</param>
            public SnapshotTypes(IEnumerable<Type> additionalTypes = null)
            {
                Queue<Type> pending = new Queue<Type>(
                    typeof(GameRoot)
                        .Assembly.GetTypes()
                        .Where(type =>
                            type.IsDefined(typeof(PersistableObjectAttribute), true) || type.IsEnum
                        )
                        .Concat(additionalTypes ?? Type.EmptyTypes)
                );
                while (pending.Count > 0)
                {
                    Type type = pending.Dequeue();
                    if (
                        type.FullName == null
                        || type.ContainsGenericParameters
                        || _types.ContainsKey(type.FullName)
                    )
                        continue;
                    _types.Add(type.FullName, type);
                    if (type.IsArray)
                        pending.Enqueue(type.GetElementType());
                    if (type.IsGenericType)
                    {
                        foreach (Type argument in type.GetGenericArguments())
                            pending.Enqueue(argument);
                        Type definition = type.GetGenericTypeDefinition();
                        if (
                            definition == typeof(IList<>)
                            || definition == typeof(IEnumerable<>)
                            || definition == typeof(ICollection<>)
                            || definition == typeof(IReadOnlyList<>)
                            || definition == typeof(IReadOnlyCollection<>)
                        )
                            pending.Enqueue(
                                typeof(List<>).MakeGenericType(type.GetGenericArguments())
                            );
                        if (
                            definition == typeof(IDictionary<,>)
                            || definition == typeof(IReadOnlyDictionary<,>)
                        )
                            pending.Enqueue(
                                typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments())
                            );
                    }
                    if (type.Assembly != typeof(GameRoot).Assembly || type.IsEnum)
                        continue;
                    foreach (
                        MemberInfo member in ReflectionHelper
                            .GetPersistableAttributes(type, ReflectionHelper.OperationType.Write)
                            .Concat(
                                ReflectionHelper.GetPersistableMembers(
                                    type,
                                    ReflectionHelper.OperationType.Write
                                )
                            )
                    )
                        pending.Enqueue(ReflectionHelper.GetMemberType(member));
                }
            }

            /// <summary>
            /// Resolves only exact type identities already present in the model schema.
            /// </summary>
            /// <param name="assemblyName">The wire assembly name, which must be absent.</param>
            /// <param name="typeName">The known model or member-container type name.</param>
            /// <returns>The admitted type.</returns>
            public Type BindToType(string assemblyName, string typeName)
            {
                if (
                    !string.IsNullOrEmpty(assemblyName)
                    || typeName == null
                    || !_types.TryGetValue(typeName, out Type type)
                )
                    throw new JsonSerializationException(
                        "The multiplayer state contains an unsupported type."
                    );
                return type;
            }

            /// <summary>
            /// Emits known type identities without assembly-loading instructions.
            /// </summary>
            /// <param name="serializedType">The model type being emitted.</param>
            /// <param name="assemblyName">The omitted assembly name.</param>
            /// <param name="typeName">The model's full type identity.</param>
            public void BindToName(
                Type serializedType,
                out string assemblyName,
                out string typeName
            )
            {
                assemblyName = null;
                typeName = serializedType.FullName;
                if (typeName == null || !_types.ContainsKey(typeName))
                    throw new JsonSerializationException(
                        "The multiplayer state contains an unsupported type."
                    );
            }
        }

        /// <summary>
        /// Retains integer map coordinates without framework type-converter or read-only-property inference.
        /// </summary>
        private sealed class PointConverter : JsonConverter<Point>
        {
            /// <summary>
            /// Writes a map position as its two integer coordinates.
            /// </summary>
            /// <param name="writer">The wire writer.</param>
            /// <param name="value">The map position.</param>
            /// <param name="serializer">The enclosing serializer.</param>
            public override void WriteJson(
                JsonWriter writer,
                Point value,
                JsonSerializer serializer
            )
            {
                writer.WriteStartArray();
                writer.WriteValue(value.X);
                writer.WriteValue(value.Y);
                writer.WriteEndArray();
            }

            /// <summary>
            /// Restores exactly two integer coordinates.
            /// </summary>
            /// <param name="reader">The wire reader.</param>
            /// <param name="objectType">The declared coordinate type.</param>
            /// <param name="existingValue">The unused prior value.</param>
            /// <param name="hasExistingValue">Whether a prior value was supplied.</param>
            /// <param name="serializer">The enclosing serializer.</param>
            /// <returns>The restored map position.</returns>
            public override Point ReadJson(
                JsonReader reader,
                Type objectType,
                Point existingValue,
                bool hasExistingValue,
                JsonSerializer serializer
            )
            {
                if (
                    reader.TokenType != JsonToken.StartArray
                    || !reader.Read()
                    || reader.TokenType != JsonToken.Integer
                )
                    throw new JsonSerializationException(
                        "A map position requires two integer coordinates."
                    );
                int x = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                if (!reader.Read() || reader.TokenType != JsonToken.Integer)
                    throw new JsonSerializationException(
                        "A map position requires two integer coordinates."
                    );
                int y = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                if (!reader.Read() || reader.TokenType != JsonToken.EndArray)
                    throw new JsonSerializationException(
                        "A map position requires two integer coordinates."
                    );
                return new Point(x, y);
            }
        }
    }
}
