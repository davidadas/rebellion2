using System;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Accepts an additional XML name when reading a persistable type or member.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = true,
        Inherited = false
    )]
    public sealed class PersistableReadAliasAttribute : Attribute
    {
        public string Name { get; }

        /// <summary>
        /// Gets or sets whether the alias is deprecated. Deprecated aliases remain readable.
        /// </summary>
        public bool Deprecated { get; set; }

        /// <summary>
        /// Declares an additional XML name accepted only during deserialization.
        /// </summary>
        /// <param name="name">The nonblank XML name to accept.</param>
        public PersistableReadAliasAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A read alias name is required.", nameof(name));
            Name = name;
        }
    }
}
