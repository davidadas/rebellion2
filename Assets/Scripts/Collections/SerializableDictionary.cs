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
    protected XmlSchema xmlSchema = null;
    protected string entryElement = "entry";
    protected string keyElement = "key";
    protected string valueElement = "value";
    protected XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
    protected XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

    /// <summary>
    ///
    /// </summary>
    public SerializableDictionary()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="xmlSchema"></param>
    public SerializableDictionary(XmlSchema xmlSchema)
    {
        this.SetSchema(xmlSchema);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public XmlSchema GetSchema()
    {
        return xmlSchema;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="xmlSchema"></param>
    public void SetSchema(XmlSchema xmlSchema)
    {
        this.xmlSchema = xmlSchema;
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

        while (reader.IsStartElement(entryElement))
        {
            reader.ReadStartElement(entryElement);

            reader.ReadStartElement(keyElement);
            TKey key = (TKey)keySerializer.Deserialize(reader);
            reader.ReadEndElement();

            reader.ReadStartElement(valueElement);
            TValue value = (TValue)valueSerializer.Deserialize(reader);
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
            writer.WriteStartElement(entryElement);

            writer.WriteStartElement(keyElement);
            keySerializer.Serialize(writer, pairs.Key);
            writer.WriteEndElement();

            writer.WriteStartElement(valueElement);
            valueSerializer.Serialize(writer, pairs.Value);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }
}
