using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

/// <summary>
/// An extension to the List class which provides simplified concatination.
/// </summary>
/// <typeparam name="T"></typeparam>
public class AppendableList<T> : List<T>
{
    /// <summary>
    ///
    /// </summary>
    public AppendableList()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="enumerables"></param>
    public AppendableList(IEnumerable<T> enumerables)
        : base(enumerables) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="size"></param>
    public AppendableList(int size)
        : base(size) { }

    /// <summary>
    /// Appends all elements from the provided enumerable to the end of this AppendableList's collection.
    /// </summary>
    /// <param name="enumerable"></param>
    /// <returns>a reference to this AppendableList</returns>
    public AppendableList<T> Append(IEnumerable<T> enumerable)
    {
        foreach (T entry in enumerable)
        {
            this.Add(entry);
        }

        return this;
    }

    /// <summary>
    /// Appends all elements from the provided enumerables to the end of this AppendableList's collection.
    /// </summary>
    /// <param name="enumerables"></param>
    /// <returns>a reference to this AppendableList</returns>
    public AppendableList<T> AppendAll(params IEnumerable<T>[] enumerables)
    {
        foreach (IEnumerable<T> enumerable in enumerables)
        {
            this.Append(enumerable);
        }

        return this;
    }
}
