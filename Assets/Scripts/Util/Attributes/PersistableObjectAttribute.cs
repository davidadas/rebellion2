using System;

/// <summary>
/// Attribute applied to classes or interfaces that are intended to be serialized and deserialized
/// by the GameSerializer. This attribute marks a type as "persistable," meaning its instances can be saved to or loaded from XML.
/// </summary>
/// <remarks>
/// The attribute can only be applied once to each class or interface and is not inherited by subclasses.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface,
    Inherited = true,
    AllowMultiple = false
)]
public class PersistableObjectAttribute : Attribute
{
    public string Name { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistableObjectAttribute"/> class.
    /// This attribute is used to mark a class or interface as persistable for serialization purposes.
    /// </summary>
    public PersistableObjectAttribute() { }
}
