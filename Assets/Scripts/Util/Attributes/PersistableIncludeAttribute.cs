using System;

/// <summary>
/// Attribute applied to fields or properties to specify the expected types for serialization and deserialization
/// by the GameSerializer. This attribute indicates potential derived types that a field or property may contain.
/// </summary>
/// <remarks>
/// The attribute can be applied multiple times to each field or property and is inherited by subclasses.
/// It is used to handle polymorphic types during the serialization and deserialization process.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    AllowMultiple = true,
    Inherited = true
)]
public class PersistableIncludeAttribute : Attribute
{
    /// <summary>
    /// Gets the type that is expected to be persisted for this field or property.
    /// </summary>
    public Type PersistableType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableIncludeAttribute"/> class.
    /// </summary>
    public PersistableIncludeAttribute() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableIncludeAttribute"/> class.
    /// </summary>
    /// <param name="persistableType">The type that is expected to be persisted for this field or property.</param>
    public PersistableIncludeAttribute(Type persistableType)
    {
        PersistableType = persistableType;
    }
}
