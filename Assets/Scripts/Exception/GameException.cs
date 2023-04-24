using System;

/// <summary>
///
/// </summary>
public class GameException : Exception
{
    public GameException()
        : base() { }

    public GameException(string message)
        : base(message) { }
}
