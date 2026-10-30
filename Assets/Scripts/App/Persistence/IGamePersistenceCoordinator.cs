using System.Threading.Tasks;

namespace Rebellion.App
{
    /// <summary>
    /// Owns save and load permissions and reports completion after the required durable publications.
    /// </summary>
    public interface IGamePersistenceCoordinator
    {
        /// <summary>
        /// Gets whether a save request can be queued independently of the current tick execution phase.
        /// </summary>
        bool CanRequestSave { get; }

        /// <summary>
        /// Saves a checkpoint under the current session's participant and connection policy.
        /// </summary>
        /// <param name="fileName">The save identifier without its extension.</param>
        /// <param name="displayName">The requested display name, or null for the slot default.</param>
        /// <returns>True when all publications required by the current policy completed successfully.</returns>
        Task<bool> SaveGameAsync(string fileName, string displayName);

        /// <summary>
        /// Loads a shared checkpoint and completes after the participants have synchronized it.
        /// </summary>
        /// <param name="fileName">The save identifier without its extension.</param>
        /// <returns>True when the coordinated load completed successfully.</returns>
        Task<bool> LoadGameAsync(string fileName);

        /// <summary>
        /// Receives a completed local tick for scheduling a shared automatic checkpoint.
        /// </summary>
        /// <param name="settings">The current automatic-save preferences.</param>
        void ProcessAutosaveTick(UserGameplaySettings settings);
    }
}
