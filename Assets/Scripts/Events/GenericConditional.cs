using System.Collections.Generic;
using System.Xml.Serialization;
using System;
using DependencyInjectionExtensions;

/// <summary>
/// A generic conditional that uses a callback to determine if the condition is met.
/// </summary>
[Serializable]
public class GenericConditional : GameConditional
{
    public SerializableDictionary<string, object> parameters;
    private readonly Func<IServiceLocator, SerializableDictionary<string, object>, bool> callback;

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public GenericConditional() { }

    /// <summary>
    /// Initializes a new instance of the GenericConditional class.
    /// </summary>
    /// <param name="callback">The function that evaluates whether the condition is met.</param>
    /// <param name="parameters">The parameters required for the condition.</param>
    public GenericConditional(Func<IServiceLocator, SerializableDictionary<string, object>, bool> callback, SerializableDictionary<string, object> parameters)
    {
        this.callback = callback;
        this.parameters = parameters;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceLocator"></param>
    /// <returns></returns>
    public override bool IsMet(IServiceLocator serviceLocator)
    {
        return callback.Invoke(serviceLocator, parameters);
    }
}
