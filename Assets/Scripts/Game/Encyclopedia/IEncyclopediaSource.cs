using System;
using System.Collections.Generic;

namespace Rebellion.Game.Encyclopedia
{
    /// <summary>
    /// Provides the authored content used to generate an encyclopedia entry.
    /// </summary>
    public interface IEncyclopediaSource
    {
        public string EncyclopediaImagePath { get; set; }
        public List<EncyclopediaEntryStat> EncyclopediaStats { get; set; }
        public string EncyclopediaDescription { get; set; }

        public bool HasEncyclopediaData =>
            !string.IsNullOrEmpty(EncyclopediaImagePath)
            || !string.IsNullOrEmpty(EncyclopediaDescription)
            || EncyclopediaStats?.Count > 0;

        /// <summary>
        /// Copies the authored encyclopedia content to another source.
        /// </summary>
        /// <param name="destination">The source receiving the copied content.</param>
        public void CopyEncyclopediaStateTo(IEncyclopediaSource destination)
        {
            destination.EncyclopediaImagePath = EncyclopediaImagePath;
            destination.EncyclopediaDescription = EncyclopediaDescription;
            destination.EncyclopediaStats = EncyclopediaStats?.ConvertAll(
                stat => new EncyclopediaEntryStat { Label = stat.Label, Value = stat.Value }
            );
        }
    }
}
