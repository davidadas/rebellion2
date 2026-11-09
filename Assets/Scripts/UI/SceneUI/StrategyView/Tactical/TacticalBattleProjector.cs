using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Converts tactical battle state into source-space presentation data.
/// </summary>
internal sealed class TacticalBattleProjector
{
    internal const string StarfieldTexturePath = "Application/MainMenu/UI/starfield";
    internal const int MaximumPersonnelPortraits = 4;

    private const float _withdrawingAlpha = 0.45f;
    private const float _overlayExtentFraction = 0.8f;
    private const float _capitalShotThickness = 2f;
    private const float _fighterShotThickness = 1f;
    private const float _torpedoShotThickness = 3f;
    private const float _minimumBoltLength = 2f;
    private const float _shotBrightening = 0.25f;
    private const int _planetSizeDivisor = 3;
    private static readonly Color _friendlyColor = new Color(0.35f, 0.9f, 0.35f, 1f);
    private static readonly Color _enemyColor = new Color(0.95f, 0.35f, 0.35f, 1f);
    private static readonly Color _friendlyShotColor = new Color(0.55f, 1f, 0.55f, 1f);
    private static readonly Color _enemyShotColor = new Color(1f, 0.5f, 0.45f, 1f);
    private static readonly Color _ionShotColor = new Color(0.55f, 0.8f, 1f, 1f);
    private static readonly Color _torpedoShotColor = new Color(0.78f, 0.78f, 0.8f, 1f);
    private static readonly PrimaryWeaponArc[] _panelArcs =
    {
        PrimaryWeaponArc.Fore,
        PrimaryWeaponArc.Aft,
        PrimaryWeaponArc.Port,
        PrimaryWeaponArc.Starboard,
    };

    private readonly Dictionary<string, Texture2D> _portraitTextures =
        new Dictionary<string, Texture2D>();
    private Texture2D _starfieldTexture;
    private bool _starfieldResolved;
    private Texture2D _planetTexture;
    private Planet _planetTextureSource;

