/// <summary>
///
/// </summary>
public interface IConfig
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T GetValue<T>(string path);
}
