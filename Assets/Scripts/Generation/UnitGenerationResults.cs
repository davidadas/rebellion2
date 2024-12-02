/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
class UnitGenerationResults<TUnit> : IUnitGenerationResults<TUnit>
    where TUnit : BaseGameEntity
{
    public TUnit[] UnitPool { get; set; }
    public TUnit[] SelectedUnits { get; set; }
    public TUnit[] DeployedUnits { get; set; }

    public UnitGenerationResults(TUnit[] unitPool, TUnit[] selectedUnits, TUnit[] deployedUnits)
    {
        UnitPool = unitPool;
        SelectedUnits = selectedUnits;
        DeployedUnits = deployedUnits;
    }
}
