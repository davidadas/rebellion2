using System.Linq;
using ArchUnitNET.Loader;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Rebellion.Architecture.Tests
{
    internal static class ProductionArchitecture
    {
        /// <summary>
        /// Loads every repository-owned runtime assembly so moving a type cannot bypass dependency rules.
        /// </summary>
        /// <returns>The complete production dependency model.</returns>
        internal static ArchitectureModel Load() =>
            new ArchLoader()
                .LoadAssemblies(
                    RuntimeAssemblyLoader
                        .Load()
                        .Append(typeof(ContentPackEditorProject).Assembly)
                        .ToArray()
                )
                .Build();
    }
}
