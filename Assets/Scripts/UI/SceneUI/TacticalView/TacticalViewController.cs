using System;
using Rebellion.Game;
using Rebellion.Game.Combat;
using UnityEngine;

/// <summary>
/// Composes the Unity tactical scene with the active battle and its authored map definition.
/// </summary>
public sealed class TacticalViewController : MonoBehaviour
{
    [SerializeField]
    private Camera battleCamera;

    [SerializeField]
    private Transform battleRoot;

    /// <summary>
    /// Gets the active battle presented by this scene.
    /// </summary>
    public ActiveBattle ActiveBattle { get; private set; }

    /// <summary>
    /// Gets the authored map used by the active battle.
    /// </summary>
    public BattleMap BattleMap { get; private set; }

    /// <summary>
    /// Gets the camera owned by the tactical scene.
    /// </summary>
    public Camera BattleCamera => battleCamera;

    /// <summary>
    /// Gets the transform under which tactical presentation objects are created.
    /// </summary>
    public Transform BattleRoot => battleRoot;

    /// <summary>
    /// Verifies the authored tactical-scene references.
    /// </summary>
    private void Awake()
    {
        if (battleCamera == null)
            throw new MissingReferenceException($"{name} is missing its battle camera.");
        if (battleRoot == null)
            throw new MissingReferenceException($"{name} is missing its battle root.");
    }

    /// <summary>
    /// Resolves the active tactical battle after the scene becomes active.
    /// </summary>
    private void Start()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        GameRoot game = runtime?.GetActiveGame();
        if (game == null)
            throw new InvalidOperationException("TacticalView requires an active game.");

        ActiveBattle activeBattle = game.GetActiveBattle();
        if (activeBattle == null)
            throw new InvalidOperationException("TacticalView requires an active battle.");

        GameDataCatalog gameData = bootstrap.GetContentPack().GameData;
        if (!gameData.TryGetBattleMap(activeBattle.BattleMapInstanceID, out BattleMap battleMap))
        {
            throw new InvalidOperationException(
                $"Active battle map '{activeBattle.BattleMapInstanceID}' is not loaded."
            );
        }

        Initialize(activeBattle, battleMap);
    }

    /// <summary>
    /// Binds one active battle to its matching authored map.
    /// </summary>
    /// <param name="activeBattle">The persistent tactical-battle state.</param>
    /// <param name="battleMap">The authored map selected for the battle.</param>
    public void Initialize(ActiveBattle activeBattle, BattleMap battleMap)
    {
        if (activeBattle == null)
            throw new ArgumentNullException(nameof(activeBattle));
        if (battleMap == null)
            throw new ArgumentNullException(nameof(battleMap));
        if (
            !string.Equals(
                activeBattle.BattleMapInstanceID,
                battleMap.InstanceID,
                StringComparison.Ordinal
            )
        )
        {
            throw new ArgumentException(
                "The authored map does not match the active battle map identifier.",
                nameof(battleMap)
            );
        }
        if (activeBattle.Kind != battleMap.Kind)
        {
            throw new ArgumentException(
                "The authored map kind does not match the active battle kind.",
                nameof(battleMap)
            );
        }

        ActiveBattle = activeBattle;
        BattleMap = battleMap;
    }
}
