namespace Rebellion.Simulation
{
    /// <summary>
    /// Identifies synchronous work boundaries within a session's incremental simulation.
    /// </summary>
    public enum SimulationPhase
    {
        Messages,
        AutomationBeforeProduction,
        ResourceProduction,
        ManufacturingStep,
        ManufacturingProgressNotification,
        AutomationRefill,
        Maintenance,
        Recovery,
        Captives,
        Movement,
        SpaceCombat,
        MovementResultSettlement,
        WaypointContinuation,
        BlockadeSettlement,
        KnowledgeAfterMovement,
        MessageDelivery,
        Missions,
        Events,
        Naming,
        AIStep,
        DeferredBlockades,
        PlanetaryControl,
        Uprising,
        Research,
        Jedi,
        Victory,
        KnowledgeAfterTick,
        ResultDelivery,
        TickCompletedNotification,
        CombatDecisionNotification,
        CombatResolution,
        CombatContinuation,
        CombatResumedNotification,
        LoadedStateReconciliation,
        StrategySnapshot,
        StrategyRender,
        HostCheckpointWrite,
        GuestCheckpointWrite,
        StateVerification,
    }
}
