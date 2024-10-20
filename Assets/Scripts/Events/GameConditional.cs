using System;
using System.Xml.Serialization;
using DependencyInjectionExtensions;

/// <summary>
/// Represents a condition that must be met for an event. 
/// </summary>
/// <remarks>
/// Conditions are critical to the event system, as they determine when an event is
/// eligible to be executed. Conditions are evaluated at the time the event is scheduled
/// to occur, and if all conditions are met, the event is executed.
/// </remarks>
[Serializable]
public abstract class GameConditional : GameEntity
{
    [XmlAttribute("Type")]
    public string ConditionalType { get; set; }
    public SerializableDictionary<string, object> Parameters { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GameConditional() { }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameters"></param>
    public GameConditional(SerializableDictionary<string, object> parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public abstract bool IsMet(IServiceLocator serviceLocator);
}
