using System.Collections.Generic;

public class OfficerManager
{
    private Game game;

    public OfficerManager(Game game)
    {
        this.game = game;
    }

    public void Update(Officer officer)
    {
        HealOfficer(officer);
        AttemptJailbreak(officer);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officer"></param>
    private void HealOfficer(Officer officer)
    {
        // @TODO: Implement.
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officer"></param>
    private void AttemptJailbreak(Officer officer)
    {
        // @TODO: Implement.
    }
}
