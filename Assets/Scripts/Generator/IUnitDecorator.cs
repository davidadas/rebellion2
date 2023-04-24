/// <summary>
/// An interface for generators which strictly modify the properties of the
/// specified type parameter.
/// </summary>
/// <typeparam name="TUnit">The GameNode to decorate.</typeparam>
public interface IUnitDecorator<TUnit>
    where TUnit : GameNode
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public TUnit[] DecorateUnits(TUnit[] units);
}
