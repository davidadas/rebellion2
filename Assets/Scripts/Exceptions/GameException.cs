using System;

/// <summary>
///
/// </summary>
public class GameException : Exception
{
    /// <summary>
    ///
    /// </summary>
    public GameException()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="message"></param>
    public GameException(string message)
        : base(message) { }
}
