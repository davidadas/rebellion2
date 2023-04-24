/// <summary>
/// An interface for decorators which apply a static number of of the
/// first specified type parameter to instances of the second specified
/// type parameter. This interface's intended use is for single-instance
/// units, such as Officers and Planets.
/// </summary>
/// <typeparam name="TUnit">The GameNode to be assigned to a destination.</typeparam>
/// <typeparam name="TDestination">The GameNode destination to be decorated.</typeparam>
public interface IUnitDeployer<TUnit, TDestination>
    where TUnit : GameNode
    where TDestination : GameNode
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    public void DeployUnits(TUnit[] units, TDestination[] destinations);
}
