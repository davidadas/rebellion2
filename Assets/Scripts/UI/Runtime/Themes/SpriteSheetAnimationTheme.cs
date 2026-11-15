using Rebellion.Util.Serialization;

/// <summary>
/// Describes a looping animation stored as a grid in one content image.
/// </summary>
[PersistableObject]
public sealed class SpriteSheetAnimationTheme
{
    public string ImagePath { get; set; }

    public int FrameCount { get; set; } = 1;

    public int Columns { get; set; } = 1;

    public float FramesPerSecond { get; set; } = 30f;
}
