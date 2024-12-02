using System;

/// <summary>
/// Attribute applied to fields or properties within a class that should be excluded from
/// serialization and deserialization processes by the GameSerializer.
/// </summary>
/// <remarks>
/// This attribute is designed to be used on instance fields or properties only and cannot be inherited by subclasses.
/// It is intended for use with classes marked by the <see cref="PersistableIgnoreAttribute"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public class PersistableIgnoreAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableIgnoreAttribute"/> class.
    /// This attribute is used to mark a field or property as ignored during serialization and deserialization.
    /// </summary>
    public PersistableIgnoreAttribute() { }
}
