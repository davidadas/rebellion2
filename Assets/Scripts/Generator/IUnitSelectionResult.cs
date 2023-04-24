/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
public interface IUnitSelectionResult<TUnit>
    where TUnit : GameNode
{
    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public TUnit[] GetSelectedUnits();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public TUnit[] GetRemainingUnits();
}
