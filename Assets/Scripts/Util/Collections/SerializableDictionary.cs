using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

/// <summary>
/// An extension of the Dictionary collection which allows nested serialization.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
{
    protected XmlSchema Schema = null;
    protected string EntryElement = "entry";
    protected string KeyElement = "key";
    protected string ValueElement = "value";
    protected XmlSerializer KeySerializer = new XmlSerializer(typeof(TKey));
    protected XmlSerializer ValueSerializer = new XmlSerializer(typeof(TValue));

    /// <summary>
    ///
    /// </summary>
    public SerializableDictionary()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public XmlSchema GetSchema()
    {
        return Schema;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="Schema"></param>
    public void SetSchema(XmlSchema Schema)
    {
        this.Schema = Schema;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="reader"></param>
    public void ReadXml(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return;
        }

        reader.ReadStartElement();

        while (reader.IsStartElement(EntryElement))
        {
            reader.ReadStartElement(EntryElement);

            reader.ReadStartElement(KeyElement);
            TKey key = (TKey)KeySerializer.Deserialize(reader);
            reader.ReadEndElement();

            reader.ReadStartElement(ValueElement);
            TValue value = (TValue)ValueSerializer.Deserialize(reader);
            reader.ReadEndElement();

            reader.ReadEndElement();
            this.Add(key, value);
        }

        reader.ReadEndElement();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="writer"></param>
    public void WriteXml(XmlWriter writer)
    {
        foreach (KeyValuePair<TKey, TValue> pairs in this)
        {
            writer.WriteStartElement(EntryElement);

            writer.WriteStartElement(KeyElement);
            KeySerializer.Serialize(writer, pairs.Key);
            writer.WriteEndElement();

            writer.WriteStartElement(ValueElement);
            ValueSerializer.Serialize(writer, pairs.Value);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
