using System;

/// <summary>
/// Attribute applied to fields or properties within a class that should be included
/// serialization and deserialization processes by the GameSerializer as an element attribute.
/// </summary>
/// <remarks>
/// This attribute is designed to be used on instance fields or properties only and cannot be inherited by subclasses.
/// It is intended for use with classes marked by the <see cref="PersistableAttributeAttribute"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public class PersistableAttributeAttribute : Attribute
{
    public string Name { get; set; }
    public bool UseTypeName { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableAttributeAttribute"/> class.
    /// This attribute is used to mark a field or property as an element attribute during serialization and deserialization.
    /// </summary>
    public PersistableAttributeAttribute() { }
}
