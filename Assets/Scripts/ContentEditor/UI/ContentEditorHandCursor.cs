using UnityEngine;

/// <summary>
/// Draws the open and closed hand cursors used by the Strategy preview.
/// </summary>
internal static class ContentEditorHandCursor
{
    private static readonly string[] _openHand =
    {
        "......xx.........",
        "......xx.xx......",
        "......xx.xx.xx...",
        "......xx.xx.xx...",
        "......xx.xx.xx.xx",
        "......xx.xx.xx.xx",
        ".xx...xx.xx.xx.xx",
        ".xxx..xxxxxxxxxxx",
        "..xxx.xxxxxxxxxxx",
        "...xxxxxxxxxxxxx.",
        "....xxxxxxxxxxxx.",
        ".....xxxxxxxxxx..",
        "......xxxxxxxxx..",
        ".......xxxxxxxx..",
        ".......xxxxxxxx..",
    };

    private static readonly string[] _closedHand =
    {
        "......xx.xx......",
        "....xxxxxxxxxx...",
        "...xxxxxxxxxxxx..",
        "...xx.xx.xx.xxxx.",
        "..xxx.xx.xx.xxxx.",
        ".xxxxxxxxxxxxxxx.",
        ".xxxxxx.xxxxxxxx.",
        "..xxxxxxxxxxxxx..",
        "...xxxxxxxxxxxx..",
        "....xxxxxxxxxx...",
        ".....xxxxxxxxx...",
        ".....xxxxxxxxx...",
    };

    /// <summary>
    /// Rasterizes a high-contrast hand with a dark outline for both light and dark artwork.
    /// </summary>
    /// <param name="closed">Whether to depict an active grab.</param>
    /// <returns>A readable cursor texture with the palm at its center.</returns>
    internal static Texture2D Create(bool closed)
    {
        const int size = 32;
        string[] rows = closed ? _closedHand : _openHand;
        bool[] silhouette = new bool[size * size];
        int top = (size + rows.Length) / 2;
        for (int row = 0; row < rows.Length; row++)
        {
            int left = (size - rows[row].Length) / 2;
            for (int x = 0; x < rows[row].Length; x++)
                silhouette[(top - row) * size + left + x] = rows[row][x] == 'x';
        }
        Color32[] pixels = new Color32[silhouette.Length];
        for (int y = 1; y < size - 1; y++)
        for (int x = 1; x < size - 1; x++)
        {
            int index = y * size + x;
            if (silhouette[index])
                pixels[index] = Color.white;
            else if (
                silhouette[index - 1]
                || silhouette[index + 1]
                || silhouette[index - size]
                || silhouette[index + size]
            )
                pixels[index] = Color.black;
        }
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = closed ? "Grabbing hand" : "Open hand",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Releases a cursor owned by the desktop preview or an editor fixture.
    /// </summary>
    /// <param name="texture">The generated cursor texture.</param>
    internal static void Release(Texture2D texture)
    {
        if (texture == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(texture);
        else
            Object.DestroyImmediate(texture);
    }
}
