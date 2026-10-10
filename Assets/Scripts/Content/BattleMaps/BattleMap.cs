using System.Collections.Generic;
using Rebellion.Game.Combat;

/// <summary>
/// Stores a three-dimensional value authored in battle-map coordinates.
/// </summary>
public sealed class BattleMapVector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

/// <summary>
/// Stores a linear color authored by a battle map.
/// </summary>
public sealed class BattleMapColor
{
    public float Red { get; set; }
    public float Green { get; set; }
    public float Blue { get; set; }
    public float Alpha { get; set; } = 1f;
}

/// <summary>
/// Defines the scene-wide presentation settings owned by a battle map.
/// </summary>
public sealed class BattleMapEnvironment
{
    public BattleMapColor BackgroundColor { get; set; } = new BattleMapColor();
    public BattleMapColor AmbientSkyColor { get; set; } = new BattleMapColor();
    public BattleMapColor AmbientEquatorColor { get; set; } = new BattleMapColor();
    public BattleMapColor AmbientGroundColor { get; set; } = new BattleMapColor();
}

/// <summary>
/// Places one externally supplied planet model and its presentation layers in a battle map.
/// </summary>
public sealed class BattleMapPlanet
{
    public string InstanceID { get; set; }
    public string ModelAssetPath { get; set; }
    public string CloudTextureAssetPath { get; set; }
    public BattleMapVector3 Position { get; set; } = new BattleMapVector3();
    public BattleMapVector3 Rotation { get; set; } = new BattleMapVector3();
    public BattleMapVector3 SunDirection { get; set; } = new BattleMapVector3();
    public float Diameter { get; set; }
    public float AtmosphereRadiusRatio { get; set; }
    public float CloudRotationSpeed { get; set; }
}

/// <summary>
/// Defines one directional light placed by a battle map.
/// </summary>
public sealed class BattleMapDirectionalLight
{
    public string InstanceID { get; set; }
    public BattleMapVector3 Rotation { get; set; } = new BattleMapVector3();
    public BattleMapColor Color { get; set; } = new BattleMapColor();
    public float Intensity { get; set; }
    public bool CastShadows { get; set; }
    public bool IsSun { get; set; }
}

/// <summary>
/// Defines a deterministic camera-relative starfield.
/// </summary>
public sealed class BattleMapStarfield
{
    public string InstanceID { get; set; }
    public BattleMapColor PrimaryColor { get; set; } = new BattleMapColor();
    public BattleMapColor SecondaryColor { get; set; } = new BattleMapColor();
    public int Count { get; set; }
    public int Seed { get; set; }
    public float InnerRadius { get; set; }
    public float OuterRadius { get; set; }
    public float MinimumSize { get; set; }
    public float MaximumSize { get; set; }
    public float MinimumBrightness { get; set; }
    public float MaximumBrightness { get; set; }
    public float SecondaryColorProbability { get; set; }
}

/// <summary>
/// Defines a deterministic field of repeated embedded debris models.
/// </summary>
public sealed class BattleMapDebrisField
{
    public string InstanceID { get; set; }
    public string ModelAssetPath { get; set; }
    public float ShowDistance { get; set; }
    public float HideDistance { get; set; }
    public int CellCount { get; set; }
    public float CellNoise { get; set; }
    public float DebrisCountTarget { get; set; }
    public float MinimumScale { get; set; }
    public float MaximumScale { get; set; }
    public float ScaleBias { get; set; }
    public bool RandomRotation { get; set; }
    public int Seed { get; set; }
}

/// <summary>
/// Defines an axis-aligned region of battle space.
/// </summary>
public sealed class BattleMapBounds
{
    public float MinimumX { get; set; }
    public float MaximumX { get; set; }
    public float MinimumY { get; set; }
    public float MaximumY { get; set; }
    public float MinimumZ { get; set; }
    public float MaximumZ { get; set; }
}

