using System;

/// <summary>
/// Indicates that a field or property should be ignored during the cloning process.
/// </summary>
/// <remarks>
/// When applied to a field or property, the GetShallowCopy method will set the
/// corresponding member to null in the cloned object, regardless of its value
/// in the original object.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false
)]
public class CloneIgnoreAttribute : Attribute
{
    public CloneIgnoreAttribute()
    {
        // No implementation needed for this attribute.
    }
}
