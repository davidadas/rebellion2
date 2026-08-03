/// <summary>
/// Implemented by views that own dynamic, state-swapped content textures which a static binding
/// cannot restore, and which must be re-resolved from installation content at composition time.
/// </summary>
public interface IContentInitializable
{
    /// <summary>
    /// Restores the implementer's content-sourced state textures from the supplied source.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    void InitializeContent(IContentAssetSource contentAssets);
}
