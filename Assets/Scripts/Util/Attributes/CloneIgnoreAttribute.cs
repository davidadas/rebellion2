using System;

/// <summary>
/// Attribute used to indicate that a property or field should be ignored when cloning an object.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CloneIgnoreAttribute : Attribute
{
    public CloneIgnoreAttribute()
    {
        // No implementation needed for this attribute.
    }
}
