using System.Collections.Generic;

public class OfficerManager
{
    private Game game;

    public OfficerManager(Game game)
    {
        this.game = game;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officers"></param>
    public void Update(List<Officer> officers)
    {
        foreach (Officer officer in officers)
        {
            HealOfficer(officer);
            AttemptJailbreak(officer);
        }
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
