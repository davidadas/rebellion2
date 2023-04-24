/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
public interface IUnitSelector<TUnit>
    where TUnit : GameNode
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public IUnitSelectionResult<TUnit> SelectUnits(TUnit[] units);
}
