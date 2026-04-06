namespace Rebellion.Game
{
    public enum EntityStateFilter
    {
        All,    // All entities regardless of state (under construction, in transit, etc.)
        Active, // Operational only: manufacturing complete and not in transit
    }
}
