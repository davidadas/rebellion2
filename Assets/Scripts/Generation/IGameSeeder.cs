namespace Rebellion.Generation
{
    /// <summary>
    /// Contract every generation step implements. A seeder reads and writes the shared
    /// <see cref="GenerationContext"/> threaded through the pipeline by <see cref="GameBuilder"/>.
    /// </summary>
    public interface IGameSeeder
    {
        /// <summary>
        /// Applies this seeder's work against the given generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        void Seed(GenerationContext ctx);
    }
}
