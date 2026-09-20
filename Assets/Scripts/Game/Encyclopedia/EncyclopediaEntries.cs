using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Encyclopedia
{
    [PersistableObject]
    public sealed class EncyclopediaEntries : List<EncyclopediaEntry> { }
}
