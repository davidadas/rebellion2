namespace Rebellion.Game
{
    /// <summary>
    /// Base class for uprising-related events.
    /// Events describe state transitions but do not mutate state themselves.
    /// GameManager is responsible for applying event effects.
    /// </summary>
    public abstract class UprisingEvent
    {
        /// <summary>
        /// Warning that a planet has dangerously low loyalty.
        /// Fired with 10-tick cooldown to prevent spam.
        /// </summary>
        public class IncidentWarning : UprisingEvent
        {
            public string PlanetID;
            public int Tick;
        }

        /// <summary>
        /// An uprising has begun - ownership will transfer.
        /// Contains complete information for idempotent application.
        /// </summary>
        public class UprisingBegan : UprisingEvent
        {
            public string PlanetID;
            public string PreviousOwnerID;
            public string NewOwnerID;
            public int Tick;
        }

        /// <summary>
        /// An uprising has been successfully subdued.
        /// Planet remains under current owner's control.
        /// </summary>
        public class UprisingSubdued : UprisingEvent
        {
            public string PlanetID;
            public int Tick;
        }
    }
}
