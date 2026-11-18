using ArchUnitNET.Loader;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Input;
using Rebellion.Tactical;
using Rebellion.Tactical.Runtime.Audio;
using Rebellion.Util.Random;
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
                    typeof(GameRoot).Assembly,
                    typeof(ContentPackEditorProject).Assembly,
                    typeof(IRandomNumberProvider).Assembly,
                    typeof(CombatSide).Assembly,
                    typeof(TacticalCombatDefinition).Assembly,
                    typeof(TacticalCombatSession).Assembly,
                    typeof(TacticalCombatAudio).Assembly,
                    typeof(PlayerInputActions).Assembly
                )
                .Build();
    }
}
