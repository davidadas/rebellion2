using System;

/// <summary>
///
/// </summary>
public class ConfigException : Exception
{
    /// <summary>
    ///
    /// </summary>
    public ConfigException()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="message"></param>
    public ConfigException(string message)
        : base(message) { }
}
