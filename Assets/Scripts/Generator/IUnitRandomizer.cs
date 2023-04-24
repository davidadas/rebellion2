/// <summary>
/// An interface for decorators which generate a randomized number of copies of
/// the first specified type parameter, applied randomly to instances of the
/// specified type parameter. This interface's intended use is for multi-instance
/// units, such as Buildings, Starfighters, etc, which can be replicated.
/// </summary>
/// <typeparam name="TUnit">The GameNode to be randomly replicated.</typeparam>
/// <typeparam name="TDestination">The GameNode destination to be decorated.</typeparam>
public interface IUnitRandomizer<TUnit, TDestination>
    where TUnit : GameNode
    where TDestination : GameNode
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public TDestination[] RandomizeUnits(TUnit[] units, TDestination[] destinations);
}
