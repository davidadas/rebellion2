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

    /// <summary>
    /// The tip added to the hint while units are selected.
    /// </summary>
    internal const string SelectionTip =
        "Ctrl-click adds a unit; Shift-click in the roster takes a run.";
    private const float _capitalShotThickness = 2f;
    private const float _fighterShotThickness = 1f;
    private const float _torpedoShotThickness = 3f;
    private const float _minimumCapitalShotThickness = 1.5f;
    private const float _maximumCapitalShotThickness = 7f;
    private const float _boltStrengthPerPixel = 25f;
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
    /// <param name="deploying">Whether the battle is holding while hull models load.</param>
    /// <param name="superlaserArmed">Whether the player is choosing a superlaser target.</param>
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
        bool deploying = false,
        bool superlaserArmed = false
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
            // Fighter squadrons carry an overlay only while selected, so their models stay
            // readable without bars.
            if (
                !unit.IsAlive
                || unit.HasWithdrawn
                || unit.IsOffField
                || (unit.IsStarfighter && selection?.Contains(unit) != true)
            )
                continue;
            if (!scene.TryProjectUnit(unit, arena, out Vector2 point, out float diameter))
                continue;

            units.Add(
                ProjectUnit(
                    unit,
                    arena,
                    point,
                    diameter,
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
        TacticalWithdrawalStatus withdrawalStatus = battle.GetWithdrawalStatus(playerSide);
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
            BuildClock(battle, paused, deploying),
            BuildHint(battle, selection, focus, superlaserArmed),
            focus?.IsAlive != true || focus.HasWithdrawn
                ? null
                : ProjectPanel(battle, focus, uiContext, attackerLabel, defenderLabel),
            GetRetreatCaption(battle.IsComplete, withdrawing, withdrawalStatus),
            canCommand && !withdrawing && withdrawalStatus == TacticalWithdrawalStatus.Available,
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
                selection,
                focus,
                rosterSide == CombatSide.Attacker ? attackerLabel : defenderLabel
            ),
            ProjectSuperlaser(battle, superlaserArmed),
            CanRecoverAny(battle, selection)
        );
    }

    /// <summary>
    /// Returns whether a recover order would send any selected squadron to a hangar.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="selection">The selected units.</param>
    /// <returns>True when at least one selected squadron can be recalled.</returns>
    private static bool CanRecoverAny(
        TacticalSpaceBattle battle,
        IReadOnlyCollection<TacticalBattleUnit> selection
    )
    {
        if (selection == null)
            return false;

        foreach (TacticalBattleUnit unit in selection)
        {
            if (battle.CanOrderRecovery(unit))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the superlaser charge bar for the player's battle station.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="armed">Whether the player is choosing a target.</param>
    /// <returns>The bar presentation, or null when the player has no station in the battle.</returns>
    internal static TacticalSuperlaserRenderData ProjectSuperlaser(
        TacticalSpaceBattle battle,
        bool armed
    )
    {
        if (battle.PlayerSide == null)
            return null;

        TacticalBattleStation station = battle.GetStation(battle.PlayerSide.Value);
        if (station == null)
            return null;

        bool ready = station.IsSuperlaserReady && battle.CanCommandStation(station);
        bool choosing = armed && ready;
        string label;
        if (station.StrikeTarget != null)
            label = "Superlaser firing";
        else if (station.IsWithdrawing)
            label = "Superlaser: jumping out";
        else if (choosing)
            label = "Superlaser: pick a target";
        else if (ready)
            label = "Superlaser ready: click to aim";
        else if (station.IsSuperlaserReady)
            label = "Superlaser charged";
        else
            label =
                $"Superlaser charging {Mathf.FloorToInt((float)station.SuperlaserCharge * 100f)}%";
        return new TacticalSuperlaserRenderData(
            label,
            (float)station.SuperlaserCharge,
            ready,
            choosing
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
    /// Returns one side's squadrons in roster order: grouped by fighter type, each group in
    /// first-seen order. Roster squadron slots index this list.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side to list.</param>
    /// <returns>The side's squadrons, including destroyed and withdrawn ones.</returns>
    internal static List<TacticalStarfighterUnit> GetRosterSquadrons(
        TacticalSpaceBattle battle,
        CombatSide side
    )
    {
        List<TacticalStarfighterUnit> squadrons = new List<TacticalStarfighterUnit>();
        foreach (List<TacticalStarfighterUnit> members in GetFighterClasses(battle, side))
            squadrons.AddRange(members);
        return squadrons;
    }

    /// <summary>
    /// Builds the roster bar for one side: its capital ships, then every squadron grouped by
    /// fighter type.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="side">The side to list.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The inspected unit, or null.</param>
    /// <param name="sideLabel">The side's display name.</param>
    /// <returns>The roster presentation.</returns>
    private TacticalRosterRenderData ProjectRoster(
        TacticalSpaceBattle battle,
        UIContext uiContext,
        CombatSide side,
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

        List<IReadOnlyList<TacticalRosterSlotRenderData>> squadronGroups =
            new List<IReadOnlyList<TacticalRosterSlotRenderData>>();
        foreach (List<TacticalStarfighterUnit> members in GetFighterClasses(battle, side))
        {
            List<TacticalRosterSlotRenderData> group = new List<TacticalRosterSlotRenderData>(
                members.Count
            );
            foreach (TacticalStarfighterUnit squadron in members)
            {
                group.Add(
                    new TacticalRosterSlotRenderData(
                        squadron.CurrentSquadronSize.ToString(),
                        GetPortrait(squadron.Node, uiContext),
                        ReferenceEquals(squadron, focus) || selection?.Contains(squadron) == true,
                        // A squadron aboard a ship is out of the fight: its slot leaves the
                        // live look for the hangar tint and keeps its count, where a lost
                        // squadron's goes grey and reads zero.
                        squadron.IsAlive
                            && !squadron.HasWithdrawn
                            && !squadron.IsDocked,
                        squadron.IsAlive && !squadron.HasWithdrawn && squadron.IsDocked
                    )
                );
            }

            squadronGroups.Add(group);
        }

        return new TacticalRosterRenderData(sideLabel, ships, squadronGroups);
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
    /// Returns the color that marks one side's units, on their hull bars and their selection
    /// boxes: its faction theme color, so the Alliance reads red and the Empire green whichever
    /// side the player commands.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="side">The side.</param>
    /// <param name="uiContext">The current strategy UI context.</param>
    /// <param name="factionId">The side's faction instance identifier.</param>
    /// <returns>The side's color.</returns>
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
    /// Returns a bolt's drawn thickness: fighter bolts stay thin, torpedoes heavy, ion fire
    /// fixed, and capital energy bolts grow with the battery behind them so a Star Destroyer's
    /// volley lands with weight, though never below a visible floor.
    /// </summary>
    /// <param name="shot">The shot to draw.</param>
    /// <returns>The source-space line thickness.</returns>
    internal static float GetBoltThickness(TacticalShot shot)
    {
        if (shot.IsTorpedo)
            return _torpedoShotThickness;
        if (shot.IsStarfighterShot)
            return _fighterShotThickness;
        if (shot.IsIon)
            return _capitalShotThickness;
        return Mathf.Clamp(
            _minimumCapitalShotThickness + shot.Strength / _boltStrengthPerPixel,
            _minimumCapitalShotThickness,
            _maximumCapitalShotThickness
        );
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
                BuildFighterWeapons(fighter),
                Array.Empty<float>(),
                Array.Empty<TacticalPersonnelRenderData>(),
                "No personnel aboard",
                DescribeFighterOrders(fighter),
                DescribeFighterWeapons(fighter),
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
    /// Projects one unit's overlay: the status bars drawn over its 3D model.
    /// </summary>
    /// <param name="unit">The unit to present.</param>
    /// <param name="arena">The source-space arena bounds.</param>
    /// <param name="point">The unit's projected source-space center.</param>
    /// <param name="diameter">The unit's projected source-space diameter.</param>
    /// <param name="barColor">The unit's faction color for its hull bar.</param>
    /// <returns>The unit presentation.</returns>
    private static TacticalUnitRenderData ProjectUnit(
        TacticalBattleUnit unit,
        RectInt arena,
        Vector2 point,
        float diameter,
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
        double climb = (shot.EndAltitude - shot.StartAltitude) / distance;
        if (
            !scene.TryProject(
                shot.StartX + dx / distance * tail,
                shot.StartY + dy / distance * tail,
                arena,
                out Vector2 start,
                shot.StartAltitude + climb * tail
            )
            || !scene.TryProject(
                shot.StartX + dx / distance * head,
                shot.StartY + dy / distance * head,
                arena,
                out Vector2 end,
                shot.StartAltitude + climb * head
            )
        )
            return null;

        Vector2 delta = end - start;
        return new TacticalShotRenderData(
            (start.x + end.x) / 2f - arena.x,
            (start.y + end.y) / 2f - arena.y,
            Mathf.Max(delta.magnitude, _minimumBoltLength),
            GetBoltThickness(shot),
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
    /// Fills the system slots with a squadron's weapon lanes: each lane it carries lights up
    /// with its power, and its hyperdrive shows whether it can jump out on its own.
    /// </summary>
    /// <param name="fighter">The inspected squadron.</param>
    /// <returns>The weapon indicators in panel order.</returns>
    private static IReadOnlyList<TacticalSystemRenderData> BuildFighterWeapons(
        TacticalStarfighterUnit fighter
    )
    {
        Starfighter type = fighter.Fighter;
        return new[]
        {
            DescribeFighterLane("Lasers", type.LaserCannon),
            DescribeFighterLane("Ion", type.IonCannon),
            DescribeFighterLane("Torp", type.Torpedoes),
            new TacticalSystemRenderData(
                "Hyperdrive",
                type.Hyperdrive > 0 ? TacticalSystemState.Online : TacticalSystemState.Absent
            ),
        };
    }

    /// <summary>
    /// Describes one squadron weapon lane as a lit indicator carrying its power.
    /// </summary>
    /// <param name="label">The lane name.</param>
    /// <param name="power">The lane's weapon rating.</param>
    /// <returns>The indicator.</returns>
    private static TacticalSystemRenderData DescribeFighterLane(string label, int power)
    {
        return power > 0
            ? new TacticalSystemRenderData($"{label} {power}", TacticalSystemState.Online)
            : new TacticalSystemRenderData(label, TacticalSystemState.Absent);
    }

    /// <summary>
    /// Describes a squadron's weapon ranges and how charged its weapons are.
    /// </summary>
    /// <param name="fighter">The inspected squadron.</param>
    /// <returns>The weapons text.</returns>
    private static string DescribeFighterWeapons(TacticalStarfighterUnit fighter)
    {
        Starfighter type = fighter.Fighter;
        List<string> ranges = new List<string>();
        if (type.LaserCannon > 0)
            ranges.Add($"lasers {type.LaserRange}");
        if (type.IonCannon > 0)
            ranges.Add($"ion {type.IonRange}");
        if (type.Torpedoes > 0)
            ranges.Add($"torpedoes {type.TorpedoRange}");
        if (ranges.Count == 0)
            return "Weapons: None";

        int charge = Mathf.RoundToInt((float)(fighter.WeaponChargeFraction * 100));
        return $"Ranges: {string.Join(", ", ranges)} · {charge}% charged";
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
        if (fighter.IsDocked)
            return $"Orders: Aboard {fighter.RecoveryShip.DisplayName}";
        if (fighter.RecoveryShip != null)
            return fighter.DockingFraction > 0
                ? $"Orders: Docking with {fighter.RecoveryShip.DisplayName}"
                : $"Orders: Return to {fighter.RecoveryShip.DisplayName}";
        if (fighter.IsWithdrawing)
            return fighter.Fighter.Hyperdrive > 0
                ? "Orders: Withdraw to hyperspace"
                : "Orders: Stranded, no hangar has room";
        if (fighter.IsInTrench)
            return $"Orders: In the {fighter.TrenchRunTarget.DisplayName} trench";
        if (fighter.TrenchRunTarget != null)
            return $"Orders: Trench run on {fighter.TrenchRunTarget.DisplayName}";
        if (fighter.ScreenedShip != null)
            return $"Orders: Screening {fighter.ScreenedShip.DisplayName}";

        if (fighter.Target == null)
            return "Orders: Patrolling";
        return fighter.IsDirected
            ? $"Orders: Attack {fighter.Target.DisplayName}"
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
    /// <param name="deploying">Whether the battle is holding while hull models load.</param>
    /// <returns>The clock text.</returns>
    private static string BuildClock(TacticalSpaceBattle battle, bool paused, bool deploying)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(battle.ElapsedSeconds);
        string clock = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        if (battle.IsComplete)
            return $"{clock}   Battle over";
        if (deploying)
            return $"{clock}   Fleets deploying";
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
        int ships = battle.CountShips(side);
        int squadrons = battle.CountFighters(side);
        string shipText = ships == 1 ? "1 ship" : $"{ships} ships";
        string squadronText = squadrons == 1 ? "1 squadron" : $"{squadrons} squadrons";
        // A battle station is named ahead of the fleet it stands behind.
        TacticalBattleStation station = battle.GetStation(side);
        string stationText = station == null ? string.Empty : $"{station.DisplayName}, ";
        return $"{label}: {stationText}{shipText}, {squadronText} ({strength}%)";
    }

    /// <summary>
    /// Builds the control hint shown under the panel.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The inspected unit.</param>
    /// <param name="superlaserArmed">Whether the player is choosing a superlaser target.</param>
    /// <returns>The hint text, led by the reason retreat is unavailable when it is.</returns>
    private static string BuildHint(
        TacticalSpaceBattle battle,
        IReadOnlyCollection<TacticalBattleUnit> selection,
        TacticalBattleUnit focus,
        bool superlaserArmed
    )
    {
        if (superlaserArmed && !battle.IsComplete)
            return "Superlaser armed. Click an enemy capital ship to fire. Right-click cancels.";

        string hint = BuildControlHint(battle, selection, focus);
        if (
            battle.IsComplete
            || battle.PlayerSide == null
            || battle.IsAutomatic(battle.PlayerSide.Value)
            || battle.HasOrderedWithdrawal(battle.PlayerSide.Value)
        )
            return hint;

        // The hint field holds four lines: the reason retreat is blocked comes before the tip
        // about building a selection, which is shown only when there is room for it.
        string block = DescribeWithdrawalBlock(battle.GetWithdrawalStatus(battle.PlayerSide.Value));
        if (block != null)
            return $"{block} {hint}";
        return selection?.Count > 0 ? $"{hint} {SelectionTip}" : hint;
    }

    /// <summary>
    /// Captions the retreat button for the current frame: the withdrawal in progress, the reason
    /// retreat is unavailable, or the plain caption once the battle is over and the reason no
    /// longer means anything.
    /// </summary>
    /// <param name="battleOver">Whether the battle has ended.</param>
    /// <param name="withdrawing">Whether the player has ordered a withdrawal.</param>
    /// <param name="status">The player's withdrawal status.</param>
    /// <returns>The button caption.</returns>
    internal static string GetRetreatCaption(
        bool battleOver,
        bool withdrawing,
        TacticalWithdrawalStatus status
    )
    {
        if (withdrawing)
            return "Retreating";
        return battleOver ? "Retreat" : GetRetreatLabel(status);
    }

    /// <summary>
    /// Names the retreat button for the player's withdrawal status, so a greyed button says why.
    /// </summary>
    /// <param name="status">The player's withdrawal status.</param>
    /// <returns>The button caption.</returns>
    internal static string GetRetreatLabel(TacticalWithdrawalStatus status)
    {
        return status switch
        {
            TacticalWithdrawalStatus.Interdicted => "Interdicted",
            TacticalWithdrawalStatus.NoHyperdrive => "No Hyperdrive",
            TacticalWithdrawalStatus.NoEscapeRoute => "No Escape",
            _ => "Retreat",
        };
    }

    /// <summary>
    /// Explains why retreat is unavailable, or returns null when it is available.
    /// </summary>
    /// <param name="status">The player's withdrawal status.</param>
    /// <returns>The explanation, or null.</returns>
    internal static string DescribeWithdrawalBlock(TacticalWithdrawalStatus status)
    {
        return status switch
        {
            TacticalWithdrawalStatus.Interdicted => "Retreat blocked by an enemy gravity well.",
            TacticalWithdrawalStatus.NoHyperdrive => "No retreat: no hyperdrive in your force.",
            TacticalWithdrawalStatus.NoEscapeRoute => "No retreat: no safe system to jump to.",
            _ => null,
        };
    }

    /// <summary>
    /// Builds the control hint for the current selection and inspection state.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="selection">The selected units.</param>
    /// <param name="focus">The inspected unit.</param>
    /// <returns>The hint text.</returns>
    private static string BuildControlHint(
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
        string stationHint = DescribeStationHint(battle, selection);
        if (stationHint != null)
            return stationHint;
        if (selection?.Count > 1)
            return DescribeSelectionHint(selection);
        if (selection?.Count == 1)
            return selection.First().IsStarfighter
                ? "Right-click an enemy to attack, or one of your ships to screen it."
                : "Right-click to move or attack.";
        if (focus != null)
            return HasPlayerShips(battle)
                ? "Click one of your units to command it. Tab cycles your ships."
                : "Click one of your squadrons to command it.";
        return "Click a unit to select it, again to zoom to it. Drag selects several. Wheel zooms, right-drag orbits, WASD pans, Home reframes.";
    }

    /// <summary>
    /// Tells the player what the selected squadrons can do about an enemy battle station: fly
    /// its trench, with the chance of success, or nothing while a planetary shield protects it.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <param name="selection">The selected units.</param>
    /// <returns>
    /// The hint, or null when no squadron is selected or no enemy station is in the battle.
    /// </returns>
    internal static string DescribeStationHint(
        TacticalSpaceBattle battle,
        IReadOnlyCollection<TacticalBattleUnit> selection
    )
    {
        if (battle.PlayerSide == null || selection?.Any(unit => unit.IsStarfighter) != true)
            return null;

        TacticalBattleStation station = battle.GetStation(
            battle.PlayerSide.Value == CombatSide.Attacker
                ? CombatSide.Defender
                : CombatSide.Attacker
        );
        if (station == null)
            return null;
        if (station.IsShielded)
            return $"Right-click an enemy to attack. The {station.DisplayName} is shielded from fighters.";

        int chance = Mathf.RoundToInt(
            (float)battle.EstimateTrenchRunChance(selection, station) * 100f
        );
        return chance > 0
            ? $"Right-click an enemy to attack, or the {station.DisplayName} for a trench run ({chance}% success)."
            : $"Right-click an enemy to attack. A trench run on the {station.DisplayName} needs torpedoes.";
    }

    /// <summary>
    /// Describes a multi-unit selection by what is in it and what a right-click will do.
    /// </summary>
    /// <param name="selection">The selected units.</param>
    /// <returns>The hint text.</returns>
    private static string DescribeSelectionHint(IReadOnlyCollection<TacticalBattleUnit> selection)
    {
        int squadrons = 0;
        foreach (TacticalBattleUnit unit in selection)
        {
            if (unit.IsStarfighter)
                squadrons++;
        }

        int ships = selection.Count - squadrons;
        string shipText = ships == 1 ? "1 ship" : $"{ships} ships";
        string squadronText = squadrons == 1 ? "1 squadron" : $"{squadrons} squadrons";
        if (ships == 0)
            return $"{squadronText} selected. Right-click an enemy to attack, or one of your ships to screen it.";
        if (squadrons == 0)
            return $"{shipText} selected. Right-click to move or attack.";
        return $"{shipText} and {squadronText} selected. Right-click to move or attack.";
    }

    /// <summary>
    /// Returns whether the player still has a capital ship in the battle.
    /// </summary>
    /// <param name="battle">The battle being presented.</param>
    /// <returns>True when a player capital ship is still in the battle.</returns>
    private static bool HasPlayerShips(TacticalSpaceBattle battle)
    {
        foreach (TacticalBattleUnit unit in battle.Units)
        {
            if (unit.Side == battle.PlayerSide && !unit.IsStarfighter && unit.IsTargetable)
                return true;
        }

        return false;
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
