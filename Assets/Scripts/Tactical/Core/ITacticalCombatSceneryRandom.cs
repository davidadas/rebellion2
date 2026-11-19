using System.Numerics;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Supplies the encounter-owned scenery sequence without acquiring a platform clock in battle execution.
    /// </summary>
    public interface ITacticalCombatSceneryRandom
    {
        /// <summary>
        /// Draws a bounded scenery value, retaining trivial-bound initialization behavior.
        /// </summary>
        /// <param name="upperBound">The requested exclusive bound.</param>
        /// <returns>The bounded scenery value.</returns>
        int NextInt(int upperBound);

        /// <summary>
        /// Captures one decoration's position using the encounter's deployment dimensions.
        /// </summary>
        /// <param name="depth">The deployment span.</param>
        /// <param name="horizontal">The horizontal offset in deployment spans.</param>
        /// <param name="spread">The vertical and depth half range in deployment spans.</param>
        /// <returns>The encounter-local decoration position.</returns>
        Vector3 NextPosition(float depth, float horizontal, float spread);
    }
}
