namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies the active behavior of a tactical ship group.
    /// </summary>
    public enum TacticalBehavior
    {
        /// <summary>No tactical behavior is active.</summary>
        None = 0,

        /// <summary>The group is choosing or pursuing its primary target.</summary>
        PrimaryTarget = 1,

        /// <summary>The fighter group is returning to its carrier.</summary>
        Recover = 2,

        /// <summary>The group is withdrawing from the battle.</summary>
        Withdraw = 3,

        /// <summary>The group is attacking opposing fighter squadrons.</summary>
        AttackFighters = 4,

        /// <summary>The group is attacking opposing capital ships.</summary>
        AttackCapitalShips = 5,

        /// <summary>The fighter group is performing a Death Star attack run.</summary>
        AttackDeathStar = 6,

        /// <summary>The group is approaching around the target's left side.</summary>
        LeftHook = 7,

        /// <summary>The group is approaching around the target's right side.</summary>
        RightHook = 8,

        /// <summary>The group is approaching from below the target.</summary>
        Hammer = 9,

        /// <summary>The group is approaching from above the target.</summary>
        Anvil = 10,

        /// <summary>The group is holding its current tactical state.</summary>
        Hold = 11,
    }
}