    /// <summary>
    /// Projects one frame of the battle.
    /// </summary>
    /// <param name="battle">The battle to present.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="scene">The 3D scene whose camera projects battle positions.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="planet">The battle planet.</param>
    /// <param name="attackerFactionId">The attacking faction's instance identifier.</param>
    /// <param name="defenderFactionId">The defending faction's instance identifier.</param>
    /// <param name="attackerName">The attacking faction's display name.</param>
    /// <param name="defenderName">The defending faction's display name.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The unit shown in the information panel, or null.</param>
    /// <param name="selectionBox">The source-space drag-selection bounds, when dragging.</param>
    /// <param name="paused">Whether playback is paused.</param>
    /// <param name="speed">The playback speed multiplier.</param>
    /// <param name="rosterSide">The side listed in the roster bar.</param>
    /// <param name="rosterClassIndex">The chosen fighter class in the roster bar.</param>
    /// <returns>The complete frame presentation.</returns>
    internal TacticalBattleRenderData Project(
        TacticalSpaceBattle battle,
        RectInt arena,
        TacticalBattleScene scene,
        UIContext uiContext,
        Planet planet,
        string attackerFactionId,
        string defenderFactionId,
        string attackerName,
        string defenderName,
        IReadOnlyCollection<TacticalBattleUnit> selection,
        TacticalBattleUnit focus,
        RectInt? selectionBox,
        bool paused,
        float speed,
        CombatSide rosterSide,
        int rosterClassIndex
    )
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));
        if (scene == null)
            throw new ArgumentNullException(nameof(scene));

        Color attackerBarColor = GetSideColor(
            battle,
            CombatSide.Attacker,
            uiContext,
            attackerFactionId
        );
        Color defenderBarColor = GetSideColor(
            battle,
            CombatSide.Defender,
            uiContext,
            defenderFactionId
        );
        List<TacticalUnitRenderData> units = new List<TacticalUnitRenderData>(battle.Units.Count);
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            // Fighter squadrons carry no overlay: they cannot be commanded and their models stay
            // readable without bars.
            if (!unit.IsAlive || unit.HasWithdrawn || unit.IsStarfighter)
                continue;
            if (!scene.TryProjectUnit(unit, arena, out Vector2 point, out float diameter))
                continue;

            units.Add(
                ProjectUnit(
                    unit,
                    arena,
                    point,
                    diameter,
                    selection,
                    unit.Side == CombatSide.Attacker ? attackerBarColor : defenderBarColor
                )
            );
        }

        Color attackerShotColor = GetShotColor(
            battle,
            CombatSide.Attacker,
            uiContext,
            attackerFactionId
        );
        Color defenderShotColor = GetShotColor(
            battle,
            CombatSide.Defender,
            uiContext,
            defenderFactionId
        );
        List<TacticalShotRenderData> shots = new List<TacticalShotRenderData>(battle.Shots.Count);
        foreach (TacticalShot shot in battle.Shots)
        {
            TacticalShotRenderData shotData = ProjectShot(
                scene,
                shot,
                arena,
                shot.Side == CombatSide.Attacker ? attackerShotColor : defenderShotColor
            );
            if (shotData != null)
                shots.Add(shotData);
        }

        CombatSide playerSide = battle.PlayerSide ?? CombatSide.Attacker;
        bool canCommand = battle.PlayerSide != null && !battle.IsComplete;
        bool withdrawing = battle.HasOrderedWithdrawal(playerSide);
        Texture2D planetTexture = GetPlanetTexture(planet, uiContext);
        int planetSize = Mathf.Max(Mathf.Min(arena.width, arena.height) / _planetSizeDivisor, 1);
        string attackerLabel = GetFactionLabel(
            attackerName,
            attackerFactionId,
            CombatSide.Attacker
        );
        string defenderLabel = GetFactionLabel(
            defenderName,
            defenderFactionId,
            CombatSide.Defender
        );

        return new TacticalBattleRenderData(
            $"Battle at {planet?.GetDisplayName() ?? "Deep Space"}",
            DescribeForce(battle, CombatSide.Attacker, attackerLabel),
            DescribeForce(battle, CombatSide.Defender, defenderLabel),
            BuildClock(battle, paused),
            BuildHint(battle, selection, focus),
            focus?.IsAlive != true || focus.HasWithdrawn
                ? null
                : ProjectPanel(battle, focus, uiContext, attackerLabel, defenderLabel),
            withdrawing ? "Retreating" : "Retreat",
            canCommand && !withdrawing && battle.CanWithdraw(playerSide),
            battle.PlayerSide != null && battle.IsAutomatic(playerSide) ? "Auto: On" : "Auto: Off",
            canCommand && !withdrawing,
            paused ? "Resume" : "Pause",
            $"Speed {speed:0.#}x",
            GetStarfieldTexture(uiContext),
            planetTexture,
            new RectInt(
                arena.width / 2 - planetSize / 2,
                arena.height / 2 - planetSize / 2,
                planetSize,
                planetSize
            ),
            units,
            shots,
            selectionBox == null ? null : ToArenaRect(selectionBox.Value, arena),
            ProjectRoster(
                battle,
                uiContext,
                rosterSide,
                rosterClassIndex,
                selection,
                focus,
                rosterSide == CombatSide.Attacker ? attackerLabel : defenderLabel
            )
        );
    }

    /// <summary>
    /// Returns one side's capital ships in roster order.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side to list.</param>
    /// <returns>The side's capital ship units, including destroyed and withdrawn ones.</returns>
    internal static List<TacticalCapitalShipUnit> GetRosterShips(
        TacticalSpaceBattle battle,
        CombatSide side
    )
    {
        List<TacticalCapitalShipUnit> ships = new List<TacticalCapitalShipUnit>();
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            if (unit.Side == side && unit is TacticalCapitalShipUnit ship)
                ships.Add(ship);
        }

        return ships;
    }

    /// <summary>
    /// Groups one side's squadrons by fighter type, in first-seen order.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side to list.</param>
    /// <returns>The fighter classes, each holding its squadrons in roster order.</returns>
    internal static List<List<TacticalStarfighterUnit>> GetFighterClasses(
        TacticalSpaceBattle battle,
        CombatSide side
    )
    {
        List<List<TacticalStarfighterUnit>> classes = new List<List<TacticalStarfighterUnit>>();
        Dictionary<string, List<TacticalStarfighterUnit>> byType =
            new Dictionary<string, List<TacticalStarfighterUnit>>();
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            if (unit.Side != side || unit is not TacticalStarfighterUnit squadron)
                continue;

            string key = squadron.Fighter.TypeID ?? squadron.DisplayName ?? string.Empty;
            if (!byType.TryGetValue(key, out List<TacticalStarfighterUnit> members))
            {
                members = new List<TacticalStarfighterUnit>();
                byType.Add(key, members);
                classes.Add(members);
            }

            members.Add(squadron);
        }

        return classes;
    }

    /// <summary>
    /// Builds the roster bar for one side.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="side">The side to list.</param>
    /// <param name="classIndex">The chosen fighter class, wrapped into range.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The inspected unit, or null.</param>
    /// <param name="sideLabel">The side's display name.</param>
    /// <returns>The roster presentation.</returns>
    private TacticalRosterRenderData ProjectRoster(
        TacticalSpaceBattle battle,
        UIContext uiContext,
        CombatSide side,
        int classIndex,
        IReadOnlyCollection<TacticalBattleUnit> selection,
        TacticalBattleUnit focus,
        string sideLabel
    )
    {
        List<TacticalRosterSlotRenderData> ships = new List<TacticalRosterSlotRenderData>();
        List<TacticalCapitalShipUnit> rosterShips = GetRosterShips(battle, side);
        for (int index = 0; index < rosterShips.Count; index++)
        {
            TacticalCapitalShipUnit ship = rosterShips[index];
            ships.Add(
                new TacticalRosterSlotRenderData(
                    (index + 1).ToString(),
                    GetPortrait(ship.Node, uiContext),
                    ReferenceEquals(ship, focus) || selection?.Contains(ship) == true,
                    ship.IsAlive && !ship.HasWithdrawn
                )
            );
        }

        List<List<TacticalStarfighterUnit>> classes = GetFighterClasses(battle, side);
        List<TacticalRosterSlotRenderData> squadrons = new List<TacticalRosterSlotRenderData>();
        string classLabel = "No fighters";
        if (classes.Count > 0)
        {
            List<TacticalStarfighterUnit> members = classes[
                ((classIndex % classes.Count) + classes.Count) % classes.Count
            ];
            classLabel = members[0].DisplayName ?? "Fighters";
            foreach (TacticalStarfighterUnit squadron in members)
            {
                squadrons.Add(
                    new TacticalRosterSlotRenderData(
                        squadron.CurrentSquadronSize.ToString(),
                        GetPortrait(squadron.Node, uiContext),
                        ReferenceEquals(squadron, focus),
                        squadron.IsAlive && !squadron.HasWithdrawn
                    )
                );
            }
        }

        return new TacticalRosterRenderData(
            sideLabel,
            ships,
            classLabel,
            classes.Count > 1,
            squadrons
        );
    }

    /// <summary>
    /// Returns the conventional-weapon color for one side, taken from its faction theme.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The firing side.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="factionId">The side's faction instance identifier.</param>
    /// <returns>The shot color.</returns>
    internal static Color GetShotColor(
        TacticalSpaceBattle battle,
        CombatSide side,
        UIContext uiContext,
        string factionId
    )
    {
        if (uiContext != null && !string.IsNullOrEmpty(factionId))
        {
            Color factionColor = uiContext.ResolveFactionColor(factionId);
            Color shotColor = Color.Lerp(factionColor, Color.white, _shotBrightening);
            shotColor.a = 1f;
            return shotColor;
        }

        return battle.PlayerSide == side ? _friendlyShotColor : _enemyShotColor;
    }

    /// <summary>
    /// Returns the hull-bar color for one side: its faction theme color, so Alliance bars read
    /// red and Imperial bars green whichever side the player commands.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="factionId">The side's faction instance identifier.</param>
    /// <returns>The bar color.</returns>
    internal static Color GetSideColor(
        TacticalSpaceBattle battle,
        CombatSide side,
        UIContext uiContext,
        string factionId
    )
    {
        if (uiContext != null && !string.IsNullOrEmpty(factionId))
        {
            Color color = uiContext.ResolveFactionColor(factionId);
            color.a = 1f;
            return color;
        }

        return battle.PlayerSide == side ? _friendlyColor : _enemyColor;
    }

    /// <summary>
    /// Returns a shot's color: grey for torpedoes, blue for ion fire, and the firing side's
    /// faction color for everything else.
    /// </summary>
    /// <param name="shot">The shot to color.</param>
    /// <param name="conventionalColor">The firing side's conventional-weapon color.</param>
    /// <returns>The bolt color.</returns>
    internal static Color GetBoltColor(TacticalShot shot, Color conventionalColor)
    {
        if (shot.IsTorpedo)
            return _torpedoShotColor;
        return shot.IsIon ? _ionShotColor : conventionalColor;
    }

    /// <summary>
    /// Builds the information panel for an inspected unit.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="unit">The inspected unit.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="attackerLabel">The attacking faction label.</param>
    /// <param name="defenderLabel">The defending faction label.</param>
    /// <returns>The panel presentation.</returns>
    internal TacticalPanelRenderData ProjectPanel(
        TacticalSpaceBattle battle,
        TacticalBattleUnit unit,
        UIContext uiContext,
        string attackerLabel,
        string defenderLabel
    )
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        string factionLabel = unit.Side == CombatSide.Attacker ? attackerLabel : defenderLabel;
        Texture2D portrait = GetPortrait(unit.Node, uiContext);
        string tactics = DescribeTactics(battle, unit);
        if (unit is TacticalStarfighterUnit fighter)
        {
            int maximumSquadron = Math.Max(fighter.Fighter.MaxSquadronSize, 1);
            return new TacticalPanelRenderData(
                fighter.DisplayName,
                $"{factionLabel} squadron",
                portrait,
                showShields: fighter.Fighter.ShieldStrength > 0,
                fighter.Fighter.ShieldStrength > 0 ? 1f : 0f,
                fighter.Fighter.ShieldStrength > 0
                    ? $"{fighter.Fighter.ShieldStrength} per fighter"
                    : "—",
                fighter.CurrentSquadronSize / (float)maximumSquadron,
                $"{fighter.CurrentSquadronSize} / {maximumSquadron} fighters",
                Array.Empty<TacticalSystemRenderData>(),
                Array.Empty<float>(),
                Array.Empty<TacticalPersonnelRenderData>(),
                "No personnel aboard",
                DescribeFighterOrders(fighter),
                tactics,
                DescribeTargetPriority(unit),
                battle.CanSetTargetPriority(unit)
            );
        }

        TacticalCapitalShipUnit ship = (TacticalCapitalShipUnit)unit;
        Fleet fleet = ship.Ship.GetParentOfType<Fleet>();
        List<TacticalPersonnelRenderData> personnel = BuildPersonnel(ship.Ship, uiContext);
        return new TacticalPanelRenderData(
            ship.DisplayName,
            fleet?.GetDisplayName() ?? factionLabel,
            portrait,
            showShields: ship.MaximumShields > 0,
            ship.MaximumShields > 0 ? (float)(ship.CurrentShields / ship.MaximumShields) : 0f,
            DescribeShields(ship),
            (float)(ship.CurrentHull / Math.Max(ship.MaximumHull, 1)),
            $"{Mathf.RoundToInt((float)ship.CurrentHull)} / {Mathf.RoundToInt((float)ship.MaximumHull)}",
            BuildSystems(ship),
            _panelArcs.Select(arc => (float)ship.GetArcChargeFraction(arc)).ToArray(),
            personnel,
            DescribePersonnel(ship.Ship),
            DescribeShipOrders(ship),
            tactics,
            DescribeTargetPriority(unit),
            battle.CanSetTargetPriority(unit)
        );
    }

    /// <summary>
    /// Describes which enemy class a unit engages first.
    /// </summary>
    /// <param name="unit">The inspected unit.</param>
    /// <returns>The target priority caption.</returns>
    internal static string DescribeTargetPriority(TacticalBattleUnit unit)
    {
        return unit.TargetPriority == TacticalTargetPriority.Starfighters
            ? "Targets: Fighters first"
            : "Targets: Capital ships first";
    }

    /// <summary>
    /// Projects one unit's overlay: the selection ring and status bars drawn over its 3D model.
    /// </summary>
    /// <param name="unit">The unit to present.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="point">The unit's projected source-space center.</param>
    /// <param name="diameter">The unit's projected source-space diameter.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="barColor">The unit's faction color for its hull bar.</param>
    /// <returns>The unit presentation.</returns>
    private static TacticalUnitRenderData ProjectUnit(
        TacticalBattleUnit unit,
        RectInt arena,
        Vector2 point,
        float diameter,
        IReadOnlyCollection<TacticalBattleUnit> selection,
        Color barColor
    )
    {
        float width = Mathf.Max(diameter * _overlayExtentFraction, 4f);
        float hullFraction = 1f;
        float shieldFraction = 0f;
        bool showShields = false;
        if (unit is TacticalCapitalShipUnit ship)
        {
            hullFraction = (float)(ship.CurrentHull / Math.Max(ship.MaximumHull, 1));
            showShields = ship.MaximumShields > 0;
            shieldFraction = showShields ? (float)(ship.CurrentShields / ship.MaximumShields) : 0f;
        }
        else if (unit is TacticalStarfighterUnit fighter)
        {
            hullFraction =
                fighter.CurrentSquadronSize / (float)Math.Max(fighter.InitialSquadronSize, 1);
        }

        return new TacticalUnitRenderData(
            point.x - arena.x,
            point.y - arena.y,
            width,
            width,
            (float)(unit.Heading * Mathf.Rad2Deg),
            null,
            hullFraction,
            shieldFraction,
            showShields,
            selection?.Contains(unit) == true,
            barColor,
            unit.IsWithdrawing ? _withdrawingAlpha : 1f
        );
    }

    /// <summary>
    /// Projects one weapon bolt: a short segment that has flown from the muzzle toward the target
    /// in proportion to the shot's age.
    /// </summary>
    /// <param name="scene">The 3D scene whose camera projects battle positions.</param>
    /// <param name="shot">The shot to present.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="conventionalColor">The firing side's conventional-weapon color.</param>
    /// <returns>The shot presentation, or null when the shot lies behind the camera.</returns>
    private static TacticalShotRenderData ProjectShot(
        TacticalBattleScene scene,
        TacticalShot shot,
        RectInt arena,
        Color conventionalColor
    )
    {
        double dx = shot.EndX - shot.StartX;
        double dy = shot.EndY - shot.StartY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= 0)
            return null;

        // The bolt's head leads by the shot's progress; its tail never trails behind the muzzle.
        double head = distance * shot.Progress;
        double tail = Math.Max(head - shot.BoltLength, 0);
        if (
            !scene.TryProject(
                shot.StartX + dx / distance * tail,
                shot.StartY + dy / distance * tail,
                arena,
                out Vector2 start
            )
            || !scene.TryProject(
                shot.StartX + dx / distance * head,
                shot.StartY + dy / distance * head,
                arena,
                out Vector2 end
            )
        )
            return null;

        Vector2 delta = end - start;
        return new TacticalShotRenderData(
            (start.x + end.x) / 2f - arena.x,
            (start.y + end.y) / 2f - arena.y,
            Mathf.Max(delta.magnitude, _minimumBoltLength),
            shot.IsTorpedo ? _torpedoShotThickness
                : shot.IsStarfighterShot ? _fighterShotThickness
                : _capitalShotThickness,
            Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg,
            GetBoltColor(shot, conventionalColor)
        );
    }

    /// <summary>
    /// Describes a ship's shield level, noting a knocked-out generator.
    /// </summary>
    /// <param name="ship">The inspected capital ship.</param>
    /// <returns>The shield text.</returns>
    private static string DescribeShields(TacticalCapitalShipUnit ship)
    {
        if (ship.MaximumShields <= 0)
            return "None";

        string level =
            $"{Mathf.RoundToInt((float)ship.CurrentShields)} / {Mathf.RoundToInt((float)ship.MaximumShields)}";
        return ship.ShieldGeneratorOffline ? $"{level} — generator down" : level;
    }

    /// <summary>
    /// Builds the ship system indicators.
    /// </summary>
    /// <param name="ship">The inspected capital ship.</param>
    /// <returns>The system indicators in panel order.</returns>
    private static IReadOnlyList<TacticalSystemRenderData> BuildSystems(
        TacticalCapitalShipUnit ship
    )
    {
        TacticalSystemState shields =
            ship.MaximumShields <= 0 ? TacticalSystemState.Absent
            : ship.CurrentShields > 0 && !ship.ShieldGeneratorOffline ? TacticalSystemState.Online
            : TacticalSystemState.Offline;
        TacticalSystemState weapons =
            !ship.IsArmed ? TacticalSystemState.Absent
            : ship.WeaponsOffline ? TacticalSystemState.Offline
            : TacticalSystemState.Online;
        return new[]
        {
            new TacticalSystemRenderData("Shields", shields),
            new TacticalSystemRenderData(
                "Engines",
                ship.EnginesOffline ? TacticalSystemState.Offline : TacticalSystemState.Online
            ),
            new TacticalSystemRenderData("Weapons", weapons),
            new TacticalSystemRenderData(
                "Hyperdrive",
                ship.HasHyperdrive ? TacticalSystemState.Online : TacticalSystemState.Absent
            ),
        };
    }

    /// <summary>
    /// Collects the officers, special forces, and regiments carried by a ship.
    /// </summary>
    /// <param name="ship">The capital ship.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The personnel entries, limited to the portraits the panel can show.</returns>
    private List<TacticalPersonnelRenderData> BuildPersonnel(CapitalShip ship, UIContext uiContext)
    {
        IEnumerable<ISceneNode> aboard = ship.GetChildren<Officer>()
            .Cast<ISceneNode>()
            .Concat(ship.GetChildren<SpecialForces>())
            .Concat(ship.GetChildren<Regiment>());
        List<TacticalPersonnelRenderData> personnel = new List<TacticalPersonnelRenderData>();
        foreach (ISceneNode node in aboard)
        {
            if (personnel.Count >= MaximumPersonnelPortraits)
                break;

            personnel.Add(
                new TacticalPersonnelRenderData(GetPortrait(node, uiContext), node.GetDisplayName())
            );
        }

        return personnel;
    }

    /// <summary>
    /// Summarizes the personnel carried by a ship.
    /// </summary>
    /// <param name="ship">The capital ship.</param>
    /// <returns>The personnel summary.</returns>
    private static string DescribePersonnel(CapitalShip ship)
    {
        int officers = ship.GetChildren<Officer>().Count;
        int specialForces = ship.GetChildren<SpecialForces>().Count;
        int regiments = ship.GetChildren<Regiment>().Count;
        List<string> parts = new List<string>();
        if (officers > 0)
            parts.Add(officers == 1 ? "1 officer" : $"{officers} officers");
        if (specialForces > 0)
            parts.Add(
                specialForces == 1
                    ? "1 special forces unit"
                    : $"{specialForces} special forces units"
            );
        if (regiments > 0)
            parts.Add(regiments == 1 ? "1 regiment" : $"{regiments} regiments");
        return parts.Count == 0 ? "No personnel aboard" : string.Join(", ", parts);
    }

    /// <summary>
    /// Describes a capital ship's current orders.
    /// </summary>
    /// <param name="ship">The inspected capital ship.</param>
    /// <returns>The orders text.</returns>
    private static string DescribeShipOrders(TacticalCapitalShipUnit ship)
    {
        if (ship.IsWithdrawing)
            return "Orders: Withdraw to hyperspace";

        return ship.Order switch
        {
            TacticalShipOrder.Move => "Orders: Move to position",
            TacticalShipOrder.Attack => $"Orders: Attack {ship.Target?.DisplayName}",
            _ => ship.Target == null
                ? "Orders: None, holding position"
                : $"Orders: None, engaging {ship.Target.DisplayName}",
        };
    }

    /// <summary>
    /// Describes a fighter squadron's current behavior.
    /// </summary>
    /// <param name="fighter">The inspected squadron.</param>
    /// <returns>The orders text.</returns>
    private static string DescribeFighterOrders(TacticalStarfighterUnit fighter)
    {
        if (fighter.IsWithdrawing)
            return "Orders: Return and withdraw";

        return fighter.Target == null
            ? "Orders: Patrolling"
            : $"Orders: Engaging {fighter.Target.DisplayName}";
    }

    /// <summary>
    /// Describes who controls a unit.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="unit">The inspected unit.</param>
    /// <returns>The tactics text.</returns>
    private static string DescribeTactics(TacticalSpaceBattle battle, TacticalBattleUnit unit)
    {
        if (battle.PlayerSide == null || unit.Side != battle.PlayerSide.Value)
            return "Tactics: Enemy vessel";
        if (unit.IsStarfighter)
            return "Tactics: Squadron acts independently";
        return battle.IsAutomatic(unit.Side) ? "Tactics: Automatic" : "Tactics: Manual command";
    }

    /// <summary>
    /// Returns the starfield texture, resolving it at most once.
    /// </summary>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The starfield texture, or null when unavailable.</returns>
    private Texture2D GetStarfieldTexture(UIContext uiContext)
    {
        if (_starfieldResolved || uiContext == null)
            return _starfieldTexture;

        _starfieldTexture = uiContext.GetTexture(StarfieldTexturePath);
        _starfieldResolved = true;
        return _starfieldTexture;
    }

    /// <summary>
    /// Returns the planet backdrop texture, resolving it once per planet.
    /// </summary>
    /// <param name="planet">The battle planet.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The planet texture, or null when unavailable.</returns>
    private Texture2D GetPlanetTexture(Planet planet, UIContext uiContext)
    {
        if (planet == null || uiContext == null)
            return null;

        if (!ReferenceEquals(planet, _planetTextureSource))
        {
            _planetTextureSource = planet;
            _planetTexture = uiContext.GetPlanetTexture(planet);
        }

        return _planetTexture;
    }

    /// <summary>
    /// Returns the cached compact portrait for a scene node.
    /// </summary>
    /// <param name="node">The node to portray.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <returns>The portrait texture, or null when unavailable.</returns>
    private Texture2D GetPortrait(ISceneNode node, UIContext uiContext)
    {
        return GetCachedTexture(node, uiContext, small: true, _portraitTextures);
    }

    /// <summary>
    /// Resolves a node's display texture once and caches it by instance identifier.
    /// </summary>
    /// <param name="node">The node to draw.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="small">Whether compact artwork is preferred.</param>
    /// <param name="cache">The cache keyed by instance identifier.</param>
    /// <returns>The texture, or null when unavailable.</returns>
    private static Texture2D GetCachedTexture(
        ISceneNode node,
        UIContext uiContext,
        bool small,
        Dictionary<string, Texture2D> cache
    )
    {
        string instanceId = node?.GetInstanceID();
        if (string.IsNullOrEmpty(instanceId) || uiContext == null)
            return null;

        if (cache.TryGetValue(instanceId, out Texture2D texture))
            return texture;

        texture = uiContext.GetEntityTexture(node, small);
        cache[instanceId] = texture;
        return texture;
    }

    /// <summary>
    /// Builds the elapsed-time line.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="paused">Whether playback is paused.</param>
    /// <returns>The clock text.</returns>
    private static string BuildClock(TacticalSpaceBattle battle, bool paused)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(battle.ElapsedSeconds);
        string clock = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        if (battle.IsComplete)
            return $"{clock}   Battle over";
        return paused ? $"{clock}   PAUSED" : clock;
    }

    /// <summary>
    /// Returns a faction label, falling back to the identifier or side when no name is known.
    /// </summary>
    /// <param name="name">The faction display name.</param>
    /// <param name="factionId">The faction instance identifier.</param>
    /// <param name="side">The side the faction fights on.</param>
    /// <returns>The label.</returns>
    private static string GetFactionLabel(string name, string factionId, CombatSide side)
    {
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        if (!string.IsNullOrWhiteSpace(factionId))
            return factionId;
        return side == CombatSide.Attacker ? "Attacker" : "Defender";
    }

    /// <summary>
    /// Describes one side's remaining forces.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side to describe.</param>
    /// <param name="label">The side's faction label.</param>
    /// <returns>The side description.</returns>
    private static string DescribeForce(TacticalSpaceBattle battle, CombatSide side, string label)
    {
        int strength = Mathf.RoundToInt((float)battle.GetStrengthFraction(side) * 100f);
        return $"{label}: {battle.CountShips(side)} ships, {battle.CountFighters(side)} squadrons ({strength}%)";
    }

    /// <summary>
    /// Builds the control hint shown under the panel.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The inspected unit.</param>
    /// <returns>The hint text.</returns>
    private static string BuildHint(
        TacticalSpaceBattle battle,
        IReadOnlyCollection<TacticalBattleUnit> selection,
        TacticalBattleUnit focus
    )
    {
        if (battle.IsComplete)
            return "The engagement has ended.";
        if (battle.PlayerSide == null)
            return "Both sides are under automatic control.";
        if (battle.IsAutomatic(battle.PlayerSide.Value))
            return "Your forces are under automatic control.";
        if (selection?.Count > 1)
            return $"{selection.Count} ships selected. Right-click to move or attack.";
        if (selection?.Count == 1)
            return "Right-click to move or attack.";
        if (focus != null)
            return "Click one of your ships to command it. Tab cycles your ships.";
        return "Click a ship to inspect it. Drag to select several. Wheel zooms, right-drag orbits, middle-drag or WASD pans, Home reframes, Space pauses.";
    }

    /// <summary>
    /// Converts surface-space bounds into arena-relative bounds.
    /// </summary>
    /// <param name="bounds">The surface-space bounds.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <returns>The arena-relative bounds.</returns>
    private static RectInt ToArenaRect(RectInt bounds, RectInt arena)
    {
        return new RectInt(bounds.x - arena.x, bounds.y - arena.y, bounds.width, bounds.height);
    }
}
