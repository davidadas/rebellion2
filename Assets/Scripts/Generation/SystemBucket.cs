using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Util.Attributes;

namespace Rebellion.Generation
{
    public enum BucketStrength
    {
        Strong,
        Weak,
        Neutral,
    }

    [PersistableObject]
    public class PlanetBucket
    {
        public string FactionID;
        public BucketStrength Strength;
    }

    public class GalaxyClassificationResult
    {
        public Dictionary<Planet, PlanetBucket> BucketMap = new Dictionary<Planet, PlanetBucket>();
        public Dictionary<string, Planet> FactionHQs = new Dictionary<string, Planet>();
        public Dictionary<Planet, int> StartingPlanetLoyalty = new Dictionary<Planet, int>();
    }
}
