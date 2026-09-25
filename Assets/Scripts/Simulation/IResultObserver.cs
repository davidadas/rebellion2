namespace Rebellion.Simulation
{
    /// <summary>Connects one runtime observer to the result bus.</summary>
    internal interface IResultObserver
    {
        /// <summary>Registers this observer's result handlers.</summary>
        /// <param name="results">The bus that delivers completed results.</param>
        void Connect(GameResultBus results);
    }
}
