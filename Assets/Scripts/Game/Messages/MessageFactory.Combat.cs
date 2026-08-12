using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Presentation.Advisor;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Messages
{
    /// <summary>
    /// Translates space combat, bombardment, and planetary assault results into reports.
    /// </summary>
    public partial class MessageFactory
    {
        private void AddCombatMessages(
            IEnumerable<SpaceCombatResult> battles,
            IEnumerable<BombardmentResult> bombardments,
            IEnumerable<PlanetaryAssaultResult> assaults,
            GameRoot game,
            ICollection<MessageDelivery> deliveries
        )
        {
            foreach (SpaceCombatResult result in battles)
            {
                Faction attacker = GetCombatFaction(game, GetOwnerID(result, CombatSide.Attacker));
                Faction defender = GetCombatFaction(game, GetOwnerID(result, CombatSide.Defender));
                AddCombatDelivery(
                    deliveries,
                    attacker,
                    CreateSpaceBattle(attacker, result, defender),
                    result
                );
                if (defender?.InstanceID != attacker?.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateSpaceBattle(defender, result, attacker),
                        result
                    );
            }

            foreach (BombardmentResult result in bombardments)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;
                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetCombatFaction(game, result.Planet.OwnerInstanceID);
                AddCombatDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateBombardment(result.AttackingFaction, result, defender),
                    result
                );
                if (defender?.InstanceID != result.AttackingFaction.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateBombardment(defender, result, defender),
                        result
                    );
            }

            foreach (PlanetaryAssaultResult result in assaults)
            {
                if (result?.AttackingFaction == null || result.Planet == null)
                    continue;
                Faction defender =
                    result.OwnershipChange?.PreviousOwner
                    ?? GetCombatFaction(game, result.Planet.OwnerInstanceID);
                AddCombatDelivery(
                    deliveries,
                    result.AttackingFaction,
                    CreateAssault(result.AttackingFaction, result, defender),
                    result
                );
                if (defender?.InstanceID != result.AttackingFaction.InstanceID)
                    AddCombatDelivery(
                        deliveries,
                        defender,
                        CreateAssault(defender, result, defender),
                        result
                    );
            }
        }

        private Message CreateSpaceBattle(
            Faction faction,
            SpaceCombatResult result,
            Faction opponent
        )
        {
            if (result == null)
                return null;
            MessageResultOutcome outcome = GetOutcome(faction, result);
            if (outcome == MessageResultOutcome.None)
                return null;

            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.SpaceBattle,
                outcome
            );
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                { "faction", faction?.GetDisplayName() ?? string.Empty },
                { "opponent", opponent?.GetDisplayName() ?? string.Empty },
                { "system", result.Planet?.GetDisplayName() ?? string.Empty },
            };
            AddNarrative(values, definition, faction, opponent, result, outcome);
            Message message = BuildCombatMessage(definition, faction, values);
            SetCombatLocation(message, result.Planet, GetFleet(faction, result));
            return message;
        }

        private Message CreateBombardment(
            Faction faction,
            BombardmentResult result,
            Faction targetFaction
        )
        {
            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.Bombardment,
                GetBombardmentOutcome(result),
                GetBombardmentOwnership(result),
                planetDestroyed: result.PlanetDestroyed
            );
            Message message = BuildCombatMessage(
                definition,
                faction,
                CombatValues(result.AttackingFaction, targetFaction, result.Planet)
            );
            _deliveryBuilder.WithNotification(
                message,
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationType.None
                    : AdvisorNotificationType.Bombardment
            );
            SetCombatLocation(message, result.Planet, result.Planet);
            return message;
        }

        private Message CreateAssault(
            Faction faction,
            PlanetaryAssaultResult result,
            Faction targetFaction
        )
        {
            MessageDefinition definition = _definitionResolver.GetDefinition(
                MessageResultType.PlanetaryAssault,
                result.Success ? MessageResultOutcome.Success : MessageResultOutcome.Failed,
                GetAssaultOwnership(result)
            );
            Message message = BuildCombatMessage(
                definition,
                faction,
                CombatValues(result.AttackingFaction, targetFaction, result.Planet),
                result.AttackingFaction
            );
            _deliveryBuilder.WithNotification(
                message,
                faction?.InstanceID == result.AttackingFaction?.InstanceID
                    ? AdvisorNotificationType.None
                    : AdvisorNotificationType.PlanetaryAssault
            );
            SetCombatLocation(message, result.Planet, result.Planet);
            return message;
        }

        private Message BuildCombatMessage(
            MessageDefinition definition,
            Faction faction,
            Dictionary<string, string> values,
            Faction imageFaction = null
        )
        {
            Message message = _templateBuilder.Build(definition, faction, values, imageFaction);
            return _deliveryBuilder.WithNotification(
                message,
                AdvisorNotificationPolicy.GetDefault(definition?.ResultType)
            );
        }

        private static void AddNarrative(
            Dictionary<string, string> values,
            MessageDefinition definition,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome
        )
        {
            SpaceBattleNarrativeTemplates templates = definition?.SpaceBattleNarrative;
            if (templates == null)
                return;
            values["headline"] = Render(GetHeadline(templates, outcome), values);
            values["situation"] = Render(
                GetSituation(templates, faction, opponent, result, outcome),
                values
            );
            values["fleetOutcome"] = BuildFleetOutcome(
                templates,
                faction,
                opponent,
                result,
                outcome,
                values
            );
        }

        private static MessageResultOutcome GetOutcome(Faction faction, SpaceCombatResult result)
        {
            if (result.Winner == CombatSide.Draw)
                return MessageResultOutcome.Stalemate;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return result.Winner == CombatSide.Attacker
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Defender))
                return result.Winner == CombatSide.Defender
                    ? MessageResultOutcome.Victory
                    : MessageResultOutcome.Defeat;
            return MessageResultOutcome.None;
        }

        private static string GetHeadline(
            SpaceBattleNarrativeTemplates templates,
            MessageResultOutcome outcome
        ) =>
            outcome switch
            {
                MessageResultOutcome.Victory => templates.VictoryHeadline,
                MessageResultOutcome.Defeat => templates.DefeatHeadline,
                MessageResultOutcome.Stalemate => templates.StalemateHeadline,
                _ => string.Empty,
            };

        private static string GetSituation(
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome
        )
        {
            if (outcome == MessageResultOutcome.Stalemate)
                return templates.NoVictor;
            string ownerID = result.Planet?.OwnerInstanceID;
            if (string.IsNullOrEmpty(ownerID))
                return outcome == MessageResultOutcome.Victory
                    ? templates.NeutralVictory
                    : templates.NeutralDefeat;

            CombatSide side = GetSide(faction, result);
            bool factionOwnsPlanet = ownerID == faction?.InstanceID;
            bool opponentOwnsPlanet = ownerID == opponent?.InstanceID;
            if (outcome == MessageResultOutcome.Victory)
            {
                if (factionOwnsPlanet)
                    return side == CombatSide.Defender
                        ? templates.SuccessfullyDefended
                        : templates.BlockadeBroken;
                return side == CombatSide.Defender
                    ? templates.BlockadeMaintained
                    : templates.BlockadeEstablished;
            }
            if (factionOwnsPlanet)
                return side == CombatSide.Defender
                    ? templates.BlockadeEstablished
                    : templates.BlockadeMaintained;
            if (opponentOwnsPlanet)
                return side == CombatSide.Attacker
                    ? templates.AttackFailed
                    : templates.BlockadeBroken;
            return templates.AttackFailed;
        }

        private static string BuildFleetOutcome(
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            Faction opponent,
            SpaceCombatResult result,
            MessageResultOutcome outcome,
            Dictionary<string, string> values
        )
        {
            SpaceCombatSideOutcome factionOutcome = GetSideOutcome(faction, result);
            SpaceCombatSideOutcome opponentOutcome = GetSideOutcome(opponent, result);
            if (
                outcome == MessageResultOutcome.Stalemate
                && factionOutcome == SpaceCombatSideOutcome.Destroyed
                && opponentOutcome == SpaceCombatSideOutcome.Destroyed
            )
                return Render(templates.AllShipsDestroyed, values);

            List<string> lines = new List<string>();
            AddFleetLine(lines, templates, faction, factionOutcome, result, values, true);
            AddFleetLine(lines, templates, opponent, opponentOutcome, result, values, false);
            return string.Join("\n", lines);
        }

        private static void AddFleetLine(
            ICollection<string> lines,
            SpaceBattleNarrativeTemplates templates,
            Faction faction,
            SpaceCombatSideOutcome outcome,
            SpaceCombatResult result,
            Dictionary<string, string> values,
            bool includeRetreatDestination
        )
        {
            if (outcome is SpaceCombatSideOutcome.Active or SpaceCombatSideOutcome.Unknown)
                return;
            Dictionary<string, string> lineValues = new Dictionary<string, string>(values)
            {
                ["fleetFaction"] = faction?.GetDisplayName() ?? string.Empty,
            };
            string template;
            if (outcome == SpaceCombatSideOutcome.Destroyed)
            {
                template = templates.FleetDestroyed;
            }
            else
            {
                Planet destination = includeRetreatDestination
                    ? GetFleet(faction, result)?.GetParentOfType<Planet>()
                    : null;
                if (destination == result.Planet)
                    destination = null;
                lineValues["retreatSystem"] = destination?.GetDisplayName() ?? string.Empty;
                template =
                    destination == null ? templates.FleetWithdrawn : templates.FleetWithdrawnTo;
            }
            string line = Render(template, lineValues);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        private static CombatSide GetSide(Faction faction, SpaceCombatResult result)
        {
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return CombatSide.Attacker;
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Defender))
                return CombatSide.Defender;
            return CombatSide.Draw;
        }

        private static SpaceCombatSideOutcome GetSideOutcome(
            Faction faction,
            SpaceCombatResult result
        ) =>
            GetSide(faction, result) switch
            {
                CombatSide.Attacker => result.AttackerOutcome,
                CombatSide.Defender => result.DefenderOutcome,
                _ => SpaceCombatSideOutcome.Unknown,
            };

        private static Fleet GetFleet(Faction faction, SpaceCombatResult result)
        {
            if (faction?.InstanceID == GetOwnerID(result, CombatSide.Attacker))
                return result.AttackerFleet;
            return faction?.InstanceID == GetOwnerID(result, CombatSide.Defender)
                ? result.DefenderFleet
                : null;
        }

        private static string GetOwnerID(SpaceCombatResult result, CombatSide side) =>
            side switch
            {
                CombatSide.Attacker => string.IsNullOrEmpty(result?.AttackerOwnerInstanceID)
                    ? result?.AttackerFleet?.GetOwnerInstanceID()
                    : result.AttackerOwnerInstanceID,
                CombatSide.Defender => string.IsNullOrEmpty(result?.DefenderOwnerInstanceID)
                    ? result?.DefenderFleet?.GetOwnerInstanceID()
                    : result.DefenderOwnerInstanceID,
                _ => null,
            };

        private static MessageResultOutcome GetBombardmentOutcome(BombardmentResult result)
        {
            if (
                result.PlanetDestroyed
                || result.HeadquartersDestroyed
                || result.EnergyCapacityDamage > 0
                || result.AllocatedEnergyDamage > 0
                || result.DestroyedBuildings.Any()
                || result.DestroyedRegiments.Any()
            )
                return MessageResultOutcome.TargetLosses;
            return result.DestroyedCapitalShips.Any() || result.AttackerShipDamage.Any()
                ? MessageResultOutcome.AttackerLosses
                : MessageResultOutcome.NoLosses;
        }

        private static MessagePlanetOwnership GetBombardmentOwnership(BombardmentResult result) =>
            result?.OwnershipChange != null
                ? Ownership(result.OwnershipChange.PreviousOwner?.InstanceID)
                : Ownership(result?.Planet?.OwnerInstanceID);

        private static MessagePlanetOwnership GetAssaultOwnership(PlanetaryAssaultResult result) =>
            result?.OwnershipChange != null
                ? Ownership(result.OwnershipChange.PreviousOwner?.InstanceID)
                : Ownership(result?.Planet?.OwnerInstanceID);

        private static MessagePlanetOwnership Ownership(string ownerID) =>
            string.IsNullOrEmpty(ownerID)
                ? MessagePlanetOwnership.Neutral
                : MessagePlanetOwnership.Owned;

        private static Dictionary<string, string> CombatValues(
            Faction attacker,
            Faction target,
            Planet planet
        ) =>
            new Dictionary<string, string>
            {
                { "faction", attacker?.GetDisplayName() ?? string.Empty },
                { "target", target?.GetDisplayName() ?? string.Empty },
                { "system", planet?.GetDisplayName() ?? string.Empty },
            };

        private static string Render(string template, Dictionary<string, string> values) =>
            MessageTemplateBuilder.Interpolate(template, values);

        private static void SetCombatLocation(Message message, Planet planet, IGameEntity target)
        {
            if (message == null)
                return;
            message.EventLocationInstanceID = planet?.InstanceID;
            message.NavigationTargetInstanceID = target?.InstanceID ?? planet?.InstanceID;
        }

        private void AddCombatDelivery(
            ICollection<MessageDelivery> deliveries,
            Faction faction,
            Message message,
            GameResult sourceResult
        ) => _deliveryBuilder.Add(deliveries, faction, message, sourceResult);

        private static Faction GetCombatFaction(GameRoot game, string instanceID) =>
            string.IsNullOrEmpty(instanceID)
                ? null
                : game.GetFactions().FirstOrDefault(faction => faction.InstanceID == instanceID);
    }
}
