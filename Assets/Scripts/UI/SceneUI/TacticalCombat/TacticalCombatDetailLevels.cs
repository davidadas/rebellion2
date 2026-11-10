/// <summary>
/// Selects capital and fighter presentation resources from the camera's detail metric.
/// </summary>
public static class TacticalCombatDetailLevels
{
    /// <summary>
    /// Selects a capital model while retaining its current detail at exact high-detail boundaries.
    /// </summary>
    /// <param name="distance">The rounded detail distance.</param>
    /// <param name="field">The active viewport field.</param>
    /// <param name="highDetail">Whether all three model levels are enabled.</param>
    /// <param name="current">The current model level.</param>
    /// <returns>The selected model level.</returns>
    public static int SelectCapital(float distance, float field, bool highDetail, int current)
    {
        if (!highDetail)
            return distance > 20.0 / field ? 2 : 1;
        float far = (float)(40.0 / field);
        float near = (float)(15.0 / field);
        if (distance > far && current != 2)
            return 2;
        if (distance > near && distance < far && current != 1)
            return 1;
        if ((distance < near || float.IsNaN(distance) || float.IsNaN(near)) && current != 0)
            return 0;
        return current;
    }

    /// <summary>
    /// Selects a fighter sprite from the angular field and available detail levels.
    /// </summary>
    /// <param name="distance">The rounded detail distance.</param>
    /// <param name="field">The active viewport field.</param>
    /// <param name="highDetail">Whether the near fighter sprite is enabled.</param>
    /// <param name="current">The current sprite level.</param>
    /// <returns>The selected sprite level.</returns>
    public static int SelectFighter(float distance, float field, bool highDetail, int current)
    {
        return new TacticalCombatDetailProjection(field).SelectFighter(
            distance,
            highDetail,
            current
        );
    }

    /// <summary>
    /// Selects a fighter sprite from the completed retained angular metric.
    /// </summary>
    /// <param name="metric">The field-dependent projection metric.</param>
    /// <param name="highDetail">Whether the near fighter sprite is enabled.</param>
    /// <param name="current">The current sprite level.</param>
    /// <returns>The selected sprite level.</returns>
    public static int SelectFighter(double metric, bool highDetail, int current)
    {
        bool atLeastTen = !double.IsNaN(metric) && !(metric < 10);
        bool atLeastFive = !double.IsNaN(metric) && !(metric < 5);
        if (!highDetail)
            return atLeastTen ? 2 : 1;
        if (atLeastTen && current != 2)
            return 2;
        if (atLeastFive && metric < 10 && current != 1)
            return 1;
        if (!atLeastFive && current != 0)
            return 0;
        return current;
    }
}
