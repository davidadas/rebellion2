using System;

/// <summary>
/// Attribute applied to fields or properties within a class that should be included in
/// serialization and deserialization processes by the GameSerializer.
/// This attribute marks specific members as "persistable," meaning their values will be saved to or loaded from XML.
/// </summary>
/// <remarks>
/// This attribute is designed to be used on instance fields or properties only and cannot be inherited by subclasses.
/// It is intended for use with classes marked by the <see cref="PersistableObjectAttribute"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public class PersistableMemberAttribute : Attribute
{
    public string Name { get; set; }
    public bool UseTypeName { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableMemberAttribute"/> class.
    /// This attribute is used to mark a field or property as persistable, enabling it to be serialized and deserialized.
    /// </summary>
    public PersistableMemberAttribute() { }
}
