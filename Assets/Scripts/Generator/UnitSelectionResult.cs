/// <summary>
///
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class UnitSelectionResult<T> : IUnitSelectionResult<T>
    where T : GameNode
{
    private T[] _selected;
    private T[] _remaining;

    /// <summary>
    ///
    /// </summary>
    /// <param name="selected"></param>
    /// <param name="remaining"></param>
    public UnitSelectionResult(T[] selected, T[] remaining)
    {
        this._selected = selected;
        this._remaining = remaining;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public T[] GetSelectedUnits()
    {
        return _selected;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public T[] GetRemainingUnits()
    {
        return _remaining;
    }
}
