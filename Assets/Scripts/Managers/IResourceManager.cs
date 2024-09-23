/// <summary>
///
/// </summary>
public interface IResourceManager
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>()
        where T : IConfig;

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T[] GetSceneNodeData<T>()
        where T : SceneNode;
}
