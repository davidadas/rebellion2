using System;
using System.Collections.Generic;
using Rebellion.Game.Traits;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Stores the difficulty-specific values loaded from game configuration.
    /// </summary>
    [PersistableObject]
    public sealed class DifficultyModifiers
    {
        public int MissionSuccessChancePoints { get; set; }

        public int MineOutputPercent { get; set; } = 100;

        public int RefineryOutputPercent { get; set; } = 100;

        public int ManufacturingSpeedPercent { get; set; } = 100;
    }

    /// <summary>
    /// Adapts legacy difficulty configuration into the shared modifier pipeline.
    /// </summary>
    internal sealed class DifficultyModifierProvider
    {
        private readonly GameRoot _game;

        public DifficultyModifierProvider(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public IEnumerable<Modifier> GetModifiers(ModifierType type, ISceneNode subject)
        {
            DifficultyModifiers configured = _game.GetDifficultyModifier(
                subject.GetOwnerInstanceID()
            );
            switch (type)
            {
                case ModifierType.MissionSuccessChance
                    when configured.MissionSuccessChancePoints != 0:
                    yield return Add(type, configured.MissionSuccessChancePoints);
                    break;
                case ModifierType.ProductionSpeed when configured.ManufacturingSpeedPercent != 100:
                    yield return Multiply(type, configured.ManufacturingSpeedPercent);
                    break;
                case ModifierType.ResourceGenerationRate when subject is Building building:
                    int percent =
                        building.BuildingType == BuildingType.Mine
                            ? configured.MineOutputPercent
                            : configured.RefineryOutputPercent;
                    if (percent != 100)
                        yield return Multiply(type, percent);
                    break;
            }
        }

        private static Modifier Add(ModifierType type, int value) =>
            new Modifier
            {
                Type = type,
                Operation = ModifierOperation.Add,
                Value = value,
            };

        private static Modifier Multiply(ModifierType type, int percent) =>
            new Modifier
            {
                Type = type,
                Operation = ModifierOperation.Multiply,
                Value = percent / 100m,
            };
    }
}