/// <summary>
/// Assigns a bounded deployment region to one battle participant.
/// </summary>
public sealed class BattleMapDeploymentRegion
{
    public string ParticipantSlotID { get; set; }
    public BattleMapBounds Bounds { get; set; } = new BattleMapBounds();
    public BattleMapVector3 Facing { get; set; } = new BattleMapVector3();
}

/// <summary>
/// Defines a participant-specific starting view of a tactical battlefield.
/// </summary>
public sealed class BattleMapCameraStart
{
    public string ParticipantSlotID { get; set; }
    public BattleMapVector3 Position { get; set; } = new BattleMapVector3();
    public BattleMapVector3 Rotation { get; set; } = new BattleMapVector3();
    public float FieldOfView { get; set; }
}

/// <summary>
/// Describes an authored map available to tactical battles.
/// </summary>
public sealed class BattleMap
{
    private readonly List<BattleMapDeploymentRegion> _deploymentRegions =
        new List<BattleMapDeploymentRegion>();
    private readonly List<BattleMapCameraStart> _cameraStarts = new List<BattleMapCameraStart>();
    private readonly List<BattleMapPlanet> _planets = new List<BattleMapPlanet>();
    private readonly List<BattleMapDirectionalLight> _directionalLights =
        new List<BattleMapDirectionalLight>();
    private readonly List<BattleMapStarfield> _starfields = new List<BattleMapStarfield>();
    private readonly List<BattleMapDebrisField> _debrisFields = new List<BattleMapDebrisField>();
    private readonly Dictionary<string, byte[]> _embeddedAssets = new Dictionary<string, byte[]>();

    public string InstanceID { get; set; }
    public BattleKind Kind { get; set; }
    public BattleMapBounds PlayableBounds { get; set; } = new BattleMapBounds();
    public BattleMapEnvironment Environment { get; set; } = new BattleMapEnvironment();

    /// <summary>
    /// Returns the regions in which battle participants may be deployed.
    /// </summary>
    /// <returns>The map's deployment regions.</returns>
    public List<BattleMapDeploymentRegion> GetDeploymentRegions()
    {
        return _deploymentRegions;
    }

    /// <summary>
    /// Returns the authored starting views available to battle participants.
    /// </summary>
    /// <returns>The map's participant-specific camera starts.</returns>
    public List<BattleMapCameraStart> GetCameraStarts()
    {
        return _cameraStarts;
    }

    /// <summary>
    /// Returns the planets placed in this battlefield.
    /// </summary>
    /// <returns>The map's authored planets.</returns>
    public List<BattleMapPlanet> GetPlanets()
    {
        return _planets;
    }

    /// <summary>
    /// Returns the directional lights placed in this battlefield.
    /// </summary>
    /// <returns>The map's authored directional lights.</returns>
    public List<BattleMapDirectionalLight> GetDirectionalLights()
    {
        return _directionalLights;
    }

    /// <summary>
    /// Returns the deterministic starfields placed in this battlefield.
    /// </summary>
    /// <returns>The map's authored starfields.</returns>
    public List<BattleMapStarfield> GetStarfields()
    {
        return _starfields;
    }

    /// <summary>
    /// Returns the deterministic debris fields placed in this battlefield.
    /// </summary>
    /// <returns>The map's authored debris fields.</returns>
    public List<BattleMapDebrisField> GetDebrisFields()
    {
        return _debrisFields;
    }

    /// <summary>
    /// Tries to retrieve an asset stored inside this map package.
    /// </summary>
    /// <param name="path">The asset path relative to the package's assets directory.</param>
    /// <param name="data">The packaged bytes when the asset exists.</param>
    /// <returns>True when the asset exists in the package.</returns>
    public bool TryGetEmbeddedAsset(string path, out byte[] data)
    {
        return _embeddedAssets.TryGetValue(path, out data);
    }

    /// <summary>
    /// Adds one validated package asset while the map is loading.
    /// </summary>
    /// <param name="path">The asset path relative to the package's assets directory.</param>
    /// <param name="data">The packaged asset bytes.</param>
    internal void AddEmbeddedAsset(string path, byte[] data)
    {
        _embeddedAssets.Add(path, data);
    }
}
