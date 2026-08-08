using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines an ongoing, data-authored game-state effect that is reconciled every tick.
    /// </summary>
    [PersistableObject]
    public abstract class GameEffect
    {
        internal const string ModifierKeyPrefix = "game-event:";

        /// <summary>
        /// Reconciles the effect with the current game state using a stable, event-owned key.
        /// </summary>
        public abstract void Reconcile(GameRoot game, string modifierKey);
    }

    /// <summary>
    /// Applies an additive rating modifier to every officer of one faction while a source unit
    /// remains anywhere inside the configured location hierarchy.
    /// </summary>
    [PersistableObject(Name = "FactionOfficerRatingAura")]
    public sealed class FactionOfficerRatingAuraEffect : GameEffect
    {
        public string SourceUnitInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
        public string AffectedFactionInstanceID { get; set; }
        public OfficerRating Rating { get; set; }
        public int Amount { get; set; }

        public override void Reconcile(GameRoot game, string modifierKey)
        {
            ISceneNode source = game.GetSceneNodeByInstanceID<ISceneNode>(SourceUnitInstanceID);
            ISceneNode location = game.GetSceneNodeByInstanceID<ISceneNode>(LocationInstanceID);
            bool isActive = GameEventHierarchy.Contains(location, source);

            foreach (Officer officer in game.GetRegisteredSceneNodesByType<Officer>())
            {
                if (isActive && officer.OwnerInstanceID == AffectedFactionInstanceID)
                    officer.SetRatingModifier(modifierKey, Rating, Amount);
                else
                    officer.RemoveRatingModifier(modifierKey);
            }
        }
    }
}
