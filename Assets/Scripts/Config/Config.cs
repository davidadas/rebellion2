using System.Linq;
using System.Reflection;
using System;

public class Config : IConfig
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="key"></param>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    private object findField(object key, string fieldName)
    {
        // Unity does not support property-based serialization.
        // Therefore, we will reflect only fields here; not properties.
        FieldInfo[] fields = key.GetType().GetFields();

        foreach (FieldInfo fieldInfo in fields)
        {
            if (fieldInfo.Name == fieldName)
            {
                object value = fieldInfo.GetValue(key);
                if (value == null)
                {
                    throw new ConfigException($"\"{fieldName}\" not found in config or is null.");
                }
                return value;
            }
        }

        // If we have reached here, this property does not exist.
        throw new ConfigException($"No such property \"{fieldName}\" in config.");
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T GetValue<T>(string path)
    {
        string[] paths = path.Split(".");
        object result = paths.Aggregate((object)this, findField);

        return (T)result;
    }
}
