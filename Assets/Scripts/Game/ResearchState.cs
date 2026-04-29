using System.Collections.Generic;

namespace Rebellion.Game
{
    /// <summary>
    /// Research disciplines tracked by the side-level research system.
    /// </summary>
    public enum ResearchDiscipline
    {
        ShipDesign = 0,
        FacilityDesign = 1,
        TroopTraining = 2,
    }

    /// <summary>
    /// Persisted side-level research state for a faction.
    /// </summary>
    public sealed class FactionResearchState
    {
        /// <summary>
        /// Cost scaling percent applied to research difficulty. Defaults to 100.
        /// </summary>
        public int CostScalePercent { get; set; } = 100;

        /// <summary>
        /// Preserved persisted side research field. Defaults to 1.
        /// </summary>
        public int ReservedState { get; set; } = 1;

        /// <summary>
        /// Per-discipline research progression state.
        /// </summary>
        public Dictionary<ResearchDiscipline, ResearchDisciplineState> Disciplines { get; set; } =
            new Dictionary<ResearchDiscipline, ResearchDisciplineState>
            {
                { ResearchDiscipline.ShipDesign, new ResearchDisciplineState() },
                { ResearchDiscipline.FacilityDesign, new ResearchDisciplineState() },
                { ResearchDiscipline.TroopTraining, new ResearchDisciplineState() },
            };
    }

    /// <summary>
    /// Persisted research progression state for one discipline.
    /// </summary>
    public sealed class ResearchDisciplineState
    {
        /// <summary>
        /// Research capacity currently available for this discipline.
        /// </summary>
        public int CapacityRemaining { get; set; }

        /// <summary>
        /// Current research order reached for this discipline.
        /// </summary>
        public int CurrentOrder { get; set; }

        /// <summary>
        /// Whether this discipline has no further advances available.
        /// </summary>
        public bool IsExhausted { get; set; }
    }

    /// <summary>
    /// One researchable catalog entry for a faction and discipline.
    /// </summary>
    public sealed class ResearchCatalogEntry
    {
        /// <summary>
        /// Discipline this entry belongs to.
        /// </summary>
        public ResearchDiscipline Discipline { get; set; }

        /// <summary>
        /// Research order required to unlock this entry.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Technology represented by this catalog entry.
        /// </summary>
        public Technology Technology { get; set; }

        /// <summary>
        /// Research difficulty for this entry.
        /// </summary>
        public int Difficulty { get; set; }
    }
}
