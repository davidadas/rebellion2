/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
public interface IUnitGenerationResults<TUnit>
    where TUnit : BaseGameEntity
{
    // Array of all possible unit options.
    public TUnit[] UnitPool { get; set; }

    // Array of units, selected from pool.
    public TUnit[] SelectedUnits { get; set; }

    // Array of units deployed from pool.
    public TUnit[] DeployedUnits { get; set; }
}
