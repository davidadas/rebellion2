using System.Collections.Generic;
using System.Xml.Serialization;

[XmlInclude(typeof(MoveUnitEvent))]
public class GameEventList : List<GameEvent>
{
    public GameEventList()
        : base() { }
}
