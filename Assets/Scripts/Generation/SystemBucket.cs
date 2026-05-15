using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Attributes;

namespace Rebellion.Generation
{
    /// <summary>
    /// Identifies a planet's role in a faction's starting territory: locked-in
    /// loyalist (Strong), contested-but-leaning (Weak), or unowned (Neutral).
    /// </summary>
    public enum BucketStrength
    {
        Strong,
        Weak,
        Neutral,
    }

    /// <summary>
    /// Tags a single planet with the faction it belongs to (if any) and the strength
    /// of that affiliation. <see cref="FactionID"/> is null for neutral planets.
    /// </summary>
    [PersistableObject]
    public class PlanetBucket
    {
        public string FactionID;
        public BucketStrength Strength;
    }

    /// <summary>
    /// Output of <see cref="GalaxySeeder"/>. Carries the per-planet bucket tags,
    /// faction HQ assignments, and starting loyalty values consumed by downstream
    /// seeders.
    /// </summary>
    public class GalaxyClassificationResult
    {
        public Dictionary<Planet, PlanetBucket> BucketMap = new Dictionary<Planet, PlanetBucket>();
        public Dictionary<string, Planet> FactionHQs = new Dictionary<string, Planet>();
        public Dictionary<Planet, int> StartingPlanetLoyalty = new Dictionary<Planet, int>();
    }
}
